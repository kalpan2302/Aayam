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
and compiling clean, synthetic math verified. On-device verification
started 2026-10-19: scene wiring, marker rendering, a real PnP rotation-
frame bug, and a geometry-extraction fallback were all found and fixed
mid-pass (see "What's actually left" below) - visual confirmation of the
marker's placement is the one piece still outstanding. Step 2's zone data
also ported and tested.**

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
   Went with real-device scanning (option (b) below), chosen 2026-10-19,
   and it surfaced three real, independent bugs rather than one - all
   fixed this session, none by "reasoning through it once":
   - **Scene wiring never actually existed.** `DoorCoordinateSystem`/
     `DoorPnPWorldAnchor`/`DoorPnPDebugLabel` were committed as scripts but
     never wired into `ARtest.unity` by `AayamSceneBootstrapper` - checked
     directly via the Editor, confirmed absent, then wired and saved.
   - **Marker rendering was dark from day one.** The `Shader.Find("Universal
     Render Pipeline/Unlit")` lookup assumed this project runs URP - it
     doesn't (`Packages/manifest.json` has no render-pipeline package at
     all, Built-in RP only, confirmed via `Shader.Find` returning null in
     the Editor too, not just on-device). Switched to the built-in
     `Unlit/Color` and protected it from build-time shader stripping via
     Project Settings -> Graphics -> Always Included Shaders (the actual
     reason the fallback kept rendering magenta even after switching
     shaders once - stripping, not the shader choice, was failing twice).
   - **Real PnP math bug**: `DoorPnPSolver.RotateIntrinsics90Clockwise` was
     missing a fixed +90-degree rotation correction about the camera's
     forward axis - this is why the marker landed on the wall beside the
     door instead of on it. Found by building an actual end-to-end
     synthetic ground-truth test (known door pose -> raw-project ->
     real pixel-rotation code -> solve -> compare), not by re-deriving
     signs by hand again. The existing
     `RotateIntrinsics90Clockwise_KnownValues_MatchHandDerived` test was
     circular (asserted the method against its own formula) and could
     never have caught this - `DoorPnPSolverTests.RotatedPipeline_
     RecoversGroundTruthPose` is the real regression test now, built from
     an independent oracle. All 6 `DoorPnPSolverTests` pass.
   - **`DoorGeometryExtractor` was landing on 5-6 vertices, never 4**, on
     a real door, blocking every live PnP solve regardless of the above
     fixes (the fixed 4% epsilon from Phase 3's two validation photos
     didn't generalize). Replaced the single fixed epsilon with
     `SimplifyToQuad`, a bounded search outward from 4% in [2%, 5.5%] -
     the documented working range from the original epsilon-tuning note
     above.
   - Also enabled Development Build (for on-device Debug.Log visibility -
     found it doesn't actually stream back to the Editor console over a
     bare USB connection without matching Wi-Fi, so screenshots of the
     on-screen debug labels remain the real evidence channel for now).
   - **2026-10-21 continued**: three more real, independent issues found
     via actual screen recordings (frames extracted with OpenCV since no
     device console access exists over USB-only):
     - Marker rendering was STILL magenta even after the `Unlit/Color` +
       Always Included Shaders fix from 2026-10-19 - that fix verified
       correctly in the Editor twice and still failed on-device twice. Gave
       up on runtime `Shader.Find` entirely and switched to a serialized
       Material ASSET reference (`Assets/_Project/Materials/DoorPnPMarker.mat`,
       wired via `AayamSceneBootstrapper`) - the standard, not-string-based
       way to guarantee a shader survives a build. Confirmed cyan in the
       next recording.
     - The "undiagnosed" magenta ball from earlier sessions (PlacementReticle,
       flagged since Phase 1/2) turned out to be a red herring for Phase 4
       purposes: cross-referencing multiple close-spaced frames, it stays
       fixed near the top of the screen regardless of camera pan - almost
       certainly iOS's own screen-recording status indicator, not a Unity
       render at all.
     - With rendering finally confirmed working, a real position bug
       surfaced: the marker's on-screen error grew with distance from the
       door and nearly vanished up close - the signature of a *scale*
       error, not a rotation or corner-order error. The assumed door size
       was checked and confirmed correct (ruled out). The full math
       pipeline (rotation-frame correction AND the mask-to-frame coordinate
       scaling step) was re-verified end-to-end against synthetic ground
       truth, bit-perfect (diff=0.00000) - ruling out every piece of code
       this project's own synthetic tests can reach. That leaves the one
       input no synthetic test can cover: `ARCameraManager.TryGetIntrinsics`'s
       focalLength/principalPoint were never checked against
       `intrinsics.resolution` vs. the actual captured CPU image size - AR
       Foundation's contract does not guarantee these match, and a mismatch
       there produces exactly a distance-proportional error. Added a
       proportional correction in `VisualDoorSegmenter.OnCameraFrameReceived`
       plus an on-screen diagnostic line (`DoorPnPDebugLabel` now shows
       "intrinsics CORRECTED (reported AxB vs actual CxD)" or "intrinsics OK")
       so the next recording settles whether this was the actual cause -
       **not yet confirmed on-device**, this is the leading hypothesis
       after ruling out everything the math alone could explain, not a
       verified fix.
   - **2026-10-21, found without a recording** (asked to find the issue by
     reading code, not waiting on another device round-trip): the marker
     Kalpan kept seeing as magenta was very likely never
     `DoorPnPWorldAnchor`'s marker at all. `HandleWorldAnchorRefiner` - the
     Phase 3 diagnostic component this whole phase exists to supersede - is
     STILL wired into the scene by `AayamSceneBootstrapper`, subscribes to
     the exact same `VisualDoorSegmenter.OnDoorGeometry` event, and its
     `_markerColor` field defaults to `Color.magenta` literally. It fires on
     every successful detection and renders its own marker independently -
     every "still magenta" sighting this whole day was plausibly this, not
     a shader bug in the Phase 4 code at all. Its own class doc already
     called itself "a visual stepping stone, not yet wired into actual
     product placement."
     - **Fixed**: `AayamSceneBootstrapper` now wires `HandleWorldAnchorRefiner`
       but leaves it `enabled = false` - disabled, not removed, in case
       Option B's verification ever wants an A/B comparison against it.
     - **Also found and fixed while auditing**: `PlacementReticle` and
       `ModelLoader.CreatePlaceholderCube` had the exact same
       "CreatePrimitive with no explicit material -> missing-shader
       fallback" vulnerability as `DoorPnPWorldAnchor` did, independently.
       `HandleWorldAnchorRefiner` ALSO had its own broken
       `Shader.Find("Universal Render Pipeline/Unlit")` lookup (same dead
       URP assumption, this project has none). All four now use serialized
       Material asset references
       (`Assets/_Project/Materials/{DoorPnPMarker,ReticleDefault,PlaceholderCube}.mat`)
       via a new shared `AssignMaterialIfPresent` bootstrapper helper,
       instead of each repeating its own runtime string-based shader
       lookup. This was a systemic pattern across the project, not an
       isolated bug - grep for `CreatePrimitive` turned up all four sites
       in one pass.
     - All 9 relevant EditMode tests still pass; this was a pure
       scene-wiring/rendering change, nothing touched the verified PnP math.
   - **2026-10-21, continued**: the next recording after the
     `HandleWorldAnchorRefiner` disable confirmed that fix worked - a clean,
     genuinely cyan marker appeared, round, tracking as a world-space point,
     no magenta anywhere. But the intrinsics-diagnostic 3rd line was STILL
     never visible, in a recording that other evidence (marker color,
     no competing magenta) proved WAS running current code - ruling out
     "stale build" as the explanation for the first time. Checked the UI
     directly instead of assuming: `DoorPnPDebugText`'s `sizeDelta` was
     `(600, 80)`, sized for the original 2-line label; the 3rd line added
     that day was being silently clipped by legacy `Text`'s default
     `VerticalWrapMode.Truncate`, not failing to render at the C# level at
     all. Fixed: box grown to `(600, 140)` and `verticalOverflow` set to
     `Overflow` explicitly so a future added line can't repeat this
     silently. This explains why several consecutive recordings looked like
     "no progress" when the underlying fixes were in fact landing each
     time - the UI just wasn't showing the evidence.
   - **2026-10-21, first genuinely clean negative result**: a recording
     finally did what was asked - door held in frame, dozens of consecutive
     clean `Geo:` reads (no "not 4" failures), `intrinsics OK` visible on
     every single frame (the UI fix worked). `PlacementReticle`'s own fix
     also confirmed working in this recording - its green "valid vertical
     surface" dot appears correctly, centered on the door. But
     `DoorPnPWorldAnchor`'s cyan marker never appeared in a single frame of
     this entire ~33s recording, despite the door filling most of the frame
     throughout. This rules out staleness, UI clipping, and the intrinsics-
     resolution hypothesis all at once - the computed world position is
     genuinely not landing anywhere near the visible door.
     - Re-checked `DoorGeometryExtractor.OrderCorners`'s sum/difference
       corner-ordering logic by hand against the "v down-increasing" mask
       convention - mathematically correct, not the bug.
     - Added one more diagnostic since console access still doesn't exist:
       `Camera.main.WorldToViewportPoint(worldPos)`, shown on-screen as a
       4th debug line (`viewport=(x,y,z) ON-SCREEN|OFF-SCREEN`) - ground
       truth from Unity's own actual AR camera, independent of this
       project's own intrinsics/rotation math entirely. The next recording
       will show whether the computed point is just outside the frame edge
       (a modest, explainable offset) or wildly off (a real remaining bug
       in a part of the pipeline no synthetic test has reached yet - most
       likely candidate at this point: real ARKit camera pose/intrinsics
       behaving differently from the "nice" values every synthetic test so
       far has used).
   - **2026-10-21, viewport diagnostic result**: the very next recording
     had all 4 debug lines visible. `viewport=` across 4 separate frames
     (varying distance 2.71-2.97m): `(1.38,0.37)`, `(1.39,0.38)`,
     `(1.34,0.39)`, `(1.41,0.38)` - all OFF-SCREEN, but Y is consistently a
     correct ~0.37-0.39 (vertically fine) while X sits just past the right
     edge (~1.34-1.41) by a similar margin every time regardless of
     distance. Small and consistent, not a wild sign-flip - the signature
     of a left/right mislabeling, not a broken transform. Can't be fully
     diagnosed from synthetic tests alone: `RotatedPipeline_
     RecoversGroundTruthPose` verifies the rotation-frame math is
     internally self-consistent, but a self-consistent round-trip can't
     catch a case where the project's assumed "pixel-right = object-space
     +X" convention doesn't match reality, since the same assumption is
     baked into both sides of that test.
   - **Added a direct test instead of guessing again**: `DoorPnPWorldAnchor`
     now also solves a SECOND time with object-space X mirrored
     (TL<->TR, BL<->BR, same pixelCorners, same intrinsics) and renders
     that as a second, ORANGE marker alongside the original cyan one.
     Whichever one (if either) actually lands on the door in the next
     recording settles the hypothesis directly rather than through more
     synthetic reasoning.
   - **2026-10-21, mirror-hypothesis test came back inconclusive by
     construction, not by result**: the orange marker never appeared
     visually in the next recording either - but there was no numeric
     diagnostic for it yet, only for the original cyan marker, so "didn't
     see it" couldn't distinguish "the hypothesis is wrong" from "it's
     just as easy to miss as the first marker was before the viewport
     diagnostic existed." Fixed: `viewportDiag` now reports both markers by
     name (`cyan=(x,y) ON/OFF  orange=(x,y) ON/OFF`, or `orange=PnP FAILED`
     if the mirrored correspondence is degenerate) in one line, so the next
     recording gives a hard A/B answer instead of relying on spotting a
     15cm sphere in a compressed video frame.
   - **2026-10-21, mirror hypothesis DISPROVEN with real numbers**: with the
     dual numeric viewport diagnostic in place, the next recording gave a
     hard answer - `cyan=(1.32-1.34,0.36-0.39) OFF` and
     `orange=(1.32-1.35,0.52-0.67) OFF` across 4 frames. Both off-screen.
     Notably the mirrored-object-points solve barely moved X at all (only
     Y) - a full PnP re-solve with swapped correspondences changes the
     recovered rotation too, so it doesn't isolate to a clean translation
     mirror the way a simple "swap the corners" mental model suggests. This
     hypothesis is ruled out, not just unconfirmed.
   - **Measured the actual magnitude precisely** (pixel-sampled one frame's
     door edges with OpenCV rather than eyeballing: door spanned viewport
     X=[0.247, 0.768] at dist=2.91m) and converted using that frame's own
     assumed-door-width calibration: the cyan marker's position corresponds
     to a door-space X of roughly **2x the door's own width** past the true
     left edge. Small, consistent, not a wild sign-flip - across 8+ samples
     now, two separate recordings.
   - **Replaced the disproven hypothesis with a more surgical one**: rather
     than re-solving PnP with different correspondences (which changes
     rotation too, confounding the test), `DoorPnPWorldAnchor` now tests a
     pure sign flip of the already-recovered camera-local X component only
     (same rotation, same correspondence - which the mirror test showed
     isn't the problem) - whether Unity's actual AR camera local +X and
     this pipeline's assumed +X simply disagree in handedness. Still
     rendered as the orange marker with its own numeric viewport line.
   - **2026-10-21, stopped guessing shapes, asked for raw data instead**:
     after two shaped hypotheses (object-point mirror, disproven; camera-
     local X sign flip, pending) with no confirmed fix yet, switched
     strategy - rather than keep testing plausible-looking transforms,
     `DoorGeometryDebugLabel` now prints the 4 raw detected corners
     (TL/TR/BR/BL) as fractions of mask width/height. This lets the corners
     be checked directly against the photo - does "TL" actually land near
     the door's visual top-left in the image, or somewhere else? That would
     point straight at `DoorGeometryExtractor`/`OrderCorners` instead of the
     PnP math, which is a different part of the pipeline than anything
     tested so far. Grew the Geo label's box for the extra lines and
     recomputed all three debug labels' vertical spans by hand to confirm no
     overlap this time (ML [660,740], Geo [480,640], PnP [267.5,452.5]).
   - **Status as of 2026-10-21 (latest)**: rendering fully confirmed working
     (`DoorPnPWorldAnchor` marker genuinely cyan, `PlacementReticle` genuinely
     green, no competing magenta anywhere). Position is the one open
     question, now a multi-session investigation: 2x-door-width-magnitude
     offset confirmed consistent across 8+ on-device samples, one shaped
     hypothesis disproven, a second pending, and raw corner data now
     available to check against the source photo directly instead of
     testing further guesses blind. Nothing here is committed yet -
     `git status` shows it all in the working tree.
   - **2026-10-21/22, a genuinely new lead found via verified math, not
     another shaped guess**: fed the exact on-device `camPos`/`camRot`/
     `worldPos` numbers (from a full Xcode console dump Kalpan copied
     directly, not the rounded UI label) through Unity's own `Quaternion`/
     `Vector3` math via the Editor (`Unity_RunCommand`), not by hand.
     Camera-local angle from the camera's forward axis to the recovered
     door position came out to **22-33 degrees off-axis** across 5 samples
     spanning the recording - comfortably inside a real phone lens's field
     of view. That contradicts the `viewport.x` ~1.3-1.5 the on-screen
     diagnostic had been reporting, and the contradiction pointed at
     `Camera.fieldOfView`: grepped the whole codebase, nothing anywhere
     ever sets it, so it sits at Unity's new-Camera default (60 degrees),
     narrower than the real device lens - a well-known AR Foundation gap
     (`ARCameraManager`/`ARCameraBackground` do not sync it automatically).
     **Fix applied**: `VisualDoorSegmenter.OnCameraFrameReceived` already
     reads real intrinsics every frame for the PnP math - added one line
     syncing `Camera.fieldOfView` from the same `rawFx`/`rawWidth`, via a
     new testable static helper `ComputeVerticalFieldOfViewDegrees`.
     Verified the formula itself synthetically (90-degree sanity case
     returns exactly 90.000; `VisualDoorSegmenterFieldOfViewTests.cs`,
     3 tests) before trusting it on-device - confirmed the camera
     `VisualDoorSegmenter` sets FOV on and `Camera.main` used by the
     viewport diagnostic are the literal same scene object
     (`sameAsArCamera=True`, checked directly), ruling out a camera-identity
     mismatch as a reason the fix might not take effect. Rebuilt, confirmed
     via the Data-folder timestamp that the new build actually ran.
   - **2026-10-22: reported still off-screen, on a confirmed clean Xcode
     build** (Kalpan explicitly ruled out stale-build caching - "i already
     clean it"). Got the actual console dump from that run. Re-verified
     azimuth via the same Quaternion-based Unity math on 5 fresh samples:
     21.95-23.66 degrees - essentially unchanged from before the fix, as
     expected (world-position math doesn't depend on render FOV at all).
     But the remaining gap is now much smaller than before: even a generous
     80-degree vertical FOV only yields a 21.2-degree half-horizontal-FOV
     against the device's actual elongated portrait screen aspect
     (computed, not assumed - `aspect = atan(screenAspect * tan(halfVFOV))`).
     The marker sits just outside that, by a few degrees, not the ~2x-width
     margin measured before the fix - real, quantified progress, just not
     enough to land on-screen yet.
   - **New lead, not yet confirmed**: the raw camera capture is 1920x1440
     (confirmed directly from ARKit's own `videoFormat` log line, not
     inferred) - a 4:3-ish aspect, much boxier than any iPhone's actual
     elongated screen (~0.46 portrait aspect). Something has to reconcile
     that mismatch when the passthrough video is displayed, and whether
     it's a center-crop or a letterbox changes which aspect ratio Unity's
     3D camera needs to use to project consistently with it -
     `Camera.aspect` may need to explicitly match the raw capture's
     rotated aspect (0.75) rather than Unity's own screen-tracked default,
     since the ML/PnP pipeline's azimuth math is computed in the RAW
     capture's coordinate space, not the display's. Hand-computed check:
     at `Camera.aspect=0.75` (instead of the screen's ~0.46) with the same
     vFOV this fix already computes (~69 degrees for a plausible focal
     length), half-horizontal-FOV comes out to ~27 degrees - comfortably
     covering the observed 22-24 degree azimuth. This is a real candidate,
     not confirmed: added a one-time `[FOVDIAG]` log
     (`VisualDoorSegmenter.OnCameraFrameReceived`) printing the actual
     applied `fieldOfView`, `Camera.aspect`, `Screen.width/height`, and raw
     intrinsics together, so the next recording settles what's actually
     happening at runtime instead of reasoning further from assumed values.
     Rebuilt (`Data` folder timestamp confirmed advanced, 2026-10-22 00:36).
     Not yet on-device tested.
   - **2026-10-24: `[FOVDIAG]` result came back, confirming the aspect-mismatch
     hypothesis.** Console dump: `appliedFieldOfView=65.17 cameraAspect=0.4600
     Screen=(1206x2622) rawWidth=1920 rawHeight=1440 rawFx=1502.05
     rawFy=1502.05`. `0.4600` matches `Screen.width/Screen.height`
     (1206/2622) exactly - `Camera.aspect` was tracking the portrait screen,
     not the raw capture's rotated aspect (`rawHeight/rawWidth` = 1440/1920 =
     0.75) the PnP/azimuth math is actually solved in, confirming the
     mismatch flagged as a candidate the day before. Same recording: no
     marker visible in any frame, viewport diagnostic still reported OFF
     throughout - consistent with the mismatch not yet being corrected
     (this diagnostic build only logged the value, it didn't fix it).
     **Fix applied**: `VisualDoorSegmenter.OnCameraFrameReceived` now sets
     `_arCamera.aspect = (float)rawHeight / rawWidth` explicitly each frame,
     right where `fieldOfView` is already corrected for the same reason
     (`Camera.aspect` stops auto-tracking `Screen`'s aspect once assigned,
     same mechanism as `fieldOfView`). **Not yet built or tested on-device**
     - no device/Editor access from this session; next on-device recording
     needs to confirm the marker lands on the door, and the `[FOVDIAG]` line
     itself should now log `cameraAspect=0.7500` after the fix instead of
     `0.4600`, which is the cheapest way to confirm the new build actually
     shipped it.
   - **2026-10-24, built and deployed (headless Unity build +
     `xcodebuild` + `devicectl install/launch`, no Kalpan/Xcode-GUI step
     needed), and the fix's own confirmation line checked out**: fresh
     console dump shows `[FOVDIAG] appliedFieldOfView=64.58
     cameraAspect=0.7500 Screen=(1206x2622) rawWidth=1920 rawHeight=1440
     rawFx=1519.19 rawFy=1519.19` - `cameraAspect` now reads exactly
     `0.7500`, confirming the new build shipped `Camera.aspect =
     rawHeight/rawWidth` as intended (not `0.4600` from before).
     **But the marker still did not land - "no marker is visible cyan and
     orange are out of screen" (Kalpan, same recording).** This disproves
     the aspect-mismatch hypothesis as the (sole) cause: the aspect was a
     real, measured, now-fixed bug, but fixing it alone did not resolve
     the off-screen marker. The underlying positioning bug (whatever is
     actually pushing the PnP-recovered point off-frame) is still
     unexplained - same category of "ruled out one real bug, the symptom
     persists" as the mirror-hypothesis test on 2026-10-21. Side notes
     from this run, not yet investigated: (a) `DoorPlaneStabilityLogger`
     now shows the plane settling at `width=0.950m height=2.200m` -
     much closer to the roadmap's real-door standard (0.9-1.0m x 2.1-2.2m)
     than the `0.600m` seen in the original 2026-10-12 stability scan,
     though this is Option A's plane data and not what Option B's PnP
     actually uses; (b) one `[DoorPnPWorldAnchor] PnP failed: homography
     system singular (degenerate/collinear points)` occurred mid-session
     - the solver's own documented degenerate-input guard firing correctly
     rather than returning garbage, not itself a bug. **Next diagnostic
     step, not yet taken**: get the actual `viewportDiag` numbers (the
     on-screen `cyan=(x,y) ON/OFF orange=(x,y) ON/OFF` line, or the
     equivalent console `[DoorPnPWorldAnchor] worldPos=... camPos=...`
     pairs run through `Camera.main.WorldToViewportPoint`) from this same
     recording - "off screen" by itself doesn't say whether the remaining
     error is small (an edge-case near the frame boundary) or large (a
     different bug entirely), and that distinction is exactly what
     resolved the mirror-hypothesis question on 2026-10-21.
   - **2026-10-24, capability correction**: `xcrun devicectl device process
     launch --device <udid> --terminate-existing --console
     com.kalpan2302.aayam`, run directly from this machine (device
     unlocked, app already installed), streams the live NSLog/stdout
     console - confirmed by capturing 35s of real output including
     `[FOVDIAG]` and `[DoorPnPWorldAnchor]` lines. This **corrects** the
     standing note elsewhere in this doc that console logs don't reach the
     Editor over a bare USB connection without matching Wi-Fi - that was
     true for whatever channel was tried before (Xcode's own device
     console attach, or streaming to the Unity Editor specifically), but
     `devicectl --console` is a different, working path that doesn't need
     Kalpan's own Xcode session at all. (One failure mode found along the
     way: launching while the device is locked fails with
     `FBSOpenApplicationErrorDomain error 7 (0x07) ... Locked` - the device
     must be unlocked first.)
   - **2026-10-24, viewport numbers obtained - both X-axis hypotheses now
     dead, not just "pending"**: ~50 samples from one continuous capture,
     door in frame throughout. `cyan.y` and `orange.y` are consistently
     ~0.25-0.32 (vertically ON-SCREEN range) in every sample - Y is fine.
     `cyan.x` clusters ~1.75-1.85 (OFF, past the right edge by ~0.8 screen-
     widths); `orange.x` clusters ~-0.75 to -0.82 (OFF, past the LEFT edge
     by about the same margin). `cyan.x` and `orange.x` sum to
     approximately `1.0` in every sample (e.g. `1.80 + (-0.80) = 1.00`) -
     exactly what "orange = pure sign flip of cyan's camera-local X" should
     produce: a mirror reflection around the frame's horizontal center, not
     a correction. **This directly disproves the camera-local-X-sign-flip
     hypothesis** (pending since 2026-10-21) the same way the object-point
     mirror hypothesis was disproved that day: flipping the sign doesn't
     reduce the error, it just reflects it to the other side by the same
     large magnitude (~0.8 screen-widths, not a small edge-case offset).
     Both members of the "flip X" hypothesis family are now dead. Also
     notable: this magnitude (~0.8 screen-widths off) is *larger* than the
     ~0.2-0.3 screen-widths (`viewport.x` ~1.3-1.5) measured on 2026-10-21
     before the FOV/aspect fixes - plausible explanation, not yet
     confirmed: `Camera.main`'s FOV/aspect only affect *rendering and
     `WorldToViewportPoint`'s projection*, not the PnP solver's own
     pixel-to-ray math (which uses `rawFx`/`rawFy`/principal point
     directly, entirely separately) - so the fix made `WorldToViewportPoint`
     a more faithful ground truth for "would a real photo show this point,"
     which may simply be surfacing a real error that a miscalibrated
     viewport projection was partly masking before, not making the
     underlying error worse. **Next step, now in progress**: raw corner
     data (`TL`/`TR`/`BR`/`BL`, already shown on-screen by
     `DoorGeometryDebugLabel` since 2026-10-21) had the same gap as the
     viewport diagnostic - only ever reached the on-screen label, never the
     console - so added `Debug.Log($"[DoorGeometryDebugLabel]
     {corners...}")` there too (same fix shape as `DoorPnPWorldAnchor`'s
     viewport line). Rebuilt and redeployed; next capture should let the
     raw corners be checked directly, exactly as planned on 2026-10-21
     ("does TL actually land near the door's visual top-left... or does it
     point straight at `DoorGeometryExtractor`/`OrderCorners`").
   - **2026-10-24, pixel-corner/intrinsics capture + independent cross-check
     - solver algorithm and world-transform both cleared, not the bug**:
     added `Debug.Log` of the exact scaled `pixelCorners` and
     `rawIntrinsics`/`rotatedIntrinsics` fed to `DoorPnPSolver.SolvePlanarPose`
     (`DoorPnPWorldAnchor.HandleGeometry`, right before the solve call).
     Rebuilt, redeployed, captured ~10 samples
     (`rotatedFrame=1440x1920 mask=480x640 scale=(3.000,3.000)`, e.g. sample 1:
     `pixelCorners TL=(444,444) TR=(933,450) BR=(936,1917) BL=(405,1917)`,
     `rotatedIntrinsics(Fx=1450.09,Fy=1450.09,Cx=712.31,Cy=969.94)`,
     `assumedSize=0.7874x2.032m` - note this is the Inspector-overridden
     value, not the `0.9x2.1` serialized default in the source). Checked
     three things against this real data, each independently, not by
     derivation:
     - `RotateIntrinsics90Clockwise`'s math: rotated `Cx=712.31` vs.
       hand-expected `rawHeight-1-rawCy = 1440-1-726.69 = 712.31` - exact.
       Rotated `Cy=969.94` vs. hand-expected `rawCx=969.94` - exact.
     - **The solver's own raw output, called directly**: wrote a one-off
       Editor script (`PnPVerificationScript.cs`, deleted after this check -
       scratch only, not a real test) that calls the actual built
       `DoorPnPSolver.SolvePlanarPose` with sample 1's exact real pixels/
       intrinsics/size, via `Unity -batchmode -executeMethod`. Got
       `RAW PositionCameraLocal=(-0.431, -1.329, 2.035)`, and confirmed the
       `RotatedFrameCorrection` + world-transform steps reproduce the logged
       `worldPos` *exactly* (`RECONSTRUCTED worldPos=(-0.470,-0.419,1.871)`
       vs. logged `(-0.47,-0.42,1.87)`) - **the world-transform step is
       cleared, not the bug.**
     - **Independent cross-check against OpenCV**: fed the identical
       correspondences (same `pixelCorners`, same object points, same `K`
       from `rotatedIntrinsics`, zero distortion) to `cv2.solvePnP`
       (`SOLVEPNP_ITERATIVE` and `SOLVEPNP_EPNP`), converting OpenCV's
       Y-down camera convention to this codebase's Y-up convention (flip Y
       only - `DoorPnPSolver`'s own doc already states the `v = cy - fy*Y/z`
       sign, so this is the documented convention, not a guess). Got
       Unity-equivalent `PositionCameraLocal ≈ (-0.449, -1.266, 1.960)`
       (ITERATIVE) vs. Unity's own `(-0.431, -1.329, 2.035)` - same sign on
       every axis, within ~5% magnitude. **This clears `SolvePlanarPose`'s
       homography-decomposition algorithm itself**: an independent,
       trusted solver given the exact same real-device inputs lands in the
       same place, not a mirrored or order-of-magnitude-different one.
     - **What this leaves**: with intrinsics rotation, the solver algorithm,
       and the world-transform all independently cleared, and pixel-space
       corner labeling already confirmed geometrically self-consistent
       (`TL`/`TR` smaller `y`, `BL`/`BR` larger `y` in the rotated frame;
       `TL`/`BL` smaller `x` than `TR`/`BR`) with a standard, unambiguous
       object-point mapping (`TL→(0,h)`, `TR→(w,h)`, `BR→(w,0)`, `BL→(0,0)`),
       a rough depth sanity check lands in the right ballpark too
       (`Fx*realWidth/pixelWidth ≈ 1450*0.7874/489 ≈ 2.33m` vs. solved
       `Z≈2.03m` - same order, consistent with perspective/tilt rather than
       a gross error). The corrected camera-local `X≈+1.33m` at `Z≈2.03m`
       depth is only modestly beyond a rough frustum-edge estimate at that
       depth (`depth*tan(halfHFOV)`, using the raw-capture-derived ~52.8°
       horizontal FOV, ≈1.0-1.3m) - **consistent with a small, real,
       explainable overshoot** (the door's bottom-left corner genuinely
       computing just past the frame edge for this framing/assumed-size
       combination), not a remaining sign-flip or algorithmic bug. Not yet
       checked: whether `Camera.main`'s actual runtime `fieldOfView` exactly
       matches the raw-capture-derived FOV used in this estimate (only
       `aspect` was previously confirmed via `[FOVDIAG]`), and whether
       `_assumedDoorWidthMeters`/`_assumedDoorHeightMeters` (`0.7874`/`2.032`,
       i.e. a 31"x80" standard door) matches the actual physical test door's
       measured size - a tape-measure check of the real door is the cheapest
       remaining way to rule in/out the last open hypothesis without further
       guessing.
   - **2026-10-24, real door measured (29.5"x85" without frame, Kalpan) -
     ruled out, and a different, higher-confidence suspect found**: updated
     `ARtest.unity`'s `DoorPnPWorldAnchor._assumedDoorWidthMeters`/
     `_assumedDoorHeightMeters` from the standard-door guess (`0.7874`/
     `2.032`) to the real measured size (`0.7493`/`2.159`m). Rebuilt,
     redeployed, captured 30s/616 lines fresh. **Made no measurable
     difference**: `cyan.x` still clusters ~1.74-2.09 (same range as before
     the fix, if anything slightly wider spread) - the assumed-size
     mismatch (~5-6%) was never large enough to be the explanation for a
     ~0.8-1.1 screen-width offset. This hypothesis is now dead, not just
     deprioritized.
   - Pulled one fresh matched sample (pixelCorners/intrinsics/worldPos/
     camPos/camRot/cyan, all from the same `HandleGeometry` call,
     `device_console_6.log` sample 1) and re-ran the same
     call-the-real-solver-directly check as before
     (`PnPVerificationScript`, deleted again after use). Two results:
     - `RECONSTRUCTED worldPos` from `correctedPos`/`camPos`/`camRot`
       matched the logged `worldPos` exactly again (`(-1.816,-0.331,0.444)`
       vs. logged `(-1.82,-0.33,0.44)`) - **re-confirms** the solver +
       world-transform chain, independent of door size.
     - **Built a real `Camera` matching that exact sample's own logged
       `fieldOfView`/`aspect`/position/rotation (`[FOVDIAG]
       appliedFieldOfView=68.91 cameraAspect=0.7500`, `camPos`/`camRot` from
       the same log line) and called its `WorldToViewportPoint` on the same
       `worldPos`**: got `(1.148, 0.139)`. The device's own
       `[DoorPnPWorldAnchor]` line for that exact sample logged
       `cyan=(1.86,0.10)`. **These disagree** - re-projecting the identical
       `worldPos` through a camera built from the identical logged
       FOV/aspect/position/rotation does not reproduce the logged viewport
       reading. The only way that's possible: `Camera.main`'s *actual*
       transform, at the moment `HandleGeometry`'s `viewportDiag` code
       runs, is not the same as `result.CameraPositionAtCapture`/
       `CameraRotationAtCapture` baked into `worldPos`.
     - **New leading hypothesis - staleness, not a sign/math bug**:
       `HandleGeometry` fires from `OnDoorGeometry`, downstream of
       whatever ML/CV segmentation latency exists between a frame being
       captured and its result reaching this callback. `Camera.main`'s
       transform is driven every frame by `TrackedPoseDriver` and keeps
       moving in that gap (the capture session shows large frame-to-frame
       yaw swings, e.g. `camRot` y-component `309.45→303.94→299.43` across
       three consecutive samples - the phone was panning while scanning).
       `worldPos` is correctly computed for where the door was *at capture
       time*; if the diagnostic (and possibly the rendered marker itself)
       is evaluated against the camera's *current*, since-moved pose, both
       would appear to lag behind the live view by an amount proportional
       to that pan speed - which would explain an apparent offset with no
       code defect at all in the pose math itself.
     - **Added a direct measurement for this** (not inferred): logged
       `Camera.main.transform.position - result.CameraPositionAtCapture`
       and `Quaternion.Angle(Camera.main.transform.rotation,
       result.CameraRotationAtCapture)` right next to `viewportDiag`
       (`DoorPnPWorldAnchor.cs`, `staleness: posDelta=...m rotDelta=...deg`).
       Rebuilt (`cibuild_ios_6.log`); next capture should show directly
       whether a real pose delta of the right order of magnitude exists
       between capture and diagnostic time, and if so, whether it's large
       enough on its own to account for the ~0.8-1.1 screen-width offset -
       **not yet captured/confirmed**, this is the immediate next step.
   - **2026-10-24, staleness hypothesis DISPROVEN, and a stronger lead found
     (ARCameraManager likely overriding the projection matrix)**: captured
     30s/598 lines with the staleness diagnostic live
     (`device_console_7.log`). Result: `posDelta` is `0.000-0.003m` and
     `rotDelta` is `0-2.43deg` across every sample this run - negligible -
     yet `cyan.x` is still `1.79-1.98` (same persistent offset as every
     prior capture). **Staleness is ruled out as the explanation for the
     core phenomenon** (it may still add noise on fast-pan frames, but it
     is not the base cause).
     - Re-ran the call-the-real-solver-directly + build-a-matching-Camera
       check (`PnPVerificationScript`, deleted again after use) against
       this run's own near-zero-staleness sample 1
       (`pixelCorners TL=(570,372) TR=(939,387) BR=(1011,1866) BL=(489,1881)`,
       `rotatedIntrinsics(Fx=1420.38,Cx=711.98,Cy=969.20)`,
       `[FOVDIAG] appliedFieldOfView=68.11 cameraAspect=0.7500`,
       `camPos=(0,1.12,0) camRot=(3.87,359.45,273.43)`). `worldPos`
       reconstruction matched the log exactly again
       (`(-0.231,-0.177,1.725)` vs. logged `(-0.23,-0.18,1.73)`) -
       re-confirms solver + world-transform once more. But a from-scratch
       `Camera` built with this sample's *exact* logged `fieldOfView`/
       `aspect`/position/rotation still gave `computed viewport=(1.133,
       0.384)` against the logged `cyan=(1.98,0.35)` - **a large mismatch
       even with staleness ruled out**. The only remaining explanation for
       "identical FOV/aspect/transform in, different `WorldToViewportPoint`
       answer out" is that `Camera.main`'s *actual* projection matrix is not
       the simple symmetric one `fieldOfView`+`aspect` would produce.
     - **New leading hypothesis**: `Camera.main` has an `ARCameraManager` on
       the same GameObject (`VisualDoorSegmenter`/`VisualDoorDetector`/
       `LightEstimationController` all `[RequireComponent(typeof(ARCameraManager))]`
       on it - confirmed in `ARtest.unity`, line 2363). AR Foundation's
       camera subsystem is documented to drive `Camera.projectionMatrix`
       directly from ARKit's own (frequently asymmetric/off-axis) intrinsics
       every frame, to keep the passthrough video background correctly
       aligned - independent of whatever `fieldOfView`/`aspect` were last
       set on the `Camera` component by `VisualDoorSegmenter`'s
       `[FOVDIAG]` fix. If true, that fix changed the *property values*
       (confirmed, `[FOVDIAG]` reads back correctly right after setting
       them) but may never have changed the *actual projection matrix* used
       by rendering and `WorldToViewportPoint`, because ARCameraManager
       overwrites it again before either runs. This would mean the
       aspect-mismatch fix from the prior session was real but
       functionally inert for exactly the paths that matter here - a
       different, more specific conclusion than "disproven as sole cause,"
       which assumed the fix was at least partially in effect.
     - **Added a direct check, not an assumption**: logging
       `Camera.main.projectionMatrix`'s key terms (`m00`, `m02`, `m11`,
       `m12`) next to a freshly-built `Matrix4x4.Perspective(fieldOfView,
       aspect, near, far)` using the camera's own current properties, right
       at `viewportDiag`'s location (`DoorPnPWorldAnchor.cs`). A standard
       symmetric perspective frustum has `m02=m12=0`; a non-zero `m02`/`m12`
       on the `actual` side with `symmetricExpected` showing `~0` would
       directly confirm an asymmetric/off-axis ARKit-driven projection in
       use. Rebuilt (`cibuild_ios_7.log`) - **not yet captured**, immediate
       next step.
   - **2026-10-24, projection-matrix hypothesis CONFIRMED, and a stronger,
     more specific lead found (RotatedFrameCorrection's direction, not the
     aspect fix)**: captured 30s/582 lines (`device_console_8.log`).
     `actual.m00≈4.35-4.40` vs. `symmetricExpected.m00≈2.67-2.70` - a ~1.63x
     mismatch - while `m11` (vertical) matches almost exactly between the
     two. The effective aspect implied by `actual`'s `m00`/`m11` ratio is
     `≈0.459` - an exact match to `Screen=(1206x2622)` → `1206/2622=0.4599`
     from the standing `[FOVDIAG]` log. **Confirms the hypothesis**:
     `Camera.main`'s real projection matrix tracks the actual device
     screen's aspect (`0.46`, a typical iPhone's elongated ~19.5:9 shape),
     not the `0.75` `VisualDoorSegmenter`'s 2026-10-22 fix sets - reading
     `.aspect`/`.fieldOfView` back shows the set values (that's what
     `[FOVDIAG]` was checking), but something in AR Foundation's camera
     pipeline (almost certainly `ARCameraManager`, confirmed present on the
     same GameObject) resets the actual matrix used for rendering/
     `WorldToViewportPoint` regardless. **This corrects the 2026-10-22
     comment's own assumption** in `VisualDoorSegmenter.cs`
     ("Setting Camera.aspect explicitly... stops auto-tracking Screen's
     aspect once assigned") - that was true for the *property*, not for the
     *matrix actually used*.
   - However, **re-deriving the expected viewport position using this real
     (correct) `0.459` aspect still predicts the same large off-screen
     `cyan` reading** (hand-built a `Camera` with the exact measured
     `m00≈4.36`/`m11≈2.0` and replayed `device_console_7.log` sample 1's
     real `pixelCorners`/`camPos`/`camRot` through it via
     `PnPVerificationScript`, deleted after use): `viewport=(1.899,0.343)`
     vs. logged `cyan=(1.98,0.35)` - close enough to validate the
     reconstruction method, and confirms **the aspect-override issue is
     real but not the dominant cause of the magnitude** of the offset.
   - **Testing `RotatedFrameCorrection` alternatives against that same real
     sample, same real projection matrix** (no rebuild needed, pure offline
     re-derivation from already-logged numbers): current `+90deg` →
     `viewport=(1.899,0.343)` OFF (matches logged); `-90deg` →
     `(-0.899,0.657)` OFF (mirror of current, same shape as the
     already-disproven orange hypothesis, sums to 1.0 - not the fix);
     **no correction at all (identity)** → `(0.158,-0.142)` - **X lands
     cleanly inside `[0,1]`, Y only slightly past the top edge** - far
     closer to on-screen than any other variant tested. `180deg` →
     `(0.842,1.142)`, also much closer than `±90deg` (mirror of the
     identity case).
   - **Why a rotation-direction bug here would be invisible on screen**:
     `RotateRgba90Clockwise` (`VisualDoorSegmenter.cs`) only rotates a
     *copy* of the frame fed to the ML segmentation model - it never
     touches `ARCameraBackground`'s actual passthrough video rendering, so
     a wrong rotation *direction* here would never make the live camera
     feed look sideways/upside-down to Kalpan; it would only ever surface
     as a wrong marker position, exactly what's been observed every
     recording this investigation has looked at. Checked
     `RotateRgba90Clockwise`'s real pixel-index math
     (`dstX=height-1-y; dstY=x`) against
     `DoorPnPSolverTests.RotatedPipeline_RecoversGroundTruthPose`'s
     independent hand-derived re-implementation of the same formula - they
     match exactly, confirming the test's ground-truth check is valid
     (non-circular) for whatever rotation direction it assumes, but it
     cannot catch whether *that direction* (CW) is the one this real
     device's ARKit buffer actually needs for this orientation - that's an
     empirical fact about the real sensor mapping no synthetic test can
     verify.
   - **Tested on-device, not just offline, before touching any production
     rotation code**: added a third, purely additive diagnostic marker
     (`purple`) in `DoorPnPWorldAnchor.HandleGeometry` using
     `pnp.PositionCameraLocal` directly (no `RotatedFrameCorrection`
     applied at all), logged alongside the existing cyan/orange via a new
     `purple=(x,y) ON/OFF` field on `viewportDiag`, with a visible on-screen
     sphere (`ShowThirdMarker`) matching the existing marker pattern.
     Nothing in `DoorPnPSolver`, `RotateRgba90Clockwise`, or the shared
     `RotatedFrameCorrection` constant touched - this is purely additive and
     reversible, same pattern as the original orange-marker test. Rebuilt
     (`cibuild_ios_8.log`) - **not yet captured on-device**, immediate next
     step.
   - **2026-10-24, ROTATEDFRAMECORRECTION CONFIRMED AS THE DOMINANT CAUSE -
     on real device data, not just offline reconstruction**: captured
     30s/522 lines (`device_console_9.log`). Across ~20 samples, `purple`
     (`pnp.PositionCameraLocal` used directly, no `RotatedFrameCorrection`)
     landed at `X∈[0.06,0.24]` (mean ~0.10) and `Y∈[-0.25,0.13]` - one
     sample `ON` screen outright (`purple=(0.11,0.13) ON`), the rest just
     outside the top edge by a modest, consistent margin. `cyan` (current,
     `+90deg`) stayed at `X∈[1.18,1.99]` and `orange` (`-90deg`-equivalent
     sign flip) at `X∈[-1.18,-0.37]` - both far off, same as every prior
     capture. **This is the first variant tested all investigation that
     lands inside or near the frame on real hardware.**
   - **Angle-swept a second real sample (`device_console_9.log` sample 2:
     `cyan=(1.78,0.37)` `orange=(-0.80,0.37)` `purple=(0.20,-0.09)`, all
     logged OFF) across the full `RotatedFrameCorrection` range in 15deg
     steps, offline, using that sample's own real pixelCorners/intrinsics/
     camPos/camRot/measured-projection-matrix** (no rebuild needed - pure
     re-derivation from already-logged real numbers, `PnPVerificationScript`
     again, deleted after use): the minimum distance-from-screen-center
     across the entire ±180deg sweep falls at `angle=0 to 15deg`
     (`distFromCenter≈0.62-0.66`), reproducing the logged `purple=(0.20,-0.09)`
     almost exactly at `angle=0`
     (`viewport=(0.222,-0.100)`) - confirming the offline reconstruction
     method is sound, and that **identity (no rotation) is at or very near
     the actual optimal correction, not some other specific angle** - `+90deg`
     (current) and `-90deg` are both near the WORST points in the entire
     sweep (`distFromCenter≈1.3`, the two local maxima).
   - **Interpretation and what's NOT yet isolated**: this test only removed
     the POSE-side correction (`RotatedFrameCorrection`, applied to
     `pnp.PositionCameraLocal`/`RotationCameraLocal` in
     `DoorPnPWorldAnchor`) - it left the PIXEL-side rotation
     (`RotateRgba90Clockwise`) and the matching intrinsics rotation
     (`RotateIntrinsics90Clockwise`) running exactly as before, so
     `SolvePlanarPose` is still being fed rotated pixels/rotated intrinsics;
     only the final compensating pose rotation was skipped. The small,
     consistent residual (`purple`'s `Y` landing just outside frame on most
     samples rather than dead-center) is consistent with that: if the real
     fix is that the ARKit `XRCpuImage` for this device/orientation doesn't
     need a manual 90deg rotation *at all* (plausible given
     `VisualDoorDetector`'s own prior `XRCpuImage.Transformation.None` fix,
     referenced in this file's class doc, already touches exactly this
     orientation-handling boundary), then feeding still-rotated pixels/
     intrinsics into the solver while only skipping the pose-side
     compensation is itself an inconsistent half-fix - it happens to cancel
     out most of the error (because the dominant, previously-applied
     correction was wrong), but not all of it. The cleaner, not-yet-tested
     alternative is skipping the pixel/intrinsics rotation too and feeding
     `SolvePlanarPose` raw (unrotated) corners/intrinsics directly, which
     this investigation has not yet tried.
   - **Current status: root cause narrowed to `RotatedFrameCorrection`/
     `RotateRgba90Clockwise`'s rotation direction (or necessity) for this
     real device, confirmed as the dominant driver via on-device data, not
     yet resolved to an exact final fix.** Recommended next step: test
     feeding `SolvePlanarPose` the RAW (pre-rotation) pixel corners and raw
     intrinsics directly (skip `RotateRgba90Clockwise`/
     `RotateIntrinsics90Clockwise`/`RotatedFrameCorrection` entirely for a
     test build), which should resolve the residual if the rotation step
     is unnecessary rather than merely backwards. This is a change to the
     shared detection pipeline (also used by `VisualDoorDetector`), bigger
     in blast radius than the purely-additive diagnostics so far - flagged
     to Kalpan before implementing, rather than changed unreviewed given
     this exact code path's prior regression history (2026-10-15, marker on
     wrong side of door). Kalpan approved ("go ahead").
   - **2026-10-24, RESOLVED - fix implemented and unit-verified, NOT the
     raw-pixel change originally proposed**: before rebuilding, re-checked
     the "feed raw unrotated pixels directly" proposal offline against real
     data first (no device cycle needed) and found it produces the
     IDENTICAL result to the current (wrong) `+90deg` behavior - proven
     both empirically (`PnPVerificationScript` Path B [raw pixels, no
     rotation] and Path C [rotated pixels + current `+90deg`] gave the
     exact same `posCameraLocal=(1.1318,-0.2579,1.9199)` for the same real
     sample) and algebraically (rotating normalized image rays by the same
     fixed angle used to build `rotatedIntrinsics`/pixels, then rotating the
     recovered pose back by the inverse, is a mathematical identity -
     `rotate+solve+apply(+90deg)` ≡ `solve-directly-on-raw+apply(no
     correction)`, for ANY input). That means the originally-approved raw-
     pixel test would have shipped the same bug under a different name, not
     fixed it. **The actual fix**: `RotateRgba90Clockwise`/
     `RotateIntrinsics90Clockwise` were already correct and necessary (they
     align the solve with Unity's real camera-local axes for this device's
     sensor mounting) - the bug was the *extra* `RotatedFrameCorrection`
     applied on top, which the identity above makes an unconditional no-op
     equivalent to never rotating at all. Removed it entirely:
     `DoorPnPWorldAnchor.HandleGeometry` now uses `pnp.PositionCameraLocal`/
     `RotationCameraLocal` directly (no correction quaternion), and
     `DoorPnPSolver.RotatedFrameCorrection` was deleted (confirmed unused
     anywhere else first). Retired the now-resolved diagnostic scaffolding
     (orange/purple alternate markers, staleness delta, projection-matrix
     dump - their questions are answered) back down to the original single
     marker + `intrinsicsDiag`/`viewportDiag` console line.
   - **`DoorPnPSolverTests.RotatedPipeline_RecoversGroundTruthPose` updated,
     not just left broken**: its ground truth implicitly assumed "camera-
     local axes == raw sensor pixel axes" (`Project` applied the raw
     pinhole formula directly to the known camera-local corner) - never
     checked against a real device, and exactly the wrong half of the
     identity above. Fixed by rotating the known camera-local corner by
     the real device's actual sensor-axis offset (`+90deg`, confirmed
     on-device) before projecting it as a synthetic "raw" pixel, and
     asserting the solve now recovers the known pose with NO correction -
     matching the corrected production code. Ran the full EditMode suite
     via `-runTests -testPlatform EditMode` (first fully-confirmed headless
     run this project, superseding the earlier "not verified via Test
     Runner" note in this repo's root `CLAUDE.md`): **101 tests, 99 passed,
     2 failed** - all 6 `DoorPnPSolverTests` passed including the corrected
     one; the 2 failures are `DoorRegistryTests.DoorCoordinateSystem_
     SelfRegisters_OnAwake`/`_Unregisters_OnDestroy` (`Expected: 1, But was:
     0`), unrelated to this change (different subsystem - door registry
     self-registration, not PnP math) and already flagged in this doc as
     unverified from a prior session (now confirmed actually failing, not
     just untested) - **pre-existing, not fixed here, out of this task's
     scope.**
   - Rebuilt (`cibuild_ios_9.log`) for final on-device confirmation of the
     real fix (not just the diagnostic markers) - capture pending.
   - **2026-10-24, FIX CONFIRMED ON REAL DEVICE - dominant bug resolved,
     one small secondary residual identified**: captured 30s/330 lines
     (`device_console_10.log`), 80 `cyan=` samples from one continuous
     recording. `cyan.x` now clusters at `[-0.05, 0.23]` - solidly inside
     the visible frame - versus `[1.18, 2.09]` on every capture before this
     fix. **This is the dominant bug, resolved.** `cyan.y` is consistently
     `[-0.26, -0.09]` - a small, systematic residual (marker sits just
     above the top edge, not randomly scattered), separate from the
     rotation-direction bug just fixed. Plausible candidates, NOT yet
     investigated: the already-documented `ARCameraManager`/ARKit
     projection-matrix override (real effective aspect `~0.46` vs the
     `0.75` `VisualDoorSegmenter`'s fix sets - still unresolved, smaller
     magnitude, noted in `DoorPnPWorldAnchor`'s class doc for whoever
     revisits it) or a vertical-FOV calibration nuance specific to this
     capture session's framing (very close-up shots of a tall door, bottom-
     left corner marker placement). Left as a documented follow-up, not
     blocking - the off-the-side-of-the-screen bug this investigation
     exists for is fixed.
   - **Files changed**: `DoorPnPSolver.cs` (removed `RotatedFrameCorrection`
     and its now-stale doc comments), `DoorPnPWorldAnchor.cs` (removed the
     correction's application plus the three now-resolved diagnostic
     markers/hypotheses - orange, purple, staleness, projection-matrix dump
     - back down to the single marker + `intrinsicsDiag`/`viewportDiag`),
     `DoorPnPSolverTests.cs` (corrected `RotatedPipeline_RecoversGroundTruthPose`'s
     ground truth to encode the real device's sensor-axis offset instead of
     assuming none). `DoorGeometryDebugLabel.cs`'s added console line and
     `Assets/Editor/CIBuildScript.cs` (the headless build script this whole
     investigation's build/deploy/capture loop depended on) were left in
     place - still useful, not part of the bug.
   - **2026-10-24, Kalpan's own-device check: marker now appears on-screen
     sometimes, but not exactly on the door's real bottom-left corner** -
     matches the small Y residual flagged above. Checked `worldPos.y` vs.
     `camPos.y` across all 80 samples in `device_console_10.log`:
     `worldPos.y` is consistently `1.33-1.42`, `camPos.y` consistently
     `1.12-1.13` - **the computed marker sits ~0.2-0.3m ABOVE the camera's
     own height, every sample.** The marker represents the door's floor-
     level bottom-left corner (object point `(0,0)`), which should be well
     BELOW a camera held at chest/waist height, not above it. **Leading
     hypothesis, not yet confirmed**: the ML-detected "bottom" corner isn't
     the door's true floor-level corner - it's wherever the door happens to
     be cropped by the bottom edge of THIS particular shot. If the true
     floor-level bottom isn't actually visible in frame, the pipeline still
     treats whatever pixel the model labels bottom-left as object-space
     `(0,0)` (true floor level) - a framing/detection-completeness gap, not
     a remaining pose-math bug. Recommended cheap test, no rebuild needed:
     reframe so the ENTIRE door (top to floor) is visible before the marker
     locks on, and check whether the residual shrinks. If it doesn't, this
     hypothesis is wrong and the remaining Y-bias needs a different
     explanation (back to the ARCameraManager aspect-override lead, or a
     lens/pose calibration offset) - not yet tested either way.
   - **2026-10-24, Kalpan tried full-door framing: still off.** Re-added
     the pixelCorners/rotatedIntrinsics console log plus a new
     `worldCorners` line (all 4 recovered corners' world positions, not
     just BL) to `DoorPnPWorldAnchor.cs` temporarily, rebuilt, captured 30s/
     434 lines with the door fully framed (`device_console_11.log`).
     **Framing is ruled out**: `worldPos.y` is still consistently `1.24-1.41`
     against `camPos.y` consistently `1.11-1.12` - same ~0.2-0.3m gap as
     the earlier (partially-framed) capture, unchanged.
   - **New observation while investigating, ultimately a dead end for this
     symptom**: `worldCorners` showed the door's width edge (TL-TR) and
     height edge (TL-BL) landing on what looked like swapped WORLD axes
     (e.g. TL-TR differing mostly in world Y instead of X). Traced this to
     `camRot`'s roll component sitting at a consistent `~268-271deg` across
     every sample - NOT a bug: verified via `PnPVerificationScript`
     (deleted after use) that in CAMERA-LOCAL space (before the world
     transform) the geometry is exactly correct - the object's `+X` (width)
     axis maps to camera-local `+X` (0.9995,-0.01,0.03) and `+Y` (height)
     maps to camera-local `+Y` (0.01,0.995,-0.09), and all 4 camera-local
     corners form a sane quad matching the assumed `0.7493x2.159m` size to
     within 1cm. The apparent world-space "swap" is just `camRot`'s own
     roll convention (consistent, not random) rotating those correct local
     axes - and it's provably irrelevant to this symptom anyway: `ShowMarker`
     only ever uses `worldPos` (a pure translation), never `worldRot`, so a
     rotation-reporting quirk cannot move where the rendered sphere
     appears.
   - **Status: every traceable step in the math has now been independently
     verified correct** - the solver algorithm (OpenCV cross-check), the
     world-transform (exact reconstruction), the rotation-direction fix
     (confirmed on-device, this session), and now the camera-local geometry
     itself (matches assumed size to ~1cm). The remaining ~0.2-0.3m offset
     is smooth and consistent across frames (not jittery), which points
     away from random per-frame ML detection noise and toward either (a) a
     systematic bias in how the segmentation model detects the door's
     bottom edge specifically, or (b) a real, fixed gap between ARKit's
     reported camera pose and the physical camera lens's true position -
     neither of which is a software logic bug this codebase's own math can
     be blamed for or fixed by further auditing. Narrowing between (a) and
     (b) needs either: a known-size reference object taped to the door to
     test the model's detection precision directly, or accepting this as
     this detection approach's current precision ceiling (the Phase 4
     roadmap's own accuracy target, `<5mm`, was never going to be met by a
     single-photo ML-segmentation approach - this gap was flagged as
     unassessed back in this doc's own "Accuracy target" section before
     this investigation started). Not resolved further without Kalpan's
     input on which path to take.
   - **2026-10-24, DECISION: accepted, not pursued further.** Kalpan:
     "let's accept it for now and move on." Removed the temporary
     `pixelCorners`/`rotatedIntrinsics`/`worldCorners` diagnostic logging
     added for this investigation (its question is answered); kept the
     lighter-weight `intrinsicsDiag`/`viewportDiag` console line as an
     ongoing sanity check, same as after the rotation-direction fix.
     `DoorPnPWorldAnchor`'s class doc now documents this residual and the
     decision directly so it isn't silently re-discovered later. **This
     closes the "marker off-screen" investigation this whole session was
     about** - the dominant bug (`RotatedFrameCorrection`) is fixed and
     confirmed on-device; the remaining ~0.2-0.3m positioning residual is
     accepted as this detection approach's current precision ceiling,
     revisit only if (4)'s `<5mm` accuracy target work needs it addressed.
   - **Unexplained, checked and NOT this codebase's own code**: that same
     console dump also logged `warning: using linearization / solving
     fallback.` once, with no other context captured around it.
     `grep -rn "linearization\|solving fallback" Assets/` (2026-10-24) finds
     zero matches - this string isn't `DoorPnPSolver` or anywhere else in
     this project's C#, so it's Unity/ARKit/Apple internals (likely ARKit's
     own world-tracking solver), not a symptom of this phase's PnP math.
     Not investigated further since it's outside this codebase; flagging in
     case it recurs and turns out to correlate with bad frames. Recurred
     again in the 2026-10-24 run, same signature, still unattributed.
2. **Step 2: Component zones** (handle/lock/peephole/hinge, roadmap's own
   mm table) - **data-only port done, 2026-10-19**
   (`DoorComponentZones.cs` + `DoorComponentZonesTests.cs`, 7/7 passing).
   Writing the static zone data doesn't need (1)'s coordinate frame proven
   correct, only *using* it for real placement does - that part is still
   blocked. Peephole's X and Handle/Lock's full range are roadmap-exact;
   Hinge's Y range is the roadmap's own "various locations along edge"
   left as the honest (unnarrowed) full-height span, documented as such
   rather than inventing precision the roadmap doesn't give.
3. **Steps 3-6**: product anchor system, AR-to-door mapping/drift
   compensation, calibration routine (user taps to confirm/measure), door
   opening-direction detection - **data/logic layer built 2026-10-19**, at
   Kalpan's explicit instruction after the risk was flagged (building ahead
   of (1)'s visual confirmation repeats Phase 3's `HandleWorldAnchorRefiner`
   mistake; accepted knowingly here, not silently). 26 new EditMode tests,
   32/32 total passing including full regression. What's built vs.
   deliberately deferred:
   - **Step 3** (`ProductAnchor`, `ProductAutoPlacer`, `ProductPlacementValidator`):
     auto-placement at zone center + hard-constraint validation against
     Step 2's zones, done and tested. Drag-and-drop UI (clamping a live
     gesture, not just validating a final position) deferred - real
     interaction work needing `AR/Interaction/DragHandler.cs` integration,
     unsafe to build without seeing it run.
   - **Step 4** (`DoorAnchorDriftCompensator`, `DoorAnchorPersistence`,
     `DoorRegistry`): roadmap's own Lerp/Slerp blend pseudocode ported
     directly; ARAnchor-based save/restore mirroring `AnchorController`'s
     existing pattern; static multi-door registry. Not wired to a live
     confidence source - nothing in this codebase produces one yet (noted
     in `DoorAnchorDriftCompensator`'s own class doc).
   - **Step 5** (`DoorCalibration`): the scale-correction math (steps 3-4 of
     the roadmap's 4-step routine) and a ground-truth placement-error
     utility, both tested. Steps 1-2 (detect, tap-to-confirm-edges UI)
     deferred, same UI-safety reasoning as Step 3's drag-and-drop.
   - **Step 6** (`DoorOpeningDirection`): hinge-side heuristic (opposite the
     handle - a residential-door assumption, NOT structural detection; no
     code anywhere classifies hinges visually, Phase 3 scoped that ML class
     out) plus a user-override path matching the roadmap's "tap to
     confirm" line. Inward/outward swing not modeled - the roadmap doesn't
     describe how to derive it from one camera view either.
   - Found one real pre-existing gap while wiring this in: `Aayam.AR.asmdef`
     never referenced `Aayam.Models`, so `AR/Planes` code couldn't use
     `ProductAnchor` until that reference was added.
   - **Update, 2026-10-21: Step 4's pieces are now wired into `ARtest.unity`**
     via a new `WireDoorAnchorSystems` bootstrapper method
     (`DoorAnchorDriftCompensator` + `DoorAnchorPersistence` attached to the
     camera rig, scene saved). Steps 3/5/6 remain pure static utility
     classes with nothing to attach yet - they'll get called from UI once
     that exists, not wired into the hierarchy themselves.
   - Caught a real bug while wiring this: the Editor bootstrapper originally
     called `DoorRegistry.Register(...)` itself - but that only affects the
     Editor process that happens to run the menu command, never the actual
     built app, which would've shipped with `DoorRegistry` silently empty
     on-device. Fixed by moving registration to `DoorCoordinateSystem.Awake()`/
     `OnDestroy()` (self-registration/unregistration), matching how every
     other door-adjacent MonoBehaviour in this codebase manages its own
     lifecycle. `DoorRegistryTests` gained two tests for this directly -
     **not independently confirmed passing in-session**, the available
     Editor-scripting tool has no way to pump the player loop between a
     component's `AddComponent` call and checking its effects within one
     synchronous execution, and a two-call probe couldn't distinguish
     genuine Editor-only Awake deferral from the tool's own lifecycle
     isolation. Standard Unity behavior (every other `Awake`/`OnEnable` in
     this codebase relies on the same mechanism) says it should just work;
     run via Window > General > Test Runner for an authoritative check if
     full confidence is wanted before trusting it. All other 32 Step 2-6
     tests + the 6 `DoorPnPSolverTests` pass cleanly (32/32 non-tool-limited).
   - **UI flows (drag-and-drop zone clamping, tap-to-calibrate,
     tap-to-confirm-hinge-side) are still entirely unbuilt**, and are a
     materially different kind of work than the logic layer above:
     `AR/Interaction/DragHandler.cs` computes its drag target entirely
     internally with no extension point, so zone-clamping means either
     modifying that working Phase 1 code or bolting on a post-process
     constraint - a real design decision, not just more typing. Still
     waiting on (1)'s on-device confirmation before attempting these, since
     they're exactly the kind of interactive code that's unsafe to build
     without being able to watch it run.
4. **Accuracy target (<5mm placement, roadmap's own deliverable)** -
   can't be assessed at all until (1) happens; there's no measurement yet,
   synthetic or real, of how close the recovered pose is to a real door.
   Needs an actual tape-measure ground-truth comparison once (1) passes
   visually, not just "looks right on screen."
5. **Minor cleanup, not blocking**: `DoorCoordinateSystem.SetOriginFromPlane`
   (Option A) and `DoorPlaneStabilityLogger` are now dead/diagnostic code,
   kept rather than deleted since nobody's asked to remove them yet -
   worth doing once Option B is verified and Option A is truly not coming
   back.
6. **Unrelated findings surfaced by enabling Development Build**, not
   investigated further (out of Phase 4's scope): an `ArgumentException`
   referencing `InputUpdateType.None`, a possible duplicate-component
   warning, and `ProductApiService` failing against `http://localhost:8010`
   on-device - the last one is pre-existing and already explained in
   `AppConfig.cs`'s own comment (the phone's "localhost" is the phone, not
   this Mac; needs the Mac's LAN IP or a tunnel for on-device catalog
   loading). The other two weren't found anywhere in `Assets/` - likely
   Unity/package-internal, not reproducible from the garbled on-screen
   text alone.
