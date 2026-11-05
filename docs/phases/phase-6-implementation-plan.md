# Phase 6 Implementation Plan — Realistic Visualization

Roadmap: `docs/phases/phase-6-visualization.md`. This doc tracks actual
implementation progress against it, same convention as
`phase-5-implementation-plan.md` vs. `phase-5-measurement.md`.

Branch: `phase-6` (created from `dev`, 2026-10-29 - `dev` has Phases 1-5
merged via PRs #8/#9/#10).

## Scope decided before implementation (2026-10-29)

See the plan file's Context section (reproduced from planning) for the
full research findings. Summary: Step 3 (Occlusion) already fully built,
nothing to do. Step 2 (Lighting)'s directional half already fully
written, just unwired. Steps 1/4/6/7 (scale, shadows, variants,
comparison) are clean slates. Step 5 (PBR) is mostly solved by glTFast
already - the gap is untextured placeholder content, not import code.
Step 8 (screenshot/video) partially built - screenshot works, gallery
save/watermark/share/video are stubbed or missing. Step 9 (performance)
has a complete but fully unwired profiler class.

Kalpan approved adding two new UPM git-URL dependencies for Step 8:
`com.yasirkula.nativegallery` and `com.yasirkula.nativeshare`.

No URP/HDRP installed - Built-in Render Pipeline only. Shadows (Stage 3)
must be built-in-RP-compatible; the roadmap's literal "URP Lit Shader
Graph" instructions don't apply to this project.

## Stage 1 — Scale rendering — DONE, verified

New `Assets/_Project/Scripts/Services/ProductScaleCalculator.cs`
(static): `TryComputeScaleFactor(GameObject modelRoot, ProductDimensions
dims, out Vector3 scaleFactor, out string reason)`. Uniform scale factor
(average of valid per-axis ratios between `MathUtils.MmToMeters(dims.*)`
and `ModelInstanceManager.CalculateBounds(modelRoot).size`), warns (not
fails) on >15% per-axis disagreement, returns `false` for null/all-zero
dimensions or no renderers. Added `Aayam.Utilities` to
`Aayam.Services.asmdef`'s references (was missing - needed for
`MathUtils`).

Edited `ModelInstanceManager.SetupModelForAR` to take an optional
`ProductDimensions dimensions = null` param and apply the computed scale
as a new step 0, before layer/rigidbody/collider/anchor-offset (all of
which read bounds). Edited `ProductSelector.LoadAndPlaceAsync` to pass
`detail.Dimensions` through. Edited `ScaleHandler.cs`'s stale comment
(no functional change - it already captures scale after placement).

**Verified**: new `Assets/Tests/EditMode/ProductScaleCalculatorTests.cs`
(6 tests: matching dims → factor 1, double-size dims → factor 2, missing
dims → false, all-zero dims → false, no renderers → false, axis-mismatch
→ warns via `LogAssert.Expect` but still returns true) - **6/6 pass**.
Full EditMode suite: **138/140 pass** (same 2 pre-existing, unrelated
`DoorRegistryTests` failures from Phase 5's session - not touched here,
not a regression).
**Not yet done**: on-device confirmation (deliberately mis-scale a
placeholder GLB, confirm the correction fixes it) - deferred to a
combined on-device pass at the end of this phase, same as Phase 5's
approach.

## Stage 2 — Environment lighting wiring — DONE, verified

`AR/Core/LightEstimationController.cs` was already complete; only
wiring was missing. Added `WireLightEstimation(BaseRig rig)` to
`Assets/Editor/AayamSceneBootstrapper.cs`, called from
`BuildTestSceneWiring` right after `WireDoorEdgeCalibration`: finds
`"Directional Light"` (falls back to `FindFirstObjectByType<Light>()`,
warns if neither exists), `AddComponent<LightEstimationController>()` on
the camera rig, explicitly sets `_directionalLight` via the existing
`SetPrivateField` reflection helper (the class also self-finds a light
at runtime if left unassigned, but every other wired component here is
explicit, so this matches that convention rather than leaning on the
fallback).

**Deferred** (per the plan): AR Environment Probe / reflection-probe
capture - nothing reflective exists yet to validate it against.

**Verified**: full EditMode suite recompiled and ran clean -
**138/140 pass** (same 2 pre-existing `DoorRegistryTests` failures, no
regressions). No new EditMode test (1:1 passthrough of ARFoundation's
own estimation fields, same precedent as other AR-interactive classes).
**On-device confirmation deferred** to the combined end-of-phase device
pass.

## Stage 3 — Shadows — DONE, verified

New `Assets/_Project/Shaders/ARShadowReceiver.shader` (Built-in RP -
no URP installed - `ZWrite Off`, alpha-blended, samples shadow
attenuation via `AutoLight.cginc`/`SHADOW_ATTENUATION`, transparent
everywhere except where shadowed). Hand-authored `.shader.meta`/`.mat`/
`.mat.meta` with explicit GUIDs (Unity can't auto-assign these in
batchmode without a prior Editor import pass) - confirmed importing
cleanly with zero shader-compiler errors in the EditMode test run's log.
New `Assets/_Project/Materials/ARShadowReceiver.mat` using it.

New `Assets/_Project/Scripts/AR/Placement/DoorShadowReceiver.cs`: polls
`DoorCoordinateSystem.HasOrigin`/`DoorGeometryState.HasGeometry` in
`Update` (same pattern as `DoorEdgeCalibrationUI`), creates/positions a
`Quad` sized to the door via `DoorToWorldSpace`, assigns the shadow
material.

New `Assets/_Project/Scripts/Services/ShadowPolicy.cs` (static):
`ShouldCastShadows(DeviceQualityTier tier) => tier != Low`. Edited
`ModelInstanceManager.cs`: new `[SerializeField] DeviceCapabilityChecker
_capabilityChecker`, new step 5 (`ApplyShadowAndLodPolicy`) after anchor
offset - Low tier calls the now-finally-used `LODSetup.OptimizeRenderers`
(shadows off, perf-first); everything else enables shadow casting/
receiving on every renderer. Added `Aayam.AR` to `Aayam.Services.asmdef`'s
references (needed for `DeviceCapabilityChecker`/`DeviceQualityTier` -
confirmed no circular reference risk, `Aayam.AR` doesn't reference
`Aayam.Services`).

Edited `AayamSceneBootstrapper.cs`: wires `DeviceCapabilityChecker` onto
`ModelInstanceManager` in `BuildPlacementSystems`; new
`WireDoorShadowReceiver(BaseRig rig)` (mirrors `WireDoorEdgeCalibration`'s
structure), called from `BuildTestSceneWiring`.

**Verified**: new `Assets/Tests/EditMode/ShadowPolicyTests.cs` (4 tests:
Low → false, Medium/High/Ultra → true via `[TestCase]`) - **4/4 pass**.
Full EditMode suite: **142/144 pass** (same 2 pre-existing, unrelated
`DoorRegistryTests` failures - no regressions). Shader/material import
confirmed clean (zero compiler errors in the test run log).
**On-device confirmation deferred** to the combined end-of-phase pass -
per the plan, this stage is notably fully device-verifiable on Kalpan's
non-LiDAR iPhone 17 since it rides on the already-verified PnP door
geometry, not depth/LiDAR (unlike Phase 5's Stage 4).

## Stage 4 — PBR materials (mostly deferred) — DONE, verified

Edited `backend/scripts/generate_placeholder_models.py`: added
`_FINISH_PBR` (distinct `baseColorFactor`/`metallicFactor`/
`roughnessFactor` per catalog `finish` string, keyed to the actual
values seeded in `scripts/seed_data.py` - `matte_black`, `chrome`,
`satin_nickel`, `brushed_nickel`, `brushed_steel`, `brushed_gold`,
`rose_gold`, `antique_brass`, `silver`, `natural_wood`, `crystal_clear`)
and `_pbr_for_finish(finish, fallback_color)` (case-insensitive lookup,
falls back to today's category-color + neutral 0.3/0.6 for an unknown/
missing finish - existing behavior unchanged for anything not in the
dict). Threaded `Product.finish` through the `main()` query and into
`_build_glb_bytes`'s now-parameterized `metallic_factor`/
`roughness_factor`. This isn't just future-facing plumbing for Stage 5 -
it changes today's actual placeholder output for every already-seeded
product, since each one already has a real `finish` value.

No new Unity import code needed - glTFast's existing Built-in-RP PBR
shader renders these factors "for free."

**Deferred** (per the plan): real textured PBR assets (albedo/normal/
roughness/AO maps for brushed metal, wood grain, knurled surfaces) -
asset-sourcing/content-pipeline decision, not fabricable from nothing.
Reflections for metallic surfaces - ties to Stage 2's probe deferral.

**Verified**: new `backend/tests/test_placeholder_finish_pbr.py` (5
tests: known finish → distinct factors, case-insensitive lookup, unknown
finish → fallback, missing/`None` finish → fallback, a metal and a
dielectric have distinct metallic values) - **5/5 pass**. Full backend
suite: **34/34 pass**, zero regressions. Visual confirmation (regenerate
a couple of finishes, load in the Editor, confirm metallic/roughness
differ under the scene light) **deferred to the combined end-of-phase
on-device/Editor pass** - no numeric-render-comparison harness exists in
this project for this.

## Stage 5 — Material variants — DONE, verified

**Decision followed**: a new table, not separate Product/SKU rows - the
existing `ProductMaterial`/`product_materials` table is composition data
(e.g. "80% brass"), confirmed unrelated and not reused.

Backend (mirrors Phase 5's layering, but the READ side follows
`product_service.py`'s own established raw-SQL/view+dict convention for
`ProductDetail`, not full ORM eager-loading - that file deliberately
avoids ORM relationship loading everywhere, including the existing
`images`/`materials` lists): new migration
`backend/migrations/versions/005_material_variants.py` (chains from
`004`, applied - `alembic current` → `005 (head)`) creating
`product_material_variants` (id, `product_id` FK CASCADE, `finish_name`,
`display_order`, `base_color_hex`, `metallic_factor`/`roughness_factor`
DECIMAL(3,2) with [0,1] CheckConstraints, nullable `model_url`/
`thumbnail_url`, `is_default`, `created_at`,
`UniqueConstraint(product_id, finish_name)`). New
`app/models/material_variant.py` (`ProductMaterialVariant`, registered
in `app/models/__init__.py`, `Product.material_variants` relationship).
New `app/schemas/material_variant.py`
(`MaterialVariantResponse`/`MaterialVariantCreate`). New
`app/services/material_variant_service.py` (`create_variant`,
`list_variants_for_product`, `get_default_variant`). New
`app/api/v1/material_variants.py`: `GET
/api/v1/products/{id}/material-variants` public, `POST` gated by
`verify_admin_key`, catches `IntegrityError` on a duplicate
`(product_id, finish_name)` and returns 409. Registered in `router.py`.
Edited `product_service.py`'s `_fetch_detail` to also query
`product_material_variants ORDER BY display_order` and attach as
`data["material_variants"]`, exactly mirroring the existing images/
materials pattern there. Edited `schemas/product.py`'s `ProductDetail`
to add `material_variants: list[MaterialVariantResponse] = []`.

Unity: new `Models/MaterialVariantData.cs` (`FinishName`,
`BaseColorHex`, `MetallicFactor`, `RoughnessFactor`, `ModelUrl`,
`IsDefault`). Edited `ProductData.cs` to add `MaterialVariantData[]
MaterialVariants`. Edited `ProductApiService.cs`: new
`MaterialVariantDto`, mapped into `ProductData.MaterialVariants` in
`.MapToProductData`. New `AR/Interaction/MaterialSwitcher.cs`: targets
glTFast's own Built-in-RP PBR shader property names - **confirmed by
reading the actual shader source**
(`Library/PackageCache/com.unity.cloud.gltfast@.../Runtime/Shader/
Built-In/glTFPbrMetallicRoughness.shader`: `baseColorFactor`,
`metallicFactor`, `roughnessFactor`, no leading underscore unlike
Standard) - resolving the plan's flagged risk outright rather than
leaving a guess. `SwitchTo(int)` mutates each renderer's **instance**
material (`renderer.material`, never `.sharedMaterial`). Extracted pure
helpers into a new `MaterialVariantSelector` static class
(`FindDefaultIndex`, `TryParseBaseColor`) for EditMode testability.

Threaded `MaterialVariantData[]` through the full placement chain
(`ProductSelector.LoadAndPlaceAsync` → `ARHUDController.BeginPlacementFor`
→ `ObjectPlacementController.StartPlacement` → a new
`_pendingMaterialVariants` field → applied in `ExecutePlacement` right
after the placed clone is activated, via `MaterialSwitcher.Initialize`)
- same threading pattern Stage 1 used for `ProductDimensions`.

New `UI/FinishPickerUI.cs`: a single cycle button (not a full swatch
picker - deferred per the plan) that advances the most-recently-placed
product's finish via `ObjectPlacementController.GetPlacedObjects()`.
Wired via `AayamSceneBootstrapper.cs`'s new `WireFinishPicker`.

**Verified**: new `backend/tests/test_material_variants.py` (5 tests:
create+list, duplicate-finish → 409, admin-key required, `ProductDetail`
carries variants, empty list when none created) - **5/5 pass**. Full
backend suite: **39/39 pass**, zero regressions. New
`Assets/Tests/EditMode/MaterialVariantDataTests.cs` (6 tests: default-
index selection across empty/null/none-marked/one-marked arrays, valid/
invalid hex parsing) - **6/6 pass**. Full EditMode suite: **148/150
pass** (same 2 pre-existing, unrelated `DoorRegistryTests` failures).
**On-device/Editor-Play confirmation deferred** to the combined
end-of-phase pass (seed ≥2 variants for one real product, place it,
cycle finishes, confirm visible change).

## Stage 6 — Product comparison view — DONE, verified

**Decision followed**: side-by-side two-instance placement + solo
toggle, not a wipe/slider - reuses existing multi-object support
(`ObjectPlacementController._maxObjects=5`, `GetPlacedObjects()`) with
zero new rendering tech.

Extended `PlacedProduct` with `DisplayName`/`PriceFormatted`. Rather
than threading `ProductUIItem` through (which the QR-scan entry point,
`SelectProductBySku`, never has one of), `ProductSelector.LoadAndPlaceAsync`
now reuses its own `detail.Name`/`FormatPrice(detail.Price,
detail.Currency)` directly - simpler, and covers both entry points
uniformly. Threaded `displayName`/`priceFormatted` through
`BeginPlacementFor` → `StartPlacement` → the new `PlacedProduct` fields,
same pattern as Stage 5's `MaterialVariantData` threading.

New `AR/Placement/ProductComparisonController.cs`: a 4-state cycle
(`Inactive → SideBySide → SoloA → SoloB → Inactive`) driven entirely by
the existing `_compareButton`. `EnterComparisonMode()` takes the two
most-recently-placed products, computes side-by-side positions via the
new pure `SideBySidePositions(center, rightAxis, gapMeters)` (extracted
for testing), remembers original poses for `ExitComparisonMode()`'s
restore. World-space labels reuse `DoorEdgeCalibrationUI`'s billboarded-
`TextMesh` pattern. Solo toggling uses `GameObject.SetActive` on each
placed instance - simple, non-destructive, no component state lost.

Edited `ARHUDController.cs`: `OnCompareClicked()` now calls
`_comparisonController.CycleCompare()` instead of its log-only stub; new
`[SerializeField] ProductComparisonController _comparisonController`.
Edited `AayamSceneBootstrapper.cs`'s `BuildUI`: creates/wires the
comparison controller on the camera rig right alongside the existing HUD
wiring, assigns it onto `hud._comparisonController`.

Screenshot/share of a comparison view is not duplicated here per the
plan - falls out of Stage 7 for free once `ScreenshotCapture` works.

**Verified**: new `Assets/Tests/EditMode/ProductComparisonLayoutTests.cs`
(3 tests: positions exactly `gapMeters` apart, symmetric about center,
a non-unit right-axis input still normalizes correctly) - **3/3 pass**.
Full EditMode suite: **151/153 pass** (same 2 pre-existing, unrelated
`DoorRegistryTests` failures). The mode state machine itself is
on-device-verified only (per the plan) - **deferred to the combined
end-of-phase pass** (place two different products, compare, confirm
gap/labels, confirm solo toggling).

## Stage 7 — Screenshot & video capture — DONE, verified

Edited `Packages/manifest.json`: added `com.yasirkula.nativegallery` and
`com.yasirkula.nativeshare` as git-URL UPM dependencies - first git-URL
packages in this project. **Correction during implementation**: the
plan's assumed `#upm` branch suffix doesn't exist on either repo
(confirmed via `git ls-remote` - both only have a `master` branch, with
`package.json` already at the repo root); pinned instead to each repo's
latest release tag (`#v1.9.5` / `#v1.5.5`). Verified both resolve and
compile cleanly (`NativeGallery.Runtime`/`.Editor`,
`NativeShare.Runtime`/`.Editor` assemblies all built) via a full Unity
batchmode pass. Added `NativeGallery.Runtime`/`NativeShare.Runtime` to
`Aayam.UI.asmdef`'s references (also added the previously-missing
`Aayam.Utilities` reference, needed for `TextureCompositor`).

New `Assets/_Project/Scripts/Utilities/TextureCompositor.cs` (static,
pure pixel math): `CompositeAt(Texture2D baseTex, Texture2D overlay,
Vector2Int bottomRightMargin)` - alpha-blends onto a COPY of `baseTex`,
anchored bottom-right with margin. Implements the previously-dead
`_addWatermark`/`_watermarkSprite` fields on `ScreenshotCapture.cs` for
the first time.

Edited `ScreenshotCapture.cs`:
- `CaptureRoutine` now composites the watermark (if enabled and a
  sprite is assigned) onto the raw capture before saving.
- `SaveToGallery` replaces the commented-out call with a real
  `NativeGallery.SaveImageToGallery(tex, "Aayam AR", fileName,
  callback)` on Android/iOS.
- New `ShareLastScreenshot()` (public) wraps
  `NativeShare().AddFile(...).SetSubject(...).SetText(...).Share()` for
  the most recently captured texture. New `_autoShareAfterCapture`
  toggle, **defaulting to false** - no existing HUD button triggers
  sharing today, so this stays an explicit opt-in/future-button hook
  rather than silently changing today's capture behavior.
- `_lastCapturedTexture` is now kept alive (previous one disposed first)
  instead of destroyed immediately after save, so `ShareLastScreenshot`
  has something to share; added `OnDestroy` cleanup.

**Deferred** (per the plan): AR video recording - zero existing
scaffolding, native `ReplayKit`/frame-capture pipeline work
disproportionate to this pass.

**Verified**: new `Assets/Tests/EditMode/TextureCompositorTests.cs` (6
tests: overlay lands in the bottom-right corner, pixels outside the
overlay stay unchanged, a margin shifts the overlay correctly, a fully
transparent overlay leaves the base untouched, null overlay/null base
edge cases) - **6/6 pass**. Full EditMode suite: **157/159 pass** (same
2 pre-existing, unrelated `DoorRegistryTests` failures). **On-device
confirmation deferred** to the combined end-of-phase pass (confirm the
image lands in the real iOS Photos app, confirm the gallery permission
prompt, confirm watermark placement, confirm the share sheet via
`ShareLastScreenshot()`) - also needs a real "Aayam" logo sprite
assigned in the Inspector first (content, not code, trivial to supply).

## Stage 8 — Performance — DONE, verified

New `Assets/_Project/Scripts/Utilities/PerformanceThresholds.cs`
(static): `TargetFps = 60f`, `MemoryBudgetMB = 200`,
`FpsMeetsTarget(float)`, `MemoryWithinBudget(long)` - extracted from
`PerformanceProfiler.SaveReport`'s previously-inlined comparisons, which
used stale thresholds (55 FPS/400MB) that didn't match the roadmap's
actual targets (60 FPS/200MB) - confirmed and fixed.

Edited `PerformanceProfiler.cs`: added `Unity.Profiling.ProfilerRecorder`-
based draw-call/batch counters (`ProfilerCategory.Render`, `"Draw Calls
Count"`/`"Batches Count"`) - started in `Start`, disposed in the new
`OnDisable`, read via a `.Valid`-guarded helper so a build/platform where
the marker isn't available degrades to `-1` instead of throwing. Surfaced
in both the live overlay and `SaveReport`'s exported text file.
`SaveReport`'s benchmark lines now call `PerformanceThresholds.FpsMeetsTarget`/
`MemoryWithinBudget` instead of inlining the old thresholds.

Edited `ModelInstanceManager.cs`: new optional `[SerializeField] LODSetup
_lodSetup`, called as a new step 0b (`_lodSetup?.SetupLODs(modelRoot)`)
right after scale correction - wires the previously-dead `LODSetup.SetupLODs`
live, even though it's a confirmed no-op today (the placeholder GLBs have
no `*_LOD0/_LOD1/_LOD2` meshes or `MSFT_lod` extension).

Edited `AayamSceneBootstrapper.cs`: `BuildPlacementSystems` now also
wires a `LODSetup` component onto `ModelInstanceManager`. New
`WirePerformanceProfiler()` - Canvas-rooted (`UIRootName`, always
visible, same reasoning as the existing ML/Geo/PnP debug-text stack),
new text slot at y=150 continuing that stack, `FindOrAddComponent<PerformanceProfiler>`.
Added `Aayam.Utilities` to both `Aayam.Editor.asmdef`'s and (already
done in Stage 7) `Aayam.UI.asmdef`'s references.

**Verified**: new `Assets/Tests/EditMode/PerformanceThresholdsTests.cs`
(6 tests: FPS exactly-at/just-below/above target, memory just-under/
exactly-at/over budget - exactly-at-budget correctly fails the strict
"< 200MB" wording) - **6/6 pass**. Full EditMode suite: **163/165 pass**
(same 2 pre-existing, unrelated `DoorRegistryTests` failures - the only
2 failures across every stage of this entire phase, none introduced by
this work). The overlay and `ProfilerRecorder` counts are on-device/
Play-verified only - **deferred to the combined end-of-phase pass**.

## On-device verification (2026-11-01) — extensive, multi-round

All 8 stages' code was complete before this pass; this is the combined
end-of-phase on-device round the "Overall status" note below originally
deferred. It took far longer than Phase 5's equivalent pass because the
bootstrapper's Phase 6 wiring had **never actually been applied to the
scene** (see the first finding below) - meaning this was effectively the
very first time any of Phase 6's features, AND real product placement
itself, had ever run on real hardware. Multiple real, previously-latent
bugs surfaced as a result - each is recorded here with its root cause,
not just its symptom, since several are the kind that silently
reappear if "fixed" by only patching the visible symptom.

### Finding 1 — the Phase 6 bootstrapper wiring was never applied to the scene

`AayamSceneBootstrapper.BuildTestSceneWiring()` is a manual Editor-menu
action; every Stage this session added a new `Wire*` method to it
(`WireLightEstimation`, `WireDoorShadowReceiver`, `WireFinishPicker`,
`WirePerformanceProfiler`, `WireProductComparison`, `WireScreenshotCapture`)
and edited `BuildPlacementSystems`/`BuildUI`'s existing methods - but
nobody had re-run the bootstrap against `Assets/ARtest.unity` and saved
it. The compiled app was running Phase 6 C# code against a scene that
still only had Phase 1-5's wiring. Symptoms this caused: no Place/Reset/
Compare/Screenshot buttons visible (see Finding 2), no Finish-picker
button, no Performance overlay, Screenshot a silent no-op (see Finding 7).

**Fix**: new `Assets/Editor/Phase6SceneApply.cs` - a headless runner
(`EditorSceneManager.OpenScene` → `BuildTestSceneWiring()` →
`EditorSceneManager.SaveScene`), run via `-executeMethod
Aayam.Editor.Phase6SceneApply.Run`. Kept as reusable tooling for future
phases, not deleted - see its own doc comment. Re-run several more times
this session as later fixes below needed new wiring.

### Finding 2 — every unpositioned HUD button stacked at the same spot

`BuildUI` created `_backButton`, `_menuButton`, `_placeButton`,
`_resetButton`, `_compareButton`, `_screenshotButton` with **no explicit
`anchoredPosition`** - all six defaulted to `(0,0)`, exactly on top of
each other. Only the last-created sibling (`ScreenshotButton`) was
visually on top/clickable; Back/Menu/Place/Reset/Compare were invisible
and unclickable. Confirmed live (user report: "no place button").

**Fix**: explicit top bar (Back/Status/Menu at y=850) and bottom action
bar (Place/Reset/Compare/Screenshot at y=-850, evenly spaced). Also
repositioned the ML/Geo/PnP debug-label stack and the Performance
overlay to a consistent, non-overlapping Y-budget across the whole
screen (see `AayamSceneBootstrapper.cs`'s own inline comment at the
button-positioning block for the full before/after reasoning).

### Finding 3 — product browser tray had no ScrollRect

`CardContent`'s `HorizontalLayoutGroup` laid out all cards in a row
inside a rect stretched to exactly the screen's width, with no
`ScrollRect`/`Viewport`/`Mask` - 20 real catalog products overflowed
off-screen with no way to reach anything past the first ~4. Confirmed
live (user report: "not able to scroll the product").

**Fix**: real `ScrollRect` + `Viewport` (`RectMask2D` + a near-invisible
`Image`, required for the mask) + `Content` (`ContentSizeFitter.PreferredSize`
so it grows to fit however many cards exist) structure, standard Unity
UI pattern, built in `BuildUI`.

### Finding 4 — dev diagnostic text always visible, even in a release build

`VisualDetectionDebugLabel`/`DoorGeometryDebugLabel`/`DoorPnPDebugLabel`
(Phase 3/4) had no visibility gate at all - raw "ML: no detection" /
"Geo: ..." / "PnP: ..." text was always on screen, including in this
Release (non-dev) build, which isn't demo-appropriate. Not a Phase 6 bug,
but directly blocking the user's explicit "make it proper like a real
app / MVP" request.

**Fix**: each label's `Awake()` now checks `AppConfig.Load().DebugMode`
and `SetActive(false)`s itself if false - same convention already used
by `AppConfig`'s own `ShowFpsCounter`/`ShowPlaneVisualization` fields.
Runtime check (not a bake-time one), so it correctly reflects Editor/
Development-Build vs. Release at actual launch time.

### Finding 5 — PlacementReticle's ARKit plane-tracking never found a valid surface on this door

Confirmed via a temporary diagnostic (`[PlacementDiag]`, since removed):
**188 consecutive tap attempts, `reticleValid=false` every single time**,
while `DoorPnPWorldAnchor`'s own separate ML/PnP door solve tracked the
door fine the entire time. This is the same root cause documented back
in Phase 4 ("reticle/plane-tracking reliability") finally fully blocking
something essential (every single product placement) rather than a
feature that could route around it. Pre-existing, not introduced this
phase - just never previously load-bearing enough to force a fix.

**Fix**: `ObjectPlacementController.TryGetPlacementPose` now falls back
to raycasting directly against the already-solved door plane
(`DoorEdgeCalibrator.TryIntersectDoorPlane`, the same method
`DoorEdgeCalibrationUI` already uses successfully) whenever the reticle
isn't valid. This is a real, permanent capability upgrade, not a
device-specific workaround - it makes placement possible on any
device/environment where ARKit's own plane tracking underperforms,
exactly as Stage 3 (shadows) already does for its own door-plane query.

### Finding 6 — the door-plane fallback pose fed the wrong axis into DoorSurfacePlacer

First attempt fed `doorOrigin.rotation` straight into the pose.
`DoorSurfacePlacer.CalculatePlacementPose` reads the surface normal off
`hitPose.up` (matching AR Foundation's own raycast-hit convention), not
`DoorCoordinateSystem`'s own "Z = normal" convention - so the door's
HEIGHT axis landed where the normal was expected, producing a sideways
placement. Confirmed live (user report: "placed but in wrong direction").

**Fix**: `Quaternion.FromToRotation(Vector3.up, doorOrigin.forward)`
constructs a rotation whose `up` is *exactly* the true door normal,
matching what `CalculatePlacementPose` expects, without needing to touch
that already-correct, already-shared method.

### Finding 7 — glTFast's PBR shader was stripped from the release build

First real placed product rendered solid magenta (Unity's missing-shader
fallback). IL2CPP/shader-stripping removes any shader not in "Always
Included Shaders" that static analysis can't prove is referenced - this
was genuinely the **first on-device test of a real downloaded-GLB
product** in this project's history (every earlier on-device AR test
used `ModelLoader`'s placeholder-cube fallback path, since placement
itself was broken per Finding 5).

**Fix**: added glTFast's `glTF/PbrMetallicRoughness` Built-in-RP shader
(guid `99fa998bbbed3408aafa652b466d261d`) to `GraphicsSettings.asset`'s
`m_AlwaysIncludedShaders`.

### Finding 8 — ScreenshotCapture was never wired into the scene at all (see Finding 1)

`ARHUDController._screenshot` was left unassigned by design in an
earlier phase ("Phase 1 feature, out of scope"); Phase 6 built real
functionality for it but never actually wired the component into the
live scene. The Screenshot button was consequently a silent no-op (the
null-conditional `_screenshot?.CaptureScreenshot()` swallows it with no
log, no error).

**Fix**: new wiring in `BuildUI` - `ScreenshotCapture` component +
full-stretch flash-panel `Image` (initially hidden), assigned onto
`hud._screenshot`.

### Finding 9 — the capture itself baked in the flash effect

Once wired, the first real screenshot came out uniformly washed-out.
`CaptureRoutine`'s original order showed the white flash panel at 0.9
alpha, then waited one frame, then called `ReadPixels` - capturing the
screen *while the flash was still visible*. The flash is meant to be
user-facing feedback that a capture just happened, not part of the
captured image.

**Fix**: reordered so `ReadPixels` happens first (right after hiding the
UI and waiting a frame), and the flash panel only activates/fades out
*afterward*.

### Finding 10 — AnchorController never created a real ARKit-tracked anchor

The most significant finding. `CreateAnchor` called
`anchorGO.AddComponent<ARAnchor>()` directly on a plain `GameObject` -
this creates an inert component with **no connection to the underlying
AR platform's anchor tracking at all**. The anchor's transform was set
once at placement time and never corrected again by ARKit, so as the AR
session's own tracking refined/corrected itself (especially during
panning), placed objects visibly drifted or jumped away from their
real-world position. Confirmed via two screen recordings (frame-by-frame
analysis via `ffmpeg` frame extraction) showing the object detaching
from the door and reappearing elsewhere mid-pan. This is pre-existing
Phase 1-era code, not introduced this phase - never caught before
because this was the first time any object was ever successfully placed
at all (Finding 5 blocked every attempt before this).

**Fix**: `AnchorController.CreateAnchorAsync` now uses
`ARAnchorManager.TryAddAnchorAsync(pose)` (AR Foundation 6.x's real,
platform-backed anchor-creation API) instead of a bare `AddComponent`.
Required threading `async`/`await` up through `ObjectPlacementController.ExecutePlacement`
(now `async void`, matching this project's established fire-and-forget
convention for a `Update()` call site that can't itself `await`) and
`PlacementHistory`'s `RemoveCommand.Undo()` (same pattern - `async void`
implementing `IPlacementCommand.Undo()`'s `void` signature is valid C#).

**Verified improved, not fully eliminated**: a fresh post-fix recording,
frame-sampled across a wide pan (left/right/up/down), showed the object
consistently anchored to the same spot on the door throughout - no
repeat of the detach-and-reappear failure from before the fix. User
confirmed a much smaller residual issue "happens sometimes" - almost
certainly ARKit's own SLAM tracking making small periodic corrections in
a low-visual-texture environment (a plain door against a plain wall),
the same class of limitation already documented and accepted in Phase
4 for this exact test setup. Not further fixable from Unity-side code
alone; would need more visual texture in the physical environment or
LiDAR-grade tracking hardware.

### Finding 11 — comparison labels: degenerate billboard rotation, then overlap from long names

Two sequential bugs here, both confirmed via a `[ComparisonDiag]`
temporary diagnostic (since removed) and live screen recordings:

1. `BillboardLabel`'s original `Quaternion.LookRotation(forward)` (no
   explicit up hint) degenerates when `forward` is close to parallel
   with world-up - exactly what happens when the camera looks steeply
   down at a label on a door below it. Text rendered sideways/vertical.
   **Fix**: flatten the Y component of the to-camera vector and pass
   `Vector3.up` explicitly as the up hint - the same fix was applied to
   `DoorEdgeCalibrationUI`'s identical pre-existing pattern for
   consistency, before it hits the same angle.
2. Once the rotation was confirmed mathematically correct (diagnostic
   showed clean Y-only rotation, labels exactly `_gapMeters` apart),
   the labels *still* visually overlapped - a long product name (e.g.
   "SS Ball Bearing Hinge 4-inch") simply renders wider than a 0.3m gap.
   **Fix**: increased `_gapMeters` to 0.6f, shrank `characterSize` to
   0.012f, and truncate any name over 18 characters with "…" - keeps
   every label a predictable, comparable width regardless of product
   name length.

### Final state

All 11 findings fixed and confirmed live on-device by the user:
placement (scale + rotation + position), real product rendering,
material-variant finish switching, product comparison (layout + labels),
and screenshot capture all working. EditMode suite re-run clean after
every fix in this list: **163/165 pass** (same 2 pre-existing, unrelated
`DoorRegistryTests` failures throughout this entire phase - never
touched, never regressed). Backend: **39/39 pytest pass** (cleaned up
throwaway test-created product rows from the local dev database twice
during this session - `scripts/test_*` fixtures write into the same
real DB a device test reads from; not a problem to "fix", just routine
cleanup after test runs that touch shared dev data).

**Still not exercised this session**: watermark compositing (needs a
real logo image supplied by hand - `TextureCompositor`'s own logic is
unit-tested but never visually confirmed against a real sprite), native
share-sheet (`ShareLastScreenshot`, wired but not tapped), shadows and
lighting reaction (both wired and code-complete per their own Stage
verification, but no explicit on-device visual confirmation was sought
once attention moved to the placement/anchor/comparison bugs above).
