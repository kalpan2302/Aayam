# Phase 4 Implementation Plan — Door Coordinate System

**Companion to `phase-4-door-coordinate-system.md`.** That doc is the
conceptual roadmap (coordinate math, PnP theory, calibration UX). This doc
is the concrete, codebase-grounded execution plan, following the same
pattern as `phase-3-implementation-plan.md`: what already exists, what's
actually missing, and the order to build it in.

**Starting point, not a clean slate.** Phase 3 ended with a specific,
documented failure: `HandleWorldAnchorRefiner` attempted exactly this
problem (map a 2D door-relative position to a 3D world position) using
`ARPlane.transform`'s local axes, and it was **not verified correct** after
two on-device tests and a sign flip (see `phase-3-implementation-plan.md`
Step 8, "world-space anchor refinement"). Phase 4 is not starting this
problem fresh — it's starting from a known-wrong approach and a hypothesis
about why.

---

## What Phase 3 actually leaves us with

| Roadmap doc's Step 1 input | Have it? | Source |
|---|---|---|
| Four corners of the door | ✅ in 2D mask-pixel space, proven correct across many real on-device reads | `DoorGeometryExtractor.DoorGeometry.{TopLeft,TopRight,BottomRight,BottomLeft}` |
| Door center point | ✅ derivable from the four corners | same |
| Door normal vector | ❌ not computed anywhere — 2D mask space has no normal | — |
| Handle position, door-relative | ✅ `HandleNormalized` (u,v fraction within the quad), unit-tested | `DoorGeometryExtractor.DoorGeometry.HandleNormalized` |
| **World-space door corners** | ❌ **this is the gap** — nothing currently and reliably converts 2D mask corners to 3D world points | — |

The 2D/mask-space half of Phase 3 is solid. The entire problem Phase 4
exists to solve is the missing 3D half.

---

## Re-reading the Phase 3 failure before repeating its mistake

`HandleWorldAnchorRefiner.ComputeWorldPosition` used the geometric
heuristic's `ARPlane` (`DoorCandidateDetector.BestCandidate`) as the
coordinate frame: `plane.transform.TransformPoint(localOffset)`, where
`localOffset` came from `plane.extents` and a (u,v) fraction. Two on-device
tests put the marker on the wrong side of a confirmed-left-handle door, and
flipping the X sign didn't fix it.

**Going back through the logged data turns up a second, uninvestigated
explanation that was never ruled out:** the detected plane's `extents`
across three logged samples were `(0.30, 0.87)`, `(0.43, 1.00)`,
`(0.55, 1.20)` — i.e. doors `0.60m × 1.74m`, `0.86m × 2.00m`, and
`1.10m × 2.40m`. A real Indian residential door is standardly
`0.9m × 2.1m` or `1.0m × 2.1m` (per this roadmap doc's own Step 2). The
first sample is **much** smaller than any real door, and all three disagree
with each other by more than plausible door-to-door variation. That's
consistent with ARKit's vertical-plane boundary being a **partial, still-
converging capture** of the real door, not a sign bug — in which case no
amount of sign-flipping would have fixed it, because the coordinate frame
itself was the wrong size and shape, change to change.

**This was never isolated from the sign hypothesis before Phase 3 was
closed out.** Phase 4 needs to settle this, not assume either way:

- Log `plane.extents` and the plane's own `boundary.Length` every time
  `HandleGeometry` fires, over a longer, controlled scan (walk fully around
  the door frame, let ARKit's boundary refine for 10-15s) rather than a
  single pass.
- Compare against a real tape-measure width/height of the test door.
- If extents stay unstable/undersized even after a long scan, the ARPlane
  boundary is **not a reliable source of ground truth**, and Phase 4's
  coordinate system must be built from the ML-detected corners (CV/PnP
  route) instead of ARKit's own plane geometry - which is what the roadmap
  doc's "Mathematical Foundation" section (PnP, Perspective-n-Point)
  already points at, rather than the ARPlane-extents shortcut Phase 3 took.

---

## Decision point: which world-space source of truth (resolve before Step 1 code)

Two real options, not yet chosen:

**Option A — Fix the ARPlane approach.** Use the plane's actual `boundary`
polygon (real 3D points ARKit observed) instead of reconstructing an offset
from `extents` + `transform.right`/`forward`. Cheaper to build on existing
code, but inherits whatever ARKit's plane detection got wrong (size,
partial coverage) - the exact failure mode flagged above, unresolved.

**Option B — PnP from the ML detection.** Use `DoorGeometryExtractor`'s four
2D corners + the camera's known intrinsics/pose (AR Foundation exposes
these) + an assumed real-world door size (roadmap's own `0.9×2.1m` /
`1.0×2.1m` standard sizes) to solve for the door's 3D pose directly -
independent of ARKit's vertical-plane detection entirely. This is what the
roadmap doc's math section describes, and sidesteps Option A's inherited
imprecision, at the cost of being a genuinely new CV component (no existing
code to build on, and implementing a PnP solver by hand in C# is real work
- Unity doesn't ship one).

**Recommendation: start with Option A, instrumented properly this time**
(the extents-stability logging above), because it's a half-day's work on
top of code that already exists, and the stability data will tell us
definitively whether Option A is salvageable or whether Option B's bigger
investment is actually necessary. Don't build the PnP solver speculatively
before that evidence exists.

### Extents-stability scan results (2026-10-12, real device data)

Ran `DoorPlaneStabilityLogger` on-device for ~125 seconds of continuous
scanning of the test door (`/tmp/aayam_stability_scan.log`, 125 one-second
samples, re-verified by grep in-session - not recalled from memory). Five
distinct `ARPlane` instances were picked as "best candidate" over that
scan, in this order:

| plane id (short) | samples | width | height | boundaryVerts |
|---|---|---|---|---|
| `0949A8D3...` | 4 | 0.700m → **1.000m** | 2.450m | 12 |
| `7344C2D1...` | 7 | 0.600m → 0.650m | 2.150m | 12→18 |
| `E441E711...` | 6 | 0.500m → 0.550m | 2.096m | 12→14 |
| `BD47695B...` | 1 | 0.550m | 2.100m | 11 |
| `6E43A30D...` | **107** | **0.600m** (exact, every sample) | 2.199–2.200m | 16-17 |

Two findings, separating cleanly:

1. **Once ARKit locks onto a plane, that plane's extents genuinely stop
   drifting.** The last plane (`6E43A30D...`) held width at exactly
   `0.600m` across all 107 consecutive samples and height within 1mm
   (2.199-2.200m) - zero wander. This answers the original question from
   Phase 3: given enough observation time, `ARPlane.extents` **is**
   stable, so the sign-flip bug in `HandleWorldAnchorRefiner` was a real,
   separate bug, not an artifact of reading a still-converging plane.
2. **But stable ≠ correct.** `0.600m` is well under any size in the
   roadmap's own standard-door table (`0.9×2.1m` / `1.0×2.1m`), and it's
   not an outlier - every plane in this scan except the first 4 samples
   stayed in the `0.50m-0.65m` range. The one plane that briefly touched
   a plausible door width (`0949A8D3...` at 1.000m) was tracked for only
   4 samples before ARKit replaced it with a narrower one. Read together,
   this looks like ARKit's vertical-plane detector repeatedly
   under-growing the boundary on this door (likely the door's uniform
   painted surface starving ARKit's feature tracker of points to extend
   the boundary across) and then locking confidently onto that undersized
   boundary rather than correcting it.

**Conclusion: Option A is not salvageable as a direct source of
ground-truth door size**, even with a long, patient scan and even once
"stable" by every signal this logger can see (`samePlaneAsLastSample`,
constant `boundaryVerts`, constant `score`). Fixing the sign bug in
`DoorCoordinateSystem`'s *rotation* is still worthwhile (it's a separate,
already-tested concern - see Step 1's unit-test results), but using the
plane's `extents` as the physical door width/height is not viable without
an external correction. Two ways to apply that correction, not yet chosen:

- **(a) Snap-to-standard**: once a plane is confirmed stable (N consecutive
  identical-width samples), round its measured width to the nearest
  roadmap standard size (`0.9m` or `1.0m`) rather than trusting the raw
  number. Cheap, stays entirely inside Option A's existing code, but is a
  heuristic that assumes every test door is a standard size - fine for
  this project's own door, not a general solution.
- **(b) Move to Option B (PnP)**: use `DoorGeometryExtractor`'s ML-detected
  corners instead of ARKit's plane boundary for size/pose, exactly as
  originally proposed above. More correct, more work, no shortcuts left to
  avoid building it.

Not deciding between (a) and (b) unilaterally here - same lesson as Phase
3's `HandleWorldAnchorRefiner` (don't treat a single round of reasoning as
verified; this time the asymmetry is cost/scope, not correctness, so it's
worth asking rather than assuming).

**Decision (2026-10-12, Kalpan): Option B.** Building the PnP solver.

### Option B progress

- [x] `DoorPnPSolver.cs` (`Assets/_Project/Scripts/AR/Planes/`): pure-math
      planar PnP via homography decomposition - 4 coplanar correspondences
      reduce general PnP to a homography (no SVD dependency Unity doesn't
      ship): exact 8x8 linear system via Gaussian elimination, decompose
      into R/t with Gram-Schmidt orthogonalization, sign-ambiguity resolved
      by requiring the object in front of the camera (local Z>0). Also
      carries `RotateIntrinsics90Clockwise` (raw camera intrinsics -> the
      rotated frame `VisualDoorSegmenter`'s pipeline actually feeds the
      model, matching its existing `RotateRgba90Clockwise`).
- [x] `DoorPnPSolverTests.cs` (EditMode): synthetic ground-truth tests -
      fronto-parallel, tilted+offset, non-square focal lengths, degenerate/
      collinear-points rejection, and the intrinsics-rotation formula
      against hand-derived values. **Ran via Unity's TestRunnerApi
      in-session, re-verified just now: `RUN FINISHED status=Passed
      pass=5 fail=0 skip=0`.** This is the piece with all the sign/
      convention risk (same category of bug as the twice-wrong
      `HandleWorldAnchorRefiner`), and it's now nailed down on synthetic
      data before any device involvement - not just reasoned through once.
- [ ] Integration component wiring `DoorGeometryExtractor`'s mask-space
      corners + `ARCameraManager.TryGetIntrinsics` + `DoorCoordinateSystem`
      into a world-space door origin. Mask-space corners need an extra
      scale step first (they're in the resized mask's pixel space, not the
      rotated camera frame's) - not yet built.
- [ ] On-device verification. Deliberately not started yet - same
      live-screenshot-relay timing gap that undermined
      `HandleWorldAnchorRefiner`'s verification applies here too, so this
      needs a verification method that doesn't depend on catching the
      camera at the exact right instant before it's trusted.

---

## Step 1: `DoorCoordinateSystem` — World ↔ Door Space Transforms

Build the class the roadmap doc already specifies
(`WorldToDoorSpace`/`DoorToWorldSpace`/rotation transforms via
`Transform.InverseTransformPoint`/`TransformPoint`), but driven by an
explicit door origin `Transform` that Phase 4 constructs and owns - not
borrowed from `DoorCandidateDetector`'s `ARPlane` directly, so the sign/
coverage questions above can be fixed in one place without touching
Phase 3's detection code.

**Status: transform math + Option B origin construction built and tested;
on-device verification not started. See "Option B progress" above for the
detailed breakdown - this checklist is kept in sync with it.**

- [x] `DoorCoordinateSystem.cs`: origin `Transform`, `WorldToDoorSpace`,
      `DoorToWorldSpace`, `WorldToDoorRotation`, `DoorToWorldRotation` -
      roadmap doc's own API, unit-tested with a plain `Transform` (no AR
      session needed, same technique used for `HandleWorldAnchorRefiner`'s
      sanity check in Phase 3). Round-trip verified to ~1e-7.
- [x] Origin construction, Option B (`SetOriginFromWorldPose`): fed by
      `DoorPnPWorldAnchor`'s PnP solve, not an `ARPlane`. `SetOriginFromPlane`
      (Option A) is still in the file but unused by the current wiring -
      superseded by the Option B decision above, kept only because deleting
      working code nobody asked to delete isn't the move. Worth removing
      once Option B is on-device verified and Option A truly won't be
      revisited.
- [x] Door axes per the roadmap: X = width (door-local, bottom-left to
      bottom-right), Y = height (bottom to top), Z = normal (out of the
      surface). Bottom-left origin, per Step 2's "bottom-left (or
      bottom-center, configurable)" - both `SetOriginFromPlane` and the PnP
      object-point convention in `DoorPnPWorldAnchor` use this.
- [ ] On-device verification that the constructed origin is actually
      correct (not just internally consistent) - not started. See "Option B
      progress" above for why (timing-gap lesson from
      `HandleWorldAnchorRefiner`) and the open question of verifying via XR
      Simulation (known-good synthetic ground truth) vs. a real-device scan.

## Step 2: Door Reference Frame + Component Zones

Data-only once Step 1's frame exists - the roadmap doc already specifies
the zone table (handle/lock/peephole/hinge, in mm) and standard door sizes.
Straightforward port, not attempted until Step 1's frame is verified
non-garbage.

## Steps 3-6 (Product anchors, AR-to-door mapping/drift, calibration,
opening direction)

Not started. Each depends on Step 1 actually working, which depends on
resolving the Option A/B question above with real evidence - sequencing
these before that would repeat Phase 3's mistake of building on an
unverified foundation.

---

## Status

**Started 2026-10-12. Decision made (Option B), solver + integration built
and compiling clean, synthetic math verified, on-device verification not
started.**

Chronology: `DoorCoordinateSystem`'s transform math was built and unit-
tested first (round-trip ~1e-7, rotation bugs caught by testing - see the
Step 1 section above for the sequential-axis-assignment bug that was caught
before it ever reached a device, unlike Phase 3's `HandleWorldAnchorRefiner`
which wasn't). Then the extents-stability scan (`DoorPlaneStabilityLogger`,
real device data, 125 samples) settled the Option A/B question: ARKit's
plane boundary is stable but undersized (locked at 0.600m wide vs. 0.9-1.0m
standard), so Option A was abandoned and Option B (PnP from the ML
detection) was built instead - `DoorPnPSolver` + `DoorPnPWorldAnchor`, see
"Option B progress" above for the full breakdown.

**What's actually left for Phase 4:**

1. **On-device verification of the PnP pipeline** - the next real blocker.
   Not started on purpose: the live phone screenshot-relay loop is exactly
   what produced three inconclusive rounds of "fix the sign, still wrong"
   on `HandleWorldAnchorRefiner` in Phase 3, eventually traced to a timing
   gap rather than necessarily the math. Two ways to verify, not yet
   chosen: (a) build an XR Simulation scene with a synthetic door of known
   pose/size and compare `DoorPnPWorldAnchor`'s recovered pose against
   known ground truth (no screenshot timing ambiguity at all), or (b) go
   straight to a real-device scan on the test door now that camera-pose-
   at-capture is logged synchronously (closing the specific gap that hurt
   Phase 3, though not a guarantee nothing else is wrong).
2. **Step 2: Component zones** (handle/lock/peephole/hinge, roadmap's own
   mm table) - a data-only port, not started, intentionally blocked on (1)
   since there's no point placing zones in a coordinate frame not yet known
   to be correct.
3. **Steps 3-6**: product anchor system, AR-to-door mapping/drift
   compensation, calibration routine (user taps to confirm/measure), door
   opening-direction detection - all not started, all blocked on (1) for
   the same reason.
4. **Accuracy target (<5mm placement, roadmap's own deliverable)** -
   can't be assessed at all until (1) happens; there's no measurement yet,
   synthetic or real, of how close the recovered pose is to a real door.
5. **Minor cleanup, not blocking**: `DoorCoordinateSystem.SetOriginFromPlane`
   (Option A) and `DoorPlaneStabilityLogger` are now dead/diagnostic code,
   kept rather than deleted since nobody's asked to remove them yet -
   worth doing once Option B is verified and Option A is truly not coming
   back.
