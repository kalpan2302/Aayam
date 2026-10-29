# Phase 5 Implementation Plan — Automatic Measurement

Roadmap: `docs/phases/phase-5-measurement.md`. This doc tracks actual
implementation progress against it, same convention as
`phase-4-implementation-plan.md` vs. `phase-4-door-coordinate-system.md`.

Branch: `phase-5` (created from `phase-4`, 2026-10-29 - `dev`/`main` don't
have Phase 4's work merged yet).

## Scope decided before implementation (2026-10-29)

Two things reshape this phase relative to the roadmap's literal wording:

1. **Much of the client-side groundwork already exists** from Phase 4 and
   earlier, unrelated work: `AR/Occlusion/*` (`OcclusionController`,
   `DepthTextureHandler`, `MeshingController`) already acquires
   environment-depth/LiDAR data for occlusion rendering, just never
   sampled for distance. `Core/DeviceCapabilityChecker.cs` already tiers
   devices. Phase 4's `DoorEdgeCalibrator`/`DoorEdgeCalibrationUI`/
   `DoorCalibration` already implement the mechanical core of roadmap
   Steps 3-4 (tap two points on a solved plane, manual entry, compute
   distance) - narrowly for scale correction only, no connecting
   measurement line, no persistence. `Models/CompatibilityResult.cs`
   already defines `DoorProperties` (width/height/thickness + per-
   dimension confidence) and a `MeasurementSource` enum (`LiDAR, ARDepth,
   CVMonocular, UserAssisted, ManualInput`) matching the roadmap's Step 6
   tiers almost exactly - just never populated by anything.
2. **Storage/history (Step 7) is greenfield on the backend.** The real
   backend (`backend/`, FastAPI + PostgreSQL + Alembic, lives in-tree) was
   product-catalog-only - no door/measurement/room/project tables, no
   user/session identity, `phase-2-database-design.md` is an empty file.
   Kalpan confirmed building this too (not deferring it). Given there's no
   auth system at all yet (only an existing, previously-unused
   `verify_api_key`/`ADMIN_API_KEY` header gate), this uses a lightweight
   **device identity** (client-generated UUID), not real user accounts -
   building real auth is a separate, larger, security-critical project,
   explicitly out of scope here.

**Practical constraint**: Kalpan's test device is a standard iPhone 17
(not Pro) - likely no LiDAR. The depth-sensor path (Stage 4) can be built
and logic-tested but not end-to-end confirmed on this hardware - flagged
plainly when reached, not glossed over.

## Stage 1 — Backend: measurement schema + API — DONE, verified

New Alembic migration `backend/migrations/versions/004_measurement_schema.py`
(chains from `003`, mirrors `001`'s exact `op.create_table`/`sa.Column`/
`postgresql.UUID` style):
- `devices` (id, device_uuid unique, created_at, last_seen_at)
- `doors` (id, device_id FK, room_label, project_label, created_at)
- `door_measurements` (id, door_id FK, width/height/thickness_mm +
  per-dimension confidence DECIMAL(3,2), `source` with a CHECK constraint
  listing the 5 `MeasurementSource` values lowercase-snake, material/
  opening_direction/door_type, existing_handle/lock_height_mm,
  existing_bore_diameter_mm, created_at) - append-only history, multiple
  rows per door.

New `app/models/measurement.py` (SQLAlchemy, mirrors `app/models/product.py`),
registered in `app/models/__init__.py`. New `app/schemas/measurement.py`
(Pydantic `*Create`/`*Response` pairs, `MeasurementSource` as a `Literal`
matching the DB CHECK constraint). New `app/services/measurement_service.py`
(static methods: `register_device` [create-or-touch, idempotent],
`create_door`, `list_doors_for_device`, `get_door`, `save_measurement`,
`list_measurements_for_door`). New `app/api/v1/measurements.py`:
- `POST /api/v1/devices/register` - idempotent register-or-touch.
- `POST /api/v1/doors/`, `GET /api/v1/doors/` (device-scoped via a
  `device_id` query param - no session/token carries this, the client
  passes it explicitly after registering once).
- `POST /api/v1/doors/{door_id}/measurements`, `GET /api/v1/doors/{door_id}/
  measurements`.
All gated by `Depends(verify_api_key)` - this dependency existed in
`app/api/deps.py` since before Phase 5 but was never used by any route
until now.

Registered in `app/api/v1/router.py` (no extra prefix - routes already
spell out `/devices/...`/`/doors/...` under `/api/v1/`).

**Verified**: applied the migration against the already-running local
`aayam_db` (port 5434 - confirmed via `docker ps`, NOT the other `postgres`
containers on this machine from unrelated repos). `alembic current` → `004
(head)`. New tables confirmed via `psql \dt`. New `tests/test_measurements.py`
(8 tests: device register + idempotency + API-key gating, door create/
list, measurement save/history/invalid-source/missing-door) - **8/8
pass**. Full suite (`pytest`, no path filter): **29/29 pass**, zero
regressions in the 21 pre-existing tests.

## Stage 2 — Unity: device identity + measurement API client — DONE, verified

New `Assets/_Project/Scripts/Core/DeviceIdentity.cs` (namespace
`Aayam.Core.Bootstrap` - matches the rest of `Core/` despite the
subfolder, confirmed via `EventBus.cs`/`ServiceLocator.cs`/
`AppBootstrapper.cs` all using that same namespace regardless of their
own subfolder). Static helper: `GetOrCreateDeviceUuid()` generates a
`System.Guid` once, persists it as `device_identity.json` under
`Application.persistentDataPath` (same `JsonUtility`-to-file pattern as
`ModelCache`'s manifest), caches it in a static field, returns the same
value on every subsequent call/launch.

New `Assets/_Project/Scripts/Services/MeasurementApiService.cs` (namespace
`Aayam.Services`) - this project's first POST-capable service, mirroring
`ProductApiService`'s DTO-mapping convention (snake_case `[JsonProperty]`
wire DTOs, decimal fields as strings on responses per Pydantic's default
encoding, `Aayam.Models.DoorProperties`/`MeasurementSource` as the
app-internal type). Every request (GET included - all 5 measurement
routes are `Depends(verify_api_key)`-gated) sends `X-API-Key` from
`AppConfig.ApiKey`. Methods: `RegisterDeviceAsync(deviceUuid)`,
`CreateDoorAsync(deviceId, roomLabel, projectLabel)`,
`SaveMeasurementAsync(doorId, DoorProperties)`,
`FetchMeasurementHistoryAsync(doorId)`. Includes a `MeasurementSource`
<-> backend-literal mapping (`LiDAR` <-> `"lidar"`, etc. - Unity's enum is
PascalCase, the backend's CHECK constraint/Pydantic `Literal` is
lowercase-snake).

`AppConfig.cs` gained `ApiKey` (dev default `"dev-api-key-change-me"`,
matching the backend's `Settings.API_KEY` dev default) and a
`[Header("Measurement")] EnableMeasurementBackendSync` flag for Stage 3.

**Verified**: new `Assets/Tests/EditMode/DeviceIdentityTests.cs` (3 tests:
same-process idempotency, persistence across a simulated restart via
reflection-clearing the cached static field, valid-GUID parse) - **3/3
pass**. Ran the full EditMode suite headlessly
(`Unity -batchmode -nographics -runTests -testPlatform EditMode`):
**120/122 pass**. The 2 failures are both in pre-existing
`DoorRegistryTests` (`DoorCoordinateSystem_SelfRegisters_OnAwake`,
`_Unregisters_OnDestroy`) - unrelated Phase-4 code untouched this
session; not a regression from this work, not fixed here (out of Phase 5
scope). `MeasurementApiService` itself has no automated test (same as
`ProductApiService` - no precedent for testing network calls); its
correctness will be exercised for real in Stage 3's on-device flow
against the Stage 1 backend.

## Stage 3 — Unity: wire calibration into real measurement + history + visuals — DONE, verified on-device

New `Assets/_Project/Scripts/Models/MeasurementHistoryEntry.cs` (namespace
`Aayam.Models`): `DoorLocalId` (string - a `Guid.ToString()`, since
`JsonUtility` can't serialize `System.Guid` directly), `TimestampUtc`
(ISO-8601 string, matching `ModelCacheEntry`'s convention), width/height/
thickness + per-dimension confidence floats, `MeasurementSource` -
append-only, unlike the transient `DoorProperties`.

New `Assets/_Project/Scripts/Services/MeasurementHistoryStore.cs`
(namespace `Aayam.Services`) - **static**, not a MonoBehaviour (unlike
`ModelCache` - this class has no Inspector-configurable state and the
plan's own call site, `MeasurementHistoryStore.SaveMeasurement(entry)`,
assumes no instance). Mirrors `ModelCache`'s exact JsonUtility-manifest-
under-`persistentDataPath` pattern (`measurement_history.json`).
`SaveMeasurement(entry)` writes to disk **synchronously** first - a
measurement is never lost to a flaky network - then fires a best-effort
backend sync (`async void`, matching `ProductSelector`'s established
fire-and-forget convention for UI call sites that can't `await`):
registers the device via `DeviceIdentity.GetOrCreateDeviceUuid()`,
lazily creates one backend door (this pass calibrates one physical door
per session - multi-door management is a later-phase concern), then
calls `MeasurementApiService.SaveMeasurementAsync`. Any failure at any
step logs a warning and gives up silently - the local save already
happened. `ListMeasurements(doorLocalId)` reads back history, optionally
scoped to one door. Also exposes `DisableBackendSyncForTests` (public
bool) and `ResetInMemoryStateForTests()` - needed because `internal`
wouldn't be visible to `Aayam.Tests.EditMode` (no
`InternalsVisibleTo` anywhere in this project) and because an
unguarded real network attempt during EditMode tests would be
non-deterministic and could write real rows into whatever backend
happens to be reachable.

Extended `Assets/_Project/Scripts/UI/DoorEdgeCalibrationUI.cs`:
- `HandleConfirmClicked`, after the existing
  `ComputeScaleCorrection`/`ApplyScaleCorrection` call, now also builds a
  `MeasurementHistoryEntry` (source `UserAssisted`, confidence `0.90` -
  the point estimate inside the roadmap's own 85-95% band) from the
  now-corrected `_doorGeometry.DoorWidthMeters`/`DoorHeightMeters` and
  calls `MeasurementHistoryStore.SaveMeasurement`. A `_doorLocalId`
  (lazily generated `Guid`) is reused across repeated confirms in the
  same run so their history chains together.
- Added the previously-missing visual feedback (roadmap Step 4): a
  `LineRenderer` between `_leftMarker`/`_rightMarker`, created once both
  edges are tapped, and a world-space `TextMesh` (no Canvas needed, same
  lightweight-marker-sphere style as the existing markers) at the
  midpoint showing `{span:F2}m`, billboarded toward `_arCamera` every
  frame in a new `LateUpdate`.

**Verified so far**: new `Assets/Tests/EditMode/MeasurementHistoryStoreTests.cs`
(5 tests: immediate disk write, save-then-list, append-only across two
saves, per-door scoping, persistence across a simulated restart) -
**5/5 pass**, all with `DisableBackendSyncForTests = true` so no real
network call happens during the test run. Full EditMode suite re-run
after this stage: **125/127 pass** (same 2 pre-existing
`DoorRegistryTests` failures as Stage 2, still unrelated and untouched -
not a regression from this stage either). `DoorEdgeCalibrationUI.cs`
compiles cleanly with the new wiring.

**On-device verification (2026-10-29)**: built via `CIBuildScript.PerformIOSBuild`
→ `xcodebuild` → `devicectl install`/`launch --console`, backend run
locally via the venv's `uvicorn` (the `docker-compose` `api` image build
hit a Docker Desktop crash mid-build - documented under "Incidents"
below) bound to `0.0.0.0:8010`, reachable from the phone over LAN.
Temporarily pointed `AppConfig.ApiBaseUrl`/`CdnBaseUrl` at the Mac's LAN
IP and set `ProjectSettings.insecureHttpOption` to `2` (AlwaysAllowed) so
plain HTTP to that IP wouldn't be blocked by iOS App Transport Security
(there was no existing ATS exception anywhere in this project - this
network call path had never been exercised on-device before) - **both
reverted back to their committed dev defaults** (`localhost:8010`,
`insecureHttpOption: 0`) immediately after the test.

Walked through the real flow on Kalpan's phone: selected a product →
`Screen_ARScan` → door PnP solve locked on and tracked steadily
(`assumedSize=0.7493x2.159m`) → tapped "Calibrate Door" → tapped left
edge → tapped right edge (console: `AR thinks this span is 0.569m`) →
**confirmed the cyan measurement line and the distance label rendered
between the two tap markers** → entered `0.75` as the real measurement →
Confirm. Console showed the full chain firing in order: scale
correction applied (`newWidth=0.7500 newHeight=2.1610`) → `[DoorEdgeCalibrationUI]
Saved measurement history: widthMm=750.0 heightMm=2161.0` →
`[MeasurementHistoryStore] Synced measurement to backend (door
c636c2ea-d5a3-49b0-9fd0-37d0a051611b)`. Confirmed server-side via
`GET /api/v1/doors/{id}/measurements`: the row exists with
`width_mm=750.00`, `height_mm=2161.02`, `width_confidence=height_confidence=0.90`,
`source=user_assisted`. All three previously-open pieces (history save,
visual feedback, backend sync) are now confirmed working end-to-end, not
just unit-tested.

**Incident during this pass**: Docker Desktop crashed mid-build
(`rpc error: code = Unavailable ... EOF`) while building the `api`
service's image for the first time this session - relaunched it
(`open -a Docker`), confirmed the daemon came back, then ran the backend
via the venv's `uvicorn` directly against the already-running `aayam_db`
container instead of rebuilding the `api` image under time pressure.
Not a code issue; noted here so a stalled `docker compose up api` isn't
mysterious later.

## Stage 4 — Unity: depth-sensor measurement path — code DONE, NOT device-verifiable on this hardware

New `Assets/_Project/Scripts/AR/Measurement/DepthMeasurementSampler.cs`
(namespace `Aayam.AR.Measurement`, same `Aayam.AR` assembly as every
other `AR/*` subfolder). A discovery during this stage: `DepthTextureHandler`
already exposes `GetSmoothedDepthAtScreenPoint` (built for occlusion/
placement, never used for measurement) - so this class is a thin
Try-pattern wrapper, not a new acquisition path. It adds what the raw API
doesn't give: a `bool TrySampleDepthMeters(screenPoint, out depthMeters)`
that distinguishes "no data yet" from "a real zero," plus a 10m
plausibility ceiling so a corrupt sample can't silently become a
measurement.

New `Assets/_Project/Scripts/AR/Measurement/DoorMeasurementCoordinator.cs`:
pure static branching logic (`ShouldUseDepthSensor(bool)`,
`SourceForTier(DeviceQualityTier)` - `Ultra` to `MeasurementSource.LiDAR`,
anything else depth-capable to `.ARDepth`, `ConfidenceForSource(...)` -
the roadmap's own 90-95%/75-85% bands as point estimates 0.925/0.80) plus
an instance method `TryMeasure()`. The PnP-solved door corners
(`DoorCoordinateSystem.DoorToWorldSpace` with `DoorGeometryState`'s
width/height, per `DoorCoordinateSystem`'s own documented convention -
origin at bottom-left, X = width axis, Y = height axis) are used **only**
to aim each depth sample at the right screen point via
`Camera.WorldToScreenPoint`; the actual corner position used for the
width/height calculation comes back out via
`Camera.ScreenToWorldPoint(screenPoint, depthMeters)`, independent of the
PnP solve - so this is a genuinely independent sensor-based
cross-check, not a restatement of the PnP geometry under a different
label. Falls back to nothing itself - `TryMeasure()` returns `null`
whenever depth isn't available, the door hasn't been solved yet, or any
corner's depth sample fails; callers use Stage 3's
`DoorEdgeCalibrationUI` flow in that case.

**Verified**: new `Assets/Tests/EditMode/DoorMeasurementCoordinatorTests.cs`
(7 tests: depth-available branching, tier-to-source mapping for all 4
`DeviceQualityTier` values, both confidence values fall inside the
roadmap's own bands) - **7/7 pass**, pure logic only, no scene/
MonoBehaviour dependency. Full EditMode suite: **132/134 pass** (same 2
pre-existing, unrelated `DoorRegistryTests` failures as Stages 2-3).

**Explicitly NOT verified, and can't be from this machine**: the actual
depth-sampling math (`TryReconstructFromDepth`, the full `TryMeasure()`
path) has no LiDAR/ToF hardware to run against - Kalpan's iPhone 17 is
not a Pro model. This is flagged exactly as the plan anticipated, not
glossed over: if a LiDAR-equipped device becomes available, that's the
point to actually confirm this stage end-to-end.

## Overall status

All 4 stages are done. Stages 1-3 are fully verified, including Stage
3's on-device pass. Stage 4's branching logic is unit-verified; its
actual depth-sampling math is structurally impossible to verify on
Kalpan's current hardware (no LiDAR/ToF). Full EditMode suite:
**132/134 pass**, zero regressions attributable to any of this work (the
2 failures are pre-existing `DoorRegistryTests` issues in Phase 4 code
untouched this session). Backend: **29/29 pytest pass**, plus one
real measurement row confirmed end-to-end from an on-device tap-to-
calibrate session. Phase 5 is complete for this pass; Stage 4's hardware
gap is the one open item, to be revisited if a LiDAR-equipped device
becomes available.
