# Graph Report - Aayam  (2026-10-10)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 1208 nodes · 2131 edges · 56 communities (47 shown, 9 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 60 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `8f2a68dc`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Community 0
- Community 1
- Community 2
- Community 3
- Community 4
- Community 5
- Community 6
- Community 7
- Community 8
- Community 9
- Community 10
- Community 11
- Community 12
- Community 13
- Community 14
- Community 15
- Community 16
- Community 17
- Community 18
- Community 19
- Community 20
- Community 21
- Community 22
- Community 23
- Community 24
- Community 25
- Community 26
- Community 27
- Community 28
- Community 29
- Community 30
- Community 31
- Community 32
- Community 33
- Community 34
- Community 35
- Community 36
- Community 37
- Community 38
- Community 39
- Community 40
- Community 41
- Community 42
- Community 43
- Community 44
- Community 45
- Community 46
- Community 47
- Community 48
- Community 49
- Community 50
- Community 51
- Community 52
- Community 53
- Community 55

## God Nodes (most connected - your core abstractions)
1. `ARHUDController` - 29 edges
2. `PlacementReticle` - 24 edges
3. `ObjectSelector` - 22 edges
4. `ObjectPlacementController` - 22 edges
5. `ARSessionController` - 21 edges
6. `ModelCache` - 20 edges
7. `DragHandler` - 18 edges
8. `PlaneVisualizationController` - 18 edges
9. `ModelPreview` - 18 edges
10. `OcclusionController` - 18 edges

## Surprising Connections (you probably didn't know these)
- `InteractionStateMachine` --references--> `DragHandler`  [EXTRACTED]
  Assets/_Project/Scripts/AR/Interaction/InteractionSystem.cs → Assets/_Project/Scripts/AR/Interaction/DragHandler.cs
- `InteractionStateMachine` --references--> `RotationHandler`  [EXTRACTED]
  Assets/_Project/Scripts/AR/Interaction/InteractionSystem.cs → Assets/_Project/Scripts/AR/Interaction/RotationHandler.cs
- `InteractionStateMachine` --references--> `ScaleHandler`  [EXTRACTED]
  Assets/_Project/Scripts/AR/Interaction/InteractionSystem.cs → Assets/_Project/Scripts/AR/Interaction/ScaleHandler.cs
- `ObjectPlacementController` --references--> `PlacementReticle`  [EXTRACTED]
  Assets/_Project/Scripts/AR/Placement/ObjectPlacementController.cs → Assets/_Project/Scripts/AR/Placement/PlacementReticle.cs
- `ModelLoader` --references--> `ModelCache`  [EXTRACTED]
  Assets/_Project/Scripts/Services/ModelLoader.cs → Assets/_Project/Scripts/Services/ModelCache.cs

## Import Cycles
- None detected.

## Communities (56 total, 9 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.06
Nodes (10): Aayam.Utilities, Aayam.UI, Aayam.AR.Placement, Aayam.Services, Aayam.AR.Interaction, Aayam.AR.Occlusion, Aayam.Tests.EditMode, Aayam.Core.Bootstrap (+2 more)

### Community 1 - "Community 1"
Cohesion: 0.05
Nodes (12): DragHandler, GestureDetector, ObjectSelector, CurrentSelection, HasSelection, SelectionOutlineMarker, RotationHandler, ScaleHandler (+4 more)

### Community 2 - "Community 2"
Cohesion: 0.07
Nodes (9): DoorDetectionTests, MathUtilsTests, ModelCacheTests, TestCache, Count, TotalSize, PlacementTests, PlaceRecord (+1 more)

### Community 3 - "Community 3"
Cohesion: 0.05
Nodes (26): AnchorController, AnchorCount, DoorSurfacePlacer, ProductHeightType, Custom, Handle, Lock, Peephole (+18 more)

### Community 4 - "Community 4"
Cohesion: 0.06
Nodes (12): HapticType, Heavy, Light, Medium, PlacementAnimationRunner, PlacementFeedback, CoachingOverlay, ScreenshotCapture (+4 more)

### Community 5 - "Community 5"
Cohesion: 0.10
Nodes (4): LODSetup, ModelInstanceManager, ModelLoader, ModelPreview

### Community 6 - "Community 6"
Cohesion: 0.05
Nodes (43): dependencies, com.unity.ide.rider, com.unity.ide.visualstudio, com.unity.modules.accessibility, com.unity.modules.adaptiveperformance, com.unity.modules.ai, com.unity.modules.androidjni, com.unity.modules.animation (+35 more)

### Community 7 - "Community 7"
Cohesion: 0.10
Nodes (11): ARHUDController, AppScreen, ARScan, Debug, Loading, ProductBrowser, ProductDetail, Settings (+3 more)

### Community 8 - "Community 8"
Cohesion: 0.09
Nodes (14): HapticFeedback, InteractionState, Idle, Locked, Moving, Rotating, Scaling, Selected (+6 more)

### Community 9 - "Community 9"
Cohesion: 0.10
Nodes (13): MeshingController, OcclusionController, HasEnvironmentDepth, HasHumanOcclusion, IsOcclusionEnabled, OcclusionQualityManager, CurrentTier, DetectedTier (+5 more)

### Community 10 - "Community 10"
Cohesion: 0.14
Nodes (8): ModelCache, CurrentSizeBytes, EntryCount, ModelCacheEntry, ModelCacheManifest, ProductCard, ProductSelector, ProductUIItem

### Community 11 - "Community 11"
Cohesion: 0.08
Nodes (30): dependencies, depth, source, version, dependencies, depth, source, version (+22 more)

### Community 12 - "Community 12"
Cohesion: 0.11
Nodes (5): DepthPlacement, DepthTextureHandler, CurrentDepthTexture, HasDepthData, SafeAreaAdapter

### Community 13 - "Community 13"
Cohesion: 0.07
Nodes (27): com.unity.modules.assetbundle, com.unity.modules.imageconversion, com.unity.modules.jsonserialize, com.unity.modules.unityanalytics, com.unity.modules.unitywebrequestaudio, com.unity.xr.arfoundation, com.unity.xr.management, com.unity.ide.rider (+19 more)

### Community 14 - "Community 14"
Cohesion: 0.07
Nodes (26): com.unity.modules.androidjni, com.unity.modules.animation, com.unity.modules.audio, com.unity.modules.imgui, com.unity.modules.particlesystem, com.unity.modules.physics, com.unity.modules.physics2d, com.unity.modules.physicscore2d (+18 more)

### Community 15 - "Community 15"
Cohesion: 0.08
Nodes (27): dependencies, depth, source, version, dependencies, depth, source, version (+19 more)

### Community 16 - "Community 16"
Cohesion: 0.10
Nodes (12): ARSessionStateChangedEvent, DoorCandidateDetectedEvent, PlaneDetectedEvent, ProductMovedEvent, ProductPlacedEvent, ProductRemovedEvent, ProductSelectedEvent, ScreenChangedEvent (+4 more)

### Community 17 - "Community 17"
Cohesion: 0.10
Nodes (24): CheckStatus, Fail, Pass, Unknown, Warning, CompatibilityCheck, CompatibilityResult, DoorProperties (+16 more)

### Community 18 - "Community 18"
Cohesion: 0.08
Nodes (26): dependencies, depth, source, version, dependencies, depth, source, version (+18 more)

### Community 19 - "Community 19"
Cohesion: 0.08
Nodes (25): dependencies, depth, source, version, dependencies, depth, source, version (+17 more)

### Community 20 - "Community 20"
Cohesion: 0.12
Nodes (3): AppBootstrapper, ServiceLocator, EventBus

### Community 21 - "Community 21"
Cohesion: 0.09
Nodes (23): dependencies, depth, source, version, dependencies, depth, source, url (+15 more)

### Community 22 - "Community 22"
Cohesion: 0.09
Nodes (22): dependencies, depth, source, version, dependencies, depth, source, url (+14 more)

### Community 23 - "Community 23"
Cohesion: 0.13
Nodes (17): depth, source, version, dependencies, depth, source, url, version (+9 more)

### Community 24 - "Community 24"
Cohesion: 0.17
Nodes (3): ARSessionController, CurrentState, IsTracking

### Community 25 - "Community 25"
Cohesion: 0.12
Nodes (4): DepthVisualizer, PlaneEventHandler, TotalPlaneCount, VerticalPlaneCount

### Community 26 - "Community 26"
Cohesion: 0.13
Nodes (15): dependencies, depth, source, url, version, depth, source, version (+7 more)

### Community 27 - "Community 27"
Cohesion: 0.13
Nodes (15): dependencies, depth, source, version, depth, source, version, dependencies (+7 more)

### Community 28 - "Community 28"
Cohesion: 0.13
Nodes (15): dependencies, depth, source, version, dependencies, depth, source, version (+7 more)

### Community 29 - "Community 29"
Cohesion: 0.13
Nodes (15): dependencies, depth, source, version, dependencies, depth, source, version (+7 more)

### Community 31 - "Community 31"
Cohesion: 0.19
Nodes (9): DeviceCapabilities, DeviceCapabilityChecker, Capabilities, QualityTier, DeviceQualityTier, High, Low, Medium (+1 more)

### Community 32 - "Community 32"
Cohesion: 0.20
Nodes (9): AayamPlaneType, Ceiling, Door, DoorFrame, Floor, Table, Unknown, Wall (+1 more)

### Community 34 - "Community 34"
Cohesion: 0.27
Nodes (3): CameraPermissionHandler, HasPermission, IsRequestInProgress

### Community 36 - "Community 36"
Cohesion: 0.24
Nodes (3): ProjectStructureCreator, URPConfigValidator, Aayam.Editor

### Community 37 - "Community 37"
Cohesion: 0.17
Nodes (12): dependencies, depth, source, version, dependencies, dependencies, depth, source (+4 more)

### Community 38 - "Community 38"
Cohesion: 0.18
Nodes (11): SessionState, CheckingAvailability, Error, Installing, NeedsInstall, None, NotSupported, Paused (+3 more)

### Community 39 - "Community 39"
Cohesion: 0.20
Nodes (9): Aayam.AR, Aayam.Core, Aayam.Editor, Aayam.Models, Aayam.Services, Aayam.Tests.EditMode, Aayam.Tests.PlayMode, Aayam.UI (+1 more)

### Community 43 - "Community 43"
Cohesion: 0.27
Nodes (4): PerformanceProfiler, CurrentFps, MaxFps, MinFps

### Community 44 - "Community 44"
Cohesion: 0.20
Nodes (10): dependencies, depth, source, version, dependencies, depth, source, version (+2 more)

### Community 45 - "Community 45"
Cohesion: 0.20
Nodes (10): dependencies, depth, source, version, dependencies, depth, source, version (+2 more)

### Community 47 - "Community 47"
Cohesion: 0.22
Nodes (9): GestureType, DoubleTap, Drag, LongPress, None, Pinch, Tap, Twist (+1 more)

### Community 48 - "Community 48"
Cohesion: 0.33
Nodes (4): DoorCandidateDetector, BestCandidate, BestCandidateScore, HasConfidentCandidate

### Community 49 - "Community 49"
Cohesion: 0.29
Nodes (5): AppConfig, AppName, AppVersion, BundleId, ModelCachePath

### Community 50 - "Community 50"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.ai

### Community 51 - "Community 51"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.particlesystem

### Community 52 - "Community 52"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.tetgen

### Community 53 - "Community 53"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.umbra

## Knowledge Gaps
- **147 isolated node(s):** `CurrentSelection`, `HasSelection`, `CurrentPose`, `IsValidSurface`, `IsVisible` (+142 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 519 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **9 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `dependencies` connect `Community 27` to `Community 37`, `Community 11`, `Community 44`, `Community 45`, `Community 14`, `Community 15`, `Community 18`, `Community 50`, `Community 19`, `Community 21`, `Community 22`, `Community 51`, `Community 52`, `Community 53`, `Community 26`, `Community 23`, `Community 28`, `Community 29`?**
  _High betweenness centrality (0.074) - this node is a cross-community bridge._
- **What connects `CurrentSelection`, `HasSelection`, `CurrentPose` to the rest of the system?**
  _147 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.05630252100840336 - nodes in this community are weakly interconnected._
- **Why does `PlaneEventHandler` connect `Community 25` to `Community 0`, `Community 33`, `Community 35`, `Community 46`, `Community 30`?**
  _High betweenness centrality (0.037) - this node is a cross-community bridge._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.054098360655737705 - nodes in this community are weakly interconnected._
- **Why does `ObjectPlacementController` connect `Community 3` to `Community 0`, `Community 1`, `Community 5`, `Community 7`, `Community 10`, `Community 25`?**
  _High betweenness centrality (0.036) - this node is a cross-community bridge._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.07486338797814207 - nodes in this community are weakly interconnected._