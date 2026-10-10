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
| Project is iOS/ARKit-only | ⚠️ **`CLAUDE.md` is wrong on this.** `Packages/manifest.json` lists `com.unity.xr.arcore: 6.6.2` alongside `com.unity.xr.arkit: 6.6.2` — ARCore is present. Confirm with Kalpan whether Android is actually in scope before committing to an iOS-only export pipeline (CoreML-only) in Step 4 below. |

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

1. **Platform scope:** iOS-only (CoreML export, skip ONNX/TFLite entirely) vs.
   iOS+Android (needs TFLite too, doubles the export/testing surface). Given
   ARCore is already in the manifest, default assumption below is **iOS-only
   for the Phase 3 MVP**, Android export added later as a Step 4 follow-up —
   confirm or override.
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

Per decision point #1 (iOS-only default):

```python
model.export(format="coreml", imgsz=640, int8=True)
```

If Android is actually in scope (confirm first — see Decision Points), add:
```python
model.export(format="tflite", imgsz=640, int8=True)
```

**Targets** (unchanged from the roadmap doc, these numbers aren't scope-
dependent): model size < 20MB, inference < 50ms per frame on iPhone 12+.
Measure both after export — don't assume quantization hit the target.

**Open question to resolve at this step, not before:** Unity Sentis (cross-
platform, one inference path for iOS+Android, but `com.unity.sentis` isn't
installed yet — would be a new dependency) vs. a native CoreML bridge (iOS-
only, no new Unity package, more native glue code). Given platform scope is
still unconfirmed (Decision Point #1), **default to Sentis** since it keeps
the Android door open cheaply — switch only if Kalpan confirms iOS-only for
good.

---

## Step 5: Unity Integration

Mirrors the roadmap doc's Step 5 code outline, but grounded in this repo's
actual structure:

1. Add `com.unity.sentis` to `Packages/manifest.json` (if Sentis path chosen).
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

---

## Step 8: Door Boundary Extraction (Deferred)

The roadmap doc's Step 3 (segmentation) and Step 6 (boundary/corner
extraction) are **out of scope for the Phase 3 MVP** described above — they
require polygon/mask annotations (more expensive than box annotations) and
assume the box-detection pipeline above is already proven. Revisit only after
Steps 1–7 are working and validated on-device; don't build segmentation on
top of an unvalidated detector.

---

## Summary: MVP Scope vs. Full Roadmap Doc

| Roadmap doc (`phase-3-door-detection.md`) | This plan's MVP |
|---|---|
| 2000+ images, CVAT/Label Studio | 150–300 images, Roboflow free tier |
| 5 classes (door, door_frame, handle, lock, hinge) | 2 classes (door, handle) |
| mAP@0.5 > 0.85 target | mAP@0.5 > 0.70 target, re-scope if not met |
| YOLOv8 detection + YOLOv8-seg segmentation | Detection only; segmentation deferred (Step 8) |
| ONNX → CoreML + TFLite + Sentis (all platforms) | CoreML via Sentis, TFLite deferred pending Android scope decision |
| Replaces plane-geometry detection | Augments `DoorCandidateDetector`, runs alongside it |

---

## Status

**Not started (2026-10-11).** Zero code, dataset, or dependency work exists
yet — this is a plan, not a progress report. Update the step sections above
with dates/results as each is actually completed, following the same
`**Status: ✅ Done (date).**` convention used in `phase-2-product-pipeline.md`.
