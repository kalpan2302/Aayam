# Phase 3 Implementation Plan — Door Detection (Computer Vision)

**Companion to `phase-3-door-detection.md`.** That doc is the conceptual roadmap
(generic YOLO/dataset/Sentis template). This doc is the concrete, codebase-grounded
execution plan: what already exists, what's actually missing, and the order to
build it in — calibrated to a solo developer on one Mac, not a team with a
labeling budget.

**Verified against the real repo, 2026-10-11** (not assumed from the roadmap doc):

| Claim | Status |
|---|---|
| Any CV/ML model, dataset, or training script exists | ❌ None found (`find` for `.onnx/.pt/.tflite/.mlmodel`, backend `*detect*/*yolo*/*cv*` — all empty) |
| `com.unity.sentis` or similar inference package installed | ❌ Not in `Packages/manifest.json` |
| A geometric (non-ML) door heuristic already exists and works | ✅ `Assets/_Project/Scripts/AR/Planes/DoorCandidateDetector.cs` — scores every `ARPlane` from `VerticalPlaneFilter` against width/height/aspect-ratio/rectangularity ranges (door-sized rectangle heuristic), picks the best-scoring one, exposes `HasConfidentCandidate` which currently gates the HUD's Place button |
| Project is iOS/ARKit-only | ✅ **Resolved 2026-10-11 — no longer iOS-only.** Kalpan confirmed Android is genuinely in scope; `CLAUDE.md` has been corrected. `Packages/manifest.json`'s `com.unity.xr.arcore`/`com.unity.xr.openxr` are intentional, not leftover. Phase 3 plans for both platforms from here on. |

**What Phase 3 actually adds on top of the existing heuristic:** the heuristic
only sees plane *geometry* (width/height/shape) from ARKit/ARCore's own plane
detection — it has no idea what a handle, lock, or door frame *looks like*, and
it fails on doors ARKit can't resolve as a clean plane (glass, dark wood, low
light, partially open). Phase 3's job is to add real visual recognition on top,
not to replace a working system that already gates placement reasonably well.

---

## Decision points (pick before starting Step 1)

These materially change scope — recorded here so the plan doesn't quietly
assume an answer:

1. **Platform scope: RESOLVED 2026-10-11 — iOS + Android, both in scope.**
   Export pipeline needs both CoreML and TFLite (Step 4), roughly doubling the
   export/testing surface vs. an iOS-only MVP. This also confirms
   `com.unity.sentis` is worth adding now (Step 4/5) rather than waiting, since
   it's the only cross-platform inference path.
2. **Classes to detect:** the roadmap doc lists `door, door_frame, handle,
   lock, hinge` (5 classes). For an MVP, **`door` + `handle` only** is enough
   to materially improve on the existing plane heuristic (handle detection is
   the one thing geometry truly cannot do). Lock/frame/hinge are deferred to
   a v2 pass once the pipeline itself is proven. Confirm or override.
3. **Dataset size:** the roadmap doc's "2000+ annotated images" is a
   team-scale target. A YOLOv8n fine-tune (not training from scratch) on
   **150–300 self-shot images** covering real variation (3–5 door types ×
   multiple lighting/angles/distances) is a realistic solo-dev dataset and
   is the number this plan is built around. Scale up only if validation
   accuracy says you need to.

---

## Step 1: Minimal Dataset (realistic solo-dev scope)

**Status: ✅ Done (2026-10-11) — via a public Roboflow dataset, not self-shot
photos.** Used `uaeu/door-and-handles-detection` (v4) from Roboflow Universe
instead of shooting photos from scratch — 1,334 images (934 train / 267 valid
/ 133 test), both `door` and `handle` classes properly annotated (verified
directly against the label files, not just `data.yaml`'s claim). Real,
self-shot photos matching this app's specific distance/lighting range
(Step 1 as originally written below) remain a good v1.1 addition once the
pipeline is proven, but weren't necessary to get started.

**Goal:** 150–300 photos is enough to validate the whole pipeline end-to-end
before investing more.

1. Shoot photos on the actual target device (iPhone camera, not a DSLR —
   matches real inference-time image quality/distortion).
2. Cover real variation on purpose, not volume:
   - 3–5 different physical doors (wood, glass-paneled if you have one, metal).
   - Each door: 5–10 distances (0.5m–4m, matching `PlacementReticle._maxDistance`).
   - Each distance: a few angles (dead-on, 30° off-axis) and lighting conditions
     (daylight, indoor lamp, dim).
3. Annotate with **Roboflow's free tier** (not CVAT/Label Studio — Roboflow
   does annotation + augmentation + COCO/YOLO export + train/val/test split
   in one tool, and its free tier covers this dataset size).
4. Classes: `door`, `handle` (per decision point #2 — expand later if needed).
5. Export the Roboflow project in YOLOv8 format directly — skip hand-writing
   `door_data.yaml`, Roboflow generates it.

**Deliverable:** a Roboflow project URL + exported dataset zip. Don't commit
the raw images/dataset to this git repo (large binary churn) — note the
Roboflow project link in this file once created.

---

## Step 2: Train (Google Colab free GPU, not local)

**Status: ✅ Done (2026-10-11) — locally via PyTorch MPS, Colab wasn't
needed.** This machine is an Apple M4; trained `yolov8n` for 100 epochs
(`ml/train_door_handle.py`), ~1 epoch/min. The background process was killed
by the OS partway through due to memory pressure, but at epoch 77/100 (past
where the loss curve had already plateaued) — `best.pt`/`last.pt` checkpoints
were saved regardless, so nothing was lost. **Final validation: mAP50 0.864
overall (door 0.886, handle 0.841), mAP50-95 0.604, precision 0.852, recall
0.829** — well above the 0.70 MVP target. Checkpoint:
`runs/detect/ml/runs/door_handle_v1/weights/best.pt`.

Local training isn't assumed to be available (no GPU requirement stated for
this machine). Use Colab's free tier:

1. Open a Colab notebook, `pip install ultralytics`.
2. Fine-tune `yolov8n.pt` (nano — smallest, matches the mobile latency target)
   on the Roboflow export:
   ```python
   from ultralytics import YOLO
   model = YOLO("yolov8n.pt")
   results = model.train(data="data.yaml", epochs=100, imgsz=640, batch=16)
   metrics = model.val()
   print(metrics.box.map50)  # mAP@0.5
   ```
3. **Target for the MVP: mAP@0.5 > 0.70 for `door`**, not the roadmap doc's
   0.85 — that number assumed the 2000-image dataset. Re-evaluate once you see
   real validation numbers on the smaller set; if it's well below 0.70, that's
   the signal to grow the dataset before touching Unity integration at all.
4. Download `best.pt` locally.

**Deliverable:** `best.pt` + the mAP@0.5 / mAP@0.5:0.95 numbers, recorded in
this file (replace this line once trained).

---

## Step 3: Validate Before Touching Unity

**Status: ✅ Done (2026-10-11).** Ran inference on all 133 held-out test
images (`ml/runs/test_predictions/`); spot-checked two visually — both
showed tight, correctly-labeled boxes on door and handle with no false
positives on cluttered backgrounds (tool carts, whiteboards, shelving), and
one correctly found two additional small doors in the far background of a
busy scene. Proceeding to Step 4.

Don't integrate an unvalidated model — the QR scanner debugging this session
cost real time because the environment (XR Simulation) couldn't be trusted
for visual-pipeline testing; same risk applies here, so validate the model
*outside* Unity first, where it's cheap to iterate:

1. Run `model.predict()` against 10–20 real photos *not* in the training set
   (new, never-seen doors/angles).
2. Visually confirm boxes land on `door`/`handle` correctly, no wild false
   positives on walls/furniture.
3. Only proceed to Step 4 once this looks right. This step is pure Python,
   no Unity build required — cheapest place to catch a bad model.

---

## Step 4: Export for Mobile

**Status: ✅ Done (2026-10-11).** Both formats exported from `best.pt`:

| Format | Size | mAP50 overall | door | handle | Speed (Mac CPU, not real device) |
|---|---|---|---|---|---|
| PyTorch baseline | 25 MB | 0.864 | 0.886 | 0.841 | 5.3ms |
| **CoreML (iOS)** — `best.mlpackage` | 6.1 MB | 0.823 | 0.846 | 0.801 | 7.1ms |
| **TFLite INT8 (Android)** — `best_int8.tflite` | 3.3 MB | 0.716 | 0.807 | 0.625 | fast |
| TFLite w8a16 (Android, fallback) — `best_w8a16.tflite` | 3.3 MB | 0.871 | 0.894 | 0.847 | 475ms — too slow for the <50ms target, kept only as a fallback if INT8's handle accuracy proves insufficient on real devices |

Both meet the <20MB size target by a wide margin. **Chose INT8 as the Android
default** — standard quantization hit handle detection much harder than door
(small-object precision loss, a known INT8 effect) but still clears the 0.70
MVP bar overall, and Step 6 below doesn't require handle detection to gate
placement, only to refine it on top of the geometric heuristic. All
speed numbers above are from this Mac's CPU path (TFLite XNNPACK / CoreML
on M4), **not real device numbers** — real on-device latency needs to be
measured once this reaches Step 7.

Per the resolved platform scope (iOS + Android), export both formats:

```python
model.export(format="coreml", imgsz=640, int8=True)
model.export(format="tflite", imgsz=640, int8=True)
```

**Targets** (unchanged from the roadmap doc, these numbers aren't scope-
dependent): model size < 20MB, inference < 50ms per frame on iPhone 12+ /
equivalent Android. Measure both after export for both formats — don't
assume quantization hit the target on either platform just because it did
on one.

**Inference path: Unity Sentis**, confirmed (not just default) — it's the
only practical cross-platform path covering both iOS and Android from one
Unity-side integration, versus writing and maintaining two separate native
bridges (CoreML + a TFLite/NNAPI equivalent). `com.unity.sentis` needs to be
added to `Packages/manifest.json` (Step 5) — this is a new dependency,
justified by the now-confirmed dual-platform scope.

---

## Step 5: Unity Integration

**Status: ✅ Done (2026-10-11), verified against the real compiler and a live
component test, not just written.** `com.unity.ai.inference` 2.6.1 (Sentis's
current name) added to `Packages/manifest.json` and `Aayam.AR.asmdef`'s
references. `Assets/_Project/MLModels/DoorHandleDetector.onnx` imports
correctly as a `ModelAsset` (confirmed input `[1,3,640,640]`, output
`[1,6,8400]`). `Assets/_Project/Scripts/AR/Planes/VisualDoorDetector.cs`
written and verified: compiles with zero errors/warnings in the real
project, and a live instantiation test (model assigned via
`SerializedObject`, `OnEnable` forced) confirmed `ModelLoader.Load` +
`new Worker(...)` run with no exceptions. Hand-rolled NMS + confidence
threshold in C#, since the ONNX export has no built-in NMS. Uses
`Unity.InferenceEngine` namespace (confirmed via live compile — package docs
were inconsistently showing the old `Unity.Sentis` name mid-rename).

Mirrors the roadmap doc's Step 5 code outline, but grounded in this repo's
actual structure:

1. Add `com.unity.sentis` to `Packages/manifest.json`.
2. New file: `Assets/_Project/Scripts/AR/Planes/VisualDoorDetector.cs` in the
   existing `Aayam.AR.asmdef` — sibling to `DoorCandidateDetector.cs`, same
   namespace (`Aayam.AR.Planes`). Don't replace `DoorCandidateDetector` yet —
   run both side by side (see Step 6).
3. Frame source: reuse the same `ARCameraManager.TryAcquireLatestCpuImage`
   pattern already proven working in `QRScanner.cs` (`Assets/_Project/Scripts/
   UI/QRScanner.cs:99-128`) — that conversion code is already correct and
   tested, copy its approach rather than re-deriving it.
4. Run inference every N frames (`_decodeEveryNFrames`-style throttle,
   QRScanner already established this pattern at 8 frames) on a background
   thread, same shape as `QRScanner.DecodeAsync`.
5. Post-process: NMS + confidence threshold, map detection box back to a
   screen-space point, feed that into `ARRaycastManager.Raycast` the same way
   `PlacementReticle.cs` already does, to get a world pose for the detected
   handle/door.

---

## Step 6: Combine With the Existing Heuristic — Don't Discard It

**Status: ✅ Done for the door-confirmation half (2026-10-11); handle-based
anchor refinement deferred on purpose.** `DoorConfidenceCoordinator.cs`
cross-validates `DoorCandidateDetector`'s geometric heuristic against
`VisualDoorDetector`'s camera detection — but via **temporal co-occurrence**
(both signals positive within a ~1s window), not spatial bounding-box
overlap. Reason: the visual detection's box lives in camera-image pixel
space, the geometric candidate's on-screen projection lives in Unity screen
space, and those two spaces don't line up without careful mapping — exactly
the class of bug that cost real time in Phase 2's QR scanner debugging
(`pixelRect` vs `Screen.width/height` mismatches). Temporal co-occurrence
sidesteps that risk entirely while still delivering genuine cross-validation.
Deliberately additive, not gating: `DoorCandidateDetector.HasConfidentCandidate`
stays exactly what HUD placement already uses, unchanged — zero regression
risk. Handle-position-based anchor refinement can't avoid that same
image-to-world mapping problem, so it's deferred to its own dedicated pass
rather than rushed in here.

Wired into the actual scene via `AayamSceneBootstrapper.WireVisualDoorDetection()`
and verified live, not just compiled: `VisualDoorDetector` is attached to the
real AR camera with its model assigned, `DoorConfidenceCoordinator` exists
with both detector references correctly linked — confirmed by inspecting the
actual scene objects after running the bootstrapper, not inferred from code
review. Scene saved.

`DoorCandidateDetector`'s geometric heuristic stays as the fallback/sanity
check, not dead code to delete:

- **Visual detection confirms or downgrades a geometric candidate**: if
  `DoorCandidateDetector.BestCandidate` scores well geometrically AND
  `VisualDoorDetector` also sees a `door` box overlapping that plane's screen
  projection, confidence is high. If only one agrees, fall back to today's
  behavior (geometry alone) rather than blocking placement — this avoids
  Phase 3 regressing Phase 1's already-working Place-button gating.
- **Handle detection is the new capability geometry can't provide at all** —
  use it to refine the anchor point's horizontal position within the door
  plane (today `DoorSurfacePlacer` only snaps height; it has no signal for
  *where on the door* left-to-right), not to gate placement on its own yet.

---

## Step 7: Testing — Expect the Same XR Simulation Limitation as Step 6 (QR)

**Documented lesson from Phase 2, directly applicable here** (see memory
`xr-simulation-qr-scan-testing`): XR Simulation's `TryAcquireLatestCpuImage`
only captures the baked Simulation Environment geometry, not ad-hoc scene
objects. A door/handle detector can be legitimately tested against the
*baked* simulation room's own door/wall geometry (that's real baked content,
not an ad-hoc object) — but don't expect to drop a random door photo texture
into the scene and have the CV pipeline see it, for the same reason the QR
quad never worked.

**Real verification requires a real device test**: point the iPhone at an
actual physical door and confirm detections land correctly. Budget for this
explicitly rather than discovering it late, the way Step 6's QR scan testing
did.

**Latency status: ❌ Target NOT met, measured on real hardware 2026-10-11.**
Added per-inference timing to `VisualDoorDetector.RunInferenceAsync`
(`System.Diagnostics.Stopwatch`, logged every 30 frames as
`[VisualDoorDetector] Step7 latency`) since nothing measured this before.
Built via `xcodebuild` + `devicectl` straight to a connected physical
device — **Kalpan's iPhone (iPhone 17, Apple A19 GPU, Metal)**, not the
simulator — and console-captured live:

| Window | Avg | Max |
|---|---|---|
| frames 1–30 | 63.6ms | 105.3ms |
| frames 31–60 | 63.4ms | 90.5ms |

Consistently ~27% over the <50ms budget, on the `BackendType.GPUCompute`
backend. This is pure inference latency (`Schedule` → `ReadbackAndCloneAsync`
→ `PostProcess`), independent of what's actually in frame — the throttle
runs inference every 8 frames unconditionally, so this number is valid
regardless of whether a real door was in view. **Detection accuracy against
a real physical door is still unverified** — that part does need a human
physically pointing the device at a door, unlike the latency number above.

Options to close the gap, not yet tried: confirm `BackendType.GPUCompute` is
actually the fastest available for this model shape on this chip (Sentis
also offers `CPU`); reduce `_inferEveryNFrames` pressure isn't the fix since
this is per-call latency, not throughput; the model export's `int8` quantization
(Step 4) should be double-checked — re-verify both exports actually quantized
rather than assuming from the export call.

**Accuracy status: ✅ confirmed working on real hardware against a real door
(2026-10-11), after fixing two bugs found along the way.** Checking accuracy
required building verification tooling first (there was none), then finding
and fixing a real bug the tooling immediately surfaced:

1. **No visualization existed.** Added `VisualDetectionDebugLabel.cs`
   (`Assets/_Project/Scripts/UI/`): a screen-space cyan text readout
   ("ML: Door 0.85 | Handle 0.63"), wired to `VisualDoorDetector.OnDetections`.
   Deliberately **not** a world- or camera-image-space box overlay — that
   spatial mapping is the same bug class that cost real time in Phase 2's QR
   scanner debugging, and `DoorConfidenceCoordinator` already sidesteps it
   for the same reason (Step 6).
2. **First wiring attempt was invisible**, parented under `Screen_ARScan`.
   `ScreenManager.Start()` deactivates every screen except `_initialScreen`
   (`ProductBrowser`) — confirmed via a real device screenshot
   (`xcrun devicectl device capture screenshot`, not guessed from editor
   state) showing `StatusText`, another `Screen_ARScan` child, equally
   absent. Fixed by parenting to the Canvas root instead, so it shows
   regardless of active screen — same as the AR camera background and
   `PlacementReticle` already do.
3. **With the label actually visible, it read "ML: no detection" against a
   clear, well-lit, centered door with a visible handle.** Root-caused
   offline rather than guessed: the exact device screenshot, fed through the
   same `best.onnx` via `ultralytics` in Python, detected the door (0.58) and
   handle (0.57) correctly — ruling out the model/export. Rotating that same
   working photo 90° (either direction) reliably dropped detection to zero —
   matching the on-device symptom exactly. Root cause: `VisualDoorDetector`'s
   `XRCpuImage.Convert` uses `Transformation.None`, which hands back the raw
   camera-sensor buffer un-rotated to match the portrait screen — AR
   Foundation doesn't correct this for you on CPU-image reads. The model had
   never once seen an upright door. Fixed with `RotateRgba90Clockwise` in
   `OnCameraFrameReceived`.
4. **Re-verified on-device, same door, same lighting**: label now reads
   `ML: Door 0.85 | Handle 0.63 | Handle 0.48` — correct on the first
   rotation-direction attempt (clockwise). Screenshot evidence captured via
   `devicectl`, not just console logs.

---

## Step 8: Door Boundary Extraction

Originally deferred here as out of scope for the MVP (required polygon/mask
annotations, more expensive than box annotations, and assumed box-detection
was proven first). Started 2026-10-11 at Kalpan's direction, ahead of Step 7's
latency gap being fully closed — noted as a deliberate sequencing override,
not a plan change.

**Segmentation training: ✅ done, real metrics (2026-10-11).** No
polygon-annotated dataset existed, so one was generated rather than
hand-labeled:

1. **Box-to-mask promotion via SAM** (`ultralytics.data.annotator` pattern,
   custom script): fed the *existing* ground-truth box labels (not
   re-detected boxes) to `SAM('sam_b.pt')` as prompts, across all 1334
   images (train/valid/test). ~50 min, backgrounded.
2. **Quality check — two failure modes found and one false start on
   filtering them.** Visual inspection found real SAM failures: a
   person standing in front of a door caused the mask to bleed into their
   silhouette; a glass-paned door's reflections produced jagged noise.
   Tried an automated geometric filter (vertex count + bounding-box fill
   ratio after `cv2.approxPolyDP` simplification, matching the roadmap
   doc's own Step 3 method) to drop bad masks — **the filter didn't
   correlate with actual quality** (a wrong-content blob simplified to
   *fewer* points than a correct door at a steep angle, which has a low
   fill ratio from perspective alone, nothing to do with correctness).
   Abandoned automated filtering; kept all masks except degenerate
   (<3-point) cases, and let validation mAP be the real signal instead of a
   proxy metric.
3. **Trained `yolov8n-seg`** on the result (`ml/train_door_handle_seg.py`,
   same local MPS approach as the box detector). Lands at
   `runs/segment/ml/runs/door_handle_seg_v1/` — note the extra nesting vs.
   the box detector's `ml/runs/door_handle_v1/`; `project="ml/runs"`
   resolved differently for the segment task than it did for detect, a
   quirk not yet root-caused, harmless but worth knowing when looking for
   the weights.
4. **Interrupted once, resumed cleanly.** First attempt died at epoch 24/100
   when its monitoring process was stopped (architecture mistake: training
   was launched as that monitor's direct child, so stopping the monitor
   killed it). Recovered via `model.train(resume=True)` from the epoch-24
   `last.pt` checkpoint — training continued from there, not from scratch.
   Second attempt was launched fully detached with only a polling watcher
   (never a process owner) specifically so this can't happen again.
5. **Final result, epoch 100/100: box mAP50 = 0.877, mask mAP50 = 0.824.**
   Both clear the plan's 0.70 target; box mAP50 even exceeds the roadmap
   doc's "full-scope" 0.85 target. This held despite the known mask noise
   from step 2 — the noise didn't visibly hurt final accuracy at this
   dataset size.

**ONNX export: ✅ done and verified offline (2026-10-12).**
`model.export(format="onnx", imgsz=640)` → 13.2MB, outputs
`(1,38,8400)` detection head + `(1,32,160,160)` mask prototypes (the
standard YOLOv8-seg ONNX shape — more complex than the box detector's single
`(1,6,8400)` output; `VisualDoorDetector.PostProcess` does not yet handle
this format, see below). Verified against two different real photos of
Kalpan's actual door(s), not synthetic data:
- Photo 1 (straight-on): door 0.85, handle 0.85, both masks visually tight
  and correctly follow the real silhouette (door mask even correctly
  excludes the frame where it intrudes top-right and bottom-left).
- Photo 2 (angled hallway shot, two doors visible): both doors (0.73, 0.68)
  and two of three handles (0.84, 0.54) detected correctly, masks clean.

**Geometry extraction + on-device Sentis integration: ✅ done and verified
on-device (2026-10-12).** New code, none of it existed before today:

- `DoorGeometryExtractor.cs` — pure C# (no OpenCV in Unity): Moore-neighbor
  contour tracing + hand-rolled Douglas-Peucker simplification + corner
  ordering + handle-side heuristic, mirroring the roadmap doc's Step 6
  pseudocode. Unit-tested against synthetic masks first (clean rect, skewed
  quad, degenerate triangle, empty mask) before trusting it on real model
  output - caught and fixed a real off-by-one in the contour-closing check
  this way (Moore tracing stops exactly 1px short of closure; the dedup
  check used `<1f`, missing the exact-1.0 case).
- `VisualDoorSegmenter.cs` — loads the seg ONNX via Sentis, decodes the
  two-output YOLOv8-seg format (box+class+32 mask coefficients per anchor,
  plus the 32×160×160 prototype tensor), builds each detection's mask via
  `sigmoid(coeffs · prototypes)` cropped to its box, and feeds the result to
  `DoorGeometryExtractor`.
- **Two real bugs found and fixed via Editor-side testing against actual
  model output, before ever touching the device:**
  1. 160×160 prototype resolution is too coarse for clean 4-vertex
     simplification on its own - fixed by computing the probability map at
     native resolution, then **bilinear**-upsampling it before
     thresholding (nearest-neighbor on an already-binary mask doesn't help,
     verified: scaling 4x nearest still gave 9 vertices, since it just
     scales the same staircase up proportionally rather than smoothing it).
  2. Resizing to a square target was wrong regardless of resolution (160
     through 1280, all gave 7 vertices) - the real fix was resizing to the
     camera frame's true **aspect ratio**, not a fixed square, since the
     square 640×640 model input squashes a non-square portrait frame and
     that distortion stays baked into a square output mask. Confirmed via a
     direct Python comparison: Ultralytics' own 640×640 square mask gave
     the same 7 vertices; only resizing to the true non-square original
     dimensions gave 4 - down to a tiny 95×160, so it's about ratio, not
     pixel count. Also needed a larger simplification epsilon (4%, not the
     roadmap's/box-detector's 2%) - verified via OpenCV on the exact same
     mask bytes that this wasn't a tracer bug: real door masks have a
     legitimate small bump where the handle protrudes past the door's flat
     silhouette, which 2% doesn't smooth away.
- **On-device result** (screenshot, real door, phone held by Kalpan):
  `Geo: 178x495px handle=left conf=0.41` - genuinely difficult case (door
  mostly covered by hanging clothes), model still found a plausible
  door+handle read. No crashes, no exceptions in the device console beyond
  the same pre-existing harmless ones already known from Step 7.
- Added `DoorGeometryDebugLabel.cs` (Canvas-rooted, same reasoning as the
  Step 7 label) to show this on-screen without needing a USB tether.

**World-space anchor refinement: ⚠️ implemented, math verified, visual
on-device confirmation still outstanding (2026-10-12).**

Deliberately avoided projecting camera-image pixels through screen space at
all (the risky mapping `DoorConfidenceCoordinator`'s own doc comment
flagged, the same bug class as Phase 2's QR scanner debugging). Instead:
`DoorGeometryExtractor.HandleNormalized` gives the handle's position as a
(u,v) fraction *within the door quad itself* - scale- and shape-independent,
computed via inverse-bilinear edge projection, unit-tested against 4
synthetic cases (axis-aligned rect, 10%/25% and 90%/50% handle positions,
skewed quad) before trusting it. New `HandleWorldAnchorRefiner.cs` then
converts that fraction straight to world space using the **existing**,
already-trusted geometric placement's `ARPlane` as the coordinate frame
(`plane.transform.TransformPoint(localOffset)`, where `localOffset` is built
from the plane's own `extents`) - confirmed against `ARPlane`'s own source
(`normal => transform.up`; boundary points documented as local-X/Z "plane
space"), not assumed.

- Caught and fixed a repeat of the exact magenta "missing shader" bug
  `PlacementReticle` silently has (root-caused earlier this session,
  2026-10-11): `GameObject.CreatePrimitive`'s default material is Built-in
  Pipeline, not URP. Assigned an explicit `Universal Render Pipeline/Unlit`
  material to the fallback marker instead of repeating it unfixed.
- **First on-device screenshot showed the marker in the wrong place** - door
  correctly read "handle=right", marker appeared near the left door frame.
  Added diagnostic logging rather than accepting or hand-waving this.
- **Debugged via logged real plane data, not guesswork**: computed
  `Cross(plane.transform.up, Vector3.up)` by hand against two independent
  logged `ARPlane` instances and confirmed it exactly equals the logged
  `transform.right` both times - and `Cross(normal, worldUp)` is the
  textbook-correct "viewer's right hand" direction for someone facing the
  plane (derived from Unity's own `right = Cross(up, forward)` identity).
  So `transform.right` already *is* the correct width axis; the original
  code's axis/sign choice was not the bug.
- Most likely explanation for the wrong-looking first screenshot: the test
  door was heavily occluded by hanging clothes (the same door Step 7/8's ML
  models also struggled with), which plausibly confused the *geometric*
  heuristic's own plane-boundary detection into locking onto a mismatched
  plane - a pre-existing geometric-heuristic limitation, not a bug in this
  new code. Unconfirmed: by the time this was investigated, the phone had
  been physically moved (a later screenshot showed a curtain and someone's
  foot), so a clean visual confirmation shot (marker landing on a visible
  handle against an *unobstructed* door) was not obtained this session.

**Update, same day, with Kalpan actively scanning an unobstructed door:**
the marker was visible but landed right-of-center for a confirmed
"handle=left" door - contradicting the theoretical derivation above. Flipped
the X sign empirically and rebuilt; **still wrong after the flip**, ruling
out a simple sign error. Root cause not found: debugging this live,
world-space placement problem through asynchronous screenshots has a
fundamental timing gap (the marker is a static world point; if the phone
moves between the logged computation and the screenshot, a stale position
looks identical to a wrong one) that this session's tooling can't resolve.
**Conclusion: do not trust `HandleWorldAnchorRefiner`'s world-space math as
verified.** Superseded by Phase 4's door coordinate system, which replaces
the ambiguous `ARPlane.transform` local axes this relied on with an
explicitly-defined door frame (true corners as the basis, not plane
extents) - see `phase-4-implementation-plan.md`. The component and its
debug logging are left in place as a reference/fallback, not deleted, but
should not be relied on until re-verified.

**Not done, carried to Phase 4 rather than finished here:** visual
on-device confirmation of correct world-space placement; wiring into actual
product placement (`ObjectPlacementController` is untouched); any visual
box/outline overlay.

---

## Summary: MVP Scope vs. Full Roadmap Doc

| Roadmap doc (`phase-3-door-detection.md`) | This plan's MVP |
|---|---|
| 2000+ images, CVAT/Label Studio | 150–300 images, Roboflow free tier |
| 5 classes (door, door_frame, handle, lock, hinge) | 2 classes (door, handle) |
| mAP@0.5 > 0.85 target | mAP@0.5 > 0.70 target, re-scope if not met |
| YOLOv8 detection + YOLOv8-seg segmentation | Detection only; segmentation deferred (Step 8) |
| ONNX → CoreML + TFLite + Sentis (all platforms) | Same — CoreML + TFLite via Sentis, both platforms (scope confirmed 2026-10-11) |
| Replaces plane-geometry detection | Augments `DoorCandidateDetector`, runs alongside it |

---

## Status

**CLOSED OUT 2026-10-12, handed off to Phase 4.** Steps 1–6 done, Step 7
accuracy confirmed / latency ~53ms (down from 72ms, ~3ms over budget), Step
8 trained + exported + on-device verified, world-space placement attempted
but NOT verified correct (superseded by Phase 4's coordinate system - see
`phase-4-implementation-plan.md`). Overrides the earlier "Step 8 deferred"
sequencing at Kalpan's explicit direction. Dataset trained, exported,
integrated, and combined with the existing heuristic (see each step section
above for dates/evidence).

Step 7, the two halves:
- **Accuracy: ✅ confirmed on real hardware against a real door.** Required
  fixing a real bug along the way — the camera frame fed to the model was
  never rotated to match the portrait screen (`XRCpuImage.Transformation.None`
  hands back raw sensor orientation), so the model had never seen an upright
  door. Fixed; on-screen label now reads correct door/handle detections with
  confidence (0.85 / 0.63) on the actual device. See the Step 7 section above
  for the full root-cause chain and evidence.
- **Latency: ⚠️ much closer to budget, not fully closed.** Timeline:
  pre-rotation-fix ~63ms avg → post-rotation-fix (GPUCompute) 72.1ms/71.9ms
  avg, max ~100ms → **switched to `BackendType.CPU`, re-measured on-device:
  avg settled to 52.9ms, max 53.7ms** after a brief warmup (first window
  66.6ms as the backend spun up, then 65.4 → 60.4 → 52.9ms). CPU backend is
  both faster *and* far more consistent than GPUCompute for this model shape
  on this chip (GPUCompute's max never dropped below ~90ms across any
  window) — counter-intuitive, confirmed by measurement rather than assumed,
  and now the `VisualDoorDetector._backendType` default (was GPUCompute).
  Accuracy re-verified unaffected by the backend switch (screenshot: "ML:
  Handle 0.64 | Door 0.56" on the real door, different angle).

  **`int8` quantization: tried and rejected, not just untried.** Dynamic
  quantization (`onnxruntime.quantization.quantize_dynamic`) produces
  `DynamicQuantizeLinear`/`ConvInteger` ops that Sentis's ONNX importer
  outright rejects (`OnnxImportException`). Static QDQ quantization (proper
  recipe: `quant_pre_process` + 100 real calibration images from
  `datasets/train/images/` + the recommended `QUInt8`/non-per-channel
  settings) loads fine but **collapses to zero detections** on the same test
  photo that detects correctly unquantized — the YOLOv8 detection head is
  evidently too sensitive to naive full-graph activation quantization to fix
  within reasonable effort here. Not pursuing further (would need per-node
  quantization exclusion for the detection head, a materially bigger task).

  Still open if ~53ms isn't good enough: move `RotateRgba90Clockwise` to a
  GPU blit (Sentis's own `TextureTransform` has no rotation support, checked
  directly against its source — would need a custom shader, not a built-in
  option).

**Final scorecard:**
- Box detection (Steps 1–6): ✅ done.
- Step 7 accuracy: ✅ confirmed on real hardware. Latency: ⚠️ 72ms → ~53ms via
  a measured backend switch, still ~3ms over the 50ms budget (needs a
  GPU-blit shader to close further, not attempted — open item).
- Step 8 segmentation: ✅ trained (box mAP50=0.877, mask mAP50=0.824),
  exported, verified on-device (`Geo: 178x495px handle=left conf=0.41` and
  many consistent follow-up reads).
- Step 8 world-space placement: ❌ attempted, NOT verified correct after two
  independent on-device tests and a sign flip. Do not build on
  `HandleWorldAnchorRefiner` as-is. This is the one piece of this phase
  that's a genuine unresolved failure, not just an unstarted nice-to-have —
  carried forward as Phase 4's actual starting problem, not swept under.
- Android: ❌ completely untested — no build, install, or verification has
  touched Android at any point this phase, despite being in scope per the
  plan. Open item, not carried into Phase 4 (Phase 4's roadmap doesn't
  mention platform, this is still a Phase 3 loose end).

**What carries into Phase 4:** the door's 4 corners + normal (from
`DoorGeometryExtractor`, proven correct in 2D/mask space, verified across
many real on-device reads) are exactly Phase 4 Step 1's stated input ("From
CV detection (Phase 3), extract: four corners of the door, door center
point, door normal vector"). Phase 4 replaces the failed ad-hoc
`ARPlane.transform`-based world mapping with an explicit, principled door
coordinate frame — see `phase-4-implementation-plan.md`.
