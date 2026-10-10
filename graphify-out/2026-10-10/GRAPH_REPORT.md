# Graph Report - Aayam  (2026-10-10)

## Corpus Check
- 134 files · ~72,412 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 208 file(s) not represented in the graph (top: .meta 144, .asset 32, (none) 13)

## Summary
- 2070 nodes · 3483 edges · 135 communities (94 shown, 41 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 118 edges (avg confidence: 0.89)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `dc533af7`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- unityengine
- PlacementReticle
- Test
- PlacementHistory
- PlacementFeedback
- GameObject
- dependencies
- ARHUDController
- InteractionStateMachine
- OcclusionController
- ModelCache
- com.unity.modules.unitywebrequest
- Vector2
- manifest.json
- packages-lock.json
- dependencies
- Vector3
- CompatibilityResult.cs
- com.unity.modules.jsonserialize
- dependencies
- ServiceLocator
- com.unity.modules.androidjni
- com.unity.test-framework
- com.unity.xr.core-utils
- ARSessionController
- DepthVisualizer
- com.unity.modules.imgui
- com.unity.modules.accessibility
- com.unity.modules.imageconversion
- com.unity.modules.physics2d
- PlaneOcclusionController
- DeviceCapabilityChecker
- AayamPlaneType
- DebugPanel
- test_products.py
- PlaneVisualizationController
- com.unity.burst
- Phase 3: Door Detection (Computer Vision)
- SessionState
- Aayam.slnx
- MonoBehaviour
- ARPlane
- VerticalPlaneFilter
- PerformanceProfiler
- Step 9: Testing & Quality Assurance — Detailed Implementation Guide
- com.unity.modules.audio
- PlaneEventHandler
- GestureType
- get_storage_dir
- AppConfig
- .MapToProductData
- Step 3: Plane Detection — Detailed Implementation Guide
- com.unity.modules.tetgen
- ProductService
- AREventBus
- Phase 5: Automatic Measurement
- Step 7: Basic UI — Detailed Implementation Guide
- Phase 8: Retailer Dashboard & Pilot
- models/product.py
- products.py
- Aayam — Claude Code Instructions
- sqlalchemy
- main.py
- Phase 2: Create the Product Pipeline
- Aayam - Architecture & Technology Decisions
- Phase 6: Realistic Visualization
- .BuildUI
- Phase 7: Compatibility Engine
- CameraPermissionHandler
- Aayam Backend API
- Step 1: Unity Project Setup — Detailed Implementation Guide
- Phase 4: Door Coordinate System
- Step 2: AR Session Configuration — Detailed Implementation Guide
- Phase 1, Step 4: Object Placement — Detailed Implementation Guide
- Step 6: 3D Model Loading — Detailed Implementation Guide
- Aayam: Project Context
- assets.py
- ProductSelector
- Phase 1, Step 8: Depth & Occlusion — Detailed Implementation Guide
- AnchorController
- ObjectSelector
- Phase 0: Validate Before Coding
- Aayam 🚪✨
- system
- ObjectPlacementController
- GestureDetector
- PlaceCommand
- generate_placeholder_models.py
- ModelInstanceManager
- brands.py
- categories.py
- com.unity.ext.nunit
- ModelLoader
- Phase 1: Build the AR Foundation
- bug_report.md
- ProductApiService.cs
- validators.py
- .FindBaseRig
- IEnumerator
- com.unity.modules.umbra
- QRScanner.cs
- DoorSurfacePlacer
- PlacementState
- Current graph (built 2026-10-10)
- 6.4 Model Optimization for Mobile
- GestureDetector.cs
- com.unity.modules.wind
- DepthTextureHandler
- EventBus.cs
- Aayam.Models
- graphify
- DoorCandidateDetector
- AayamSceneBootstrapper.cs
- SafeAreaAdapter
- MenuItem
- FloorPlaneDetector
- RotationHandler
- ScaleHandler
- ScreenNavigationTests
- PlaneDebugVisualizer
- PerformanceProfiler.cs
- 9.10 QA Checklist Template
- 9.6 Tracking Stability Tests
- Step 1: Database Schema Design (Week 1)

## God Nodes (most connected - your core abstractions)
1. `ARHUDController` - 31 edges
2. `ObjectPlacementController` - 26 edges
3. `PlacementReticle` - 26 edges
4. `ARSessionController` - 25 edges
5. `ProductSelector` - 25 edges
6. `ObjectSelector` - 23 edges
7. `ModelCache` - 21 edges
8. `AnchorController` - 19 edges
9. `PlaneVisualizationController` - 19 edges
10. `AayamSceneBootstrapper` - 18 edges

## Surprising Connections (you probably didn't know these)
- `5.2 Gesture Detection System` --references--> `GestureDetector`  [INFERRED]
  docs/phases/phase-1-steps/step-5-object-interaction.md → Assets/_Project/Scripts/AR/Interaction/GestureDetector.cs
- `Debug UI Setup` --references--> `PlaneDebugVisualizer`  [INFERRED]
  docs/phases/phase-1-steps/step-3-plane-detection.md → Assets/_Project/Scripts/AR/Planes/PlaneDebugVisualizer.cs
- `3.10 Performance Optimization` --references--> `PlaneEventHandler`  [INFERRED]
  docs/phases/phase-1-steps/step-3-plane-detection.md → Assets/_Project/Scripts/AR/Planes/PlaneEventHandler.cs
- `Plane Visualization Prefab Setup` --references--> `PlaneVisualizationController`  [INFERRED]
  docs/phases/phase-1-steps/step-3-plane-detection.md → Assets/_Project/Scripts/AR/Planes/PlaneVisualizationController.cs
- `Setup Instructions` --references--> `SafeAreaAdapter`  [INFERRED]
  docs/phases/phase-1-steps/step-7-basic-ui.md → Assets/_Project/Scripts/UI/SafeAreaAdapter.cs

## Import Cycles
- None detected.

## Communities (135 total, 41 thin omitted)

### Community 0 - "unityengine"
Cohesion: 0.22
Nodes (3): DeviceCapabilities, Aayam.AR.Occlusion, Aayam.AR.Planes

### Community 1 - "PlacementReticle"
Cohesion: 0.18
Nodes (5): SelectionOutlineMarker, PlacementReticle, CurrentPose, IsValidSurface, IsVisible

### Community 2 - "Test"
Cohesion: 0.06
Nodes (10): DeepLinkParserTests, DoorDetectionTests, MathUtilsTests, ModelCacheTests, TestCache, Count, TotalSize, PlacementTests (+2 more)

### Community 3 - "PlacementHistory"
Cohesion: 0.17
Nodes (7): IPlacementCommand, Description, PlacementHistory, CanRedo, CanUndo, RemoveCommand, Description

### Community 4 - "PlacementFeedback"
Cohesion: 0.16
Nodes (6): HapticType, Heavy, Light, Medium, PlacementAnimationRunner, PlacementFeedback

### Community 6 - "dependencies"
Cohesion: 0.04
Nodes (45): dependencies, com.unity.cloud.gltfast, com.unity.ide.rider, com.unity.ide.visualstudio, com.unity.modules.accessibility, com.unity.modules.adaptiveperformance, com.unity.modules.ai, com.unity.modules.androidjni (+37 more)

### Community 7 - "ARHUDController"
Cohesion: 0.07
Nodes (14): ARHUDController, CoachingOverlay, AppScreen, ARScan, Debug, Loading, ProductBrowser, ProductDetail (+6 more)

### Community 8 - "InteractionStateMachine"
Cohesion: 0.05
Nodes (31): HapticFeedback, InteractionState, Idle, Locked, Moving, Rotating, Scaling, Selected (+23 more)

### Community 9 - "OcclusionController"
Cohesion: 0.09
Nodes (13): MeshingController, OcclusionController, HasEnvironmentDepth, HasHumanOcclusion, IsOcclusionEnabled, OcclusionQualityManager, CurrentTier, DetectedTier (+5 more)

### Community 10 - "ModelCache"
Cohesion: 0.22
Nodes (5): ModelCache, CurrentSizeBytes, EntryCount, ModelCacheEntry, ModelCacheManifest

### Community 11 - "com.unity.modules.unitywebrequest"
Cohesion: 0.09
Nodes (25): dependencies, depth, source, version, dependencies, depth, source, version (+17 more)

### Community 13 - "manifest.json"
Cohesion: 0.04
Nodes (44): com.unity.modules.androidjni, com.unity.modules.animation, com.unity.modules.assetbundle, com.unity.modules.audio, com.unity.modules.imageconversion, com.unity.modules.imgui, com.unity.modules.jsonserialize, com.unity.modules.particlesystem (+36 more)

### Community 14 - "packages-lock.json"
Cohesion: 0.05
Nodes (37): com.unity.modules.androidjni, com.unity.modules.animation, com.unity.modules.assetbundle, com.unity.modules.audio, com.unity.modules.imageconversion, com.unity.modules.imgui, com.unity.modules.jsonserialize, com.unity.modules.particlesystem (+29 more)

### Community 15 - "dependencies"
Cohesion: 0.08
Nodes (27): dependencies, depth, source, version, dependencies, depth, source, version (+19 more)

### Community 17 - "CompatibilityResult.cs"
Cohesion: 0.13
Nodes (18): CheckStatus, Fail, Pass, Unknown, Warning, CompatibilityCheck, CompatibilityResult, DoorProperties (+10 more)

### Community 18 - "com.unity.modules.jsonserialize"
Cohesion: 0.08
Nodes (26): dependencies, depth, source, version, dependencies, depth, source, version (+18 more)

### Community 19 - "dependencies"
Cohesion: 0.07
Nodes (31): dependencies, depth, source, version, dependencies, depth, source, version (+23 more)

### Community 20 - "ServiceLocator"
Cohesion: 0.12
Nodes (3): AppBootstrapper, ServiceLocator, EventBus

### Community 21 - "com.unity.modules.androidjni"
Cohesion: 0.09
Nodes (23): dependencies, depth, source, version, dependencies, depth, source, url (+15 more)

### Community 22 - "com.unity.test-framework"
Cohesion: 0.12
Nodes (16): dependencies, depth, source, url, version, dependencies, depth, dependencies (+8 more)

### Community 23 - "com.unity.xr.core-utils"
Cohesion: 0.13
Nodes (17): depth, source, version, dependencies, depth, source, url, version (+9 more)

### Community 24 - "ARSessionController"
Cohesion: 0.20
Nodes (3): ARSessionController, CurrentState, IsTracking

### Community 26 - "com.unity.modules.imgui"
Cohesion: 0.07
Nodes (31): dependencies, depth, source, url, version, dependencies, depth, source (+23 more)

### Community 27 - "com.unity.modules.accessibility"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.accessibility

### Community 28 - "com.unity.modules.imageconversion"
Cohesion: 0.13
Nodes (15): dependencies, depth, source, version, dependencies, depth, source, version (+7 more)

### Community 29 - "com.unity.modules.physics2d"
Cohesion: 0.10
Nodes (20): dependencies, depth, source, version, dependencies, depth, source, version (+12 more)

### Community 31 - "DeviceCapabilityChecker"
Cohesion: 0.21
Nodes (8): DeviceCapabilityChecker, Capabilities, QualityTier, DeviceQualityTier, High, Low, Medium, Ultra

### Community 32 - "AayamPlaneType"
Cohesion: 0.20
Nodes (9): AayamPlaneType, Ceiling, Door, DoorFrame, Floor, Table, Unknown, Wall (+1 more)

### Community 34 - "test_products.py"
Cohesion: 0.08
Nodes (20): get_url(), run_migrations_offline(), run_migrations_online(), app(), client(), settings(), test_health_contains_environment(), test_health_contains_version() (+12 more)

### Community 36 - "com.unity.burst"
Cohesion: 0.08
Nodes (27): dependencies, depth, source, version, dependencies, depth, source, url (+19 more)

### Community 37 - "Phase 3: Door Detection (Computer Vision)"
Cohesion: 0.06
Nodes (31): Algorithm Steps, Data Augmentation Strategies, Dataset Parameters, Deliverables, Detection Pipeline Architecture, Evaluation Metrics, Export Pipeline, Export Script (+23 more)

### Community 38 - "SessionState"
Cohesion: 0.18
Nodes (11): SessionState, CheckingAvailability, Error, Installing, NeedsInstall, None, NotSupported, Paused (+3 more)

### Community 39 - "Aayam.slnx"
Cohesion: 0.20
Nodes (9): Aayam.AR, Aayam.Core, Aayam.Editor, Aayam.Models, Aayam.Services, Aayam.Tests.EditMode, Aayam.Tests.PlayMode, Aayam.UI (+1 more)

### Community 43 - "PerformanceProfiler"
Cohesion: 0.27
Nodes (4): PerformanceProfiler, CurrentFps, MaxFps, MinFps

### Community 44 - "Step 9: Testing & Quality Assurance — Detailed Implementation Guide"
Cohesion: 0.09
Nodes (22): 9.11 Demo Video Script, 9.1 Testing Strategy Overview, 9.2 Unit Testing Setup, 9.3 Integration Testing, 9.4 Device Testing Matrix, 9.5 Performance Benchmarks, 9.7 Placement Accuracy Tests, 9.8 Automated Testing Pipeline (+14 more)

### Community 45 - "com.unity.modules.audio"
Cohesion: 0.10
Nodes (20): dependencies, depth, source, version, dependencies, depth, source, version (+12 more)

### Community 46 - "PlaneEventHandler"
Cohesion: 0.18
Nodes (3): PlaneEventHandler, TotalPlaneCount, VerticalPlaneCount

### Community 47 - "GestureType"
Cohesion: 0.22
Nodes (9): GestureType, DoubleTap, Drag, LongPress, None, Pinch, Tap, Twist (+1 more)

### Community 48 - "get_storage_dir"
Cohesion: 0.39
Nodes (8): get_storage_dir(), test_generate_qr_creates_working_png(), test_generate_qr_rejects_unsafe_product_id(), test_upload_model_requires_admin_key(), test_upload_model_writes_file_and_returns_working_url(), test_upload_rejects_path_traversal_filename(), test_upload_rejects_unsafe_category(), test_upload_rejects_wrong_extension_for_asset_type()

### Community 49 - "AppConfig"
Cohesion: 0.25
Nodes (5): AppConfig, AppName, AppVersion, BundleId, ModelCachePath

### Community 50 - ".MapToProductData"
Cohesion: 0.36
Nodes (6): AnchorPoint, DoorCompatibility, ProductAsset, ProductData, ProductDimensions, ProductApiService

### Community 51 - "Step 3: Plane Detection — Detailed Implementation Guide"
Cohesion: 0.07
Nodes (28): 3.10 Performance Optimization, 3.1 Understanding Plane Detection, 3.2 AR Plane Manager Setup, 3.3 Plane Visualization System, 3.4 Vertical Plane Detection (Critical Path), 3.5 Plane Classification, 3.6 Plane Boundary Detection, 3.7 Plane Events and Callbacks (+20 more)

### Community 52 - "com.unity.modules.tetgen"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.tetgen

### Community 55 - "Phase 5: Automatic Measurement"
Cohesion: 0.08
Nodes (24): Accuracy Targets, AR-Based Measurement, Code Snippet: Basic Depth Extraction (Swift/ARKit), Code Snippet: Point-to-Point Measurement (C#/Unity AR Foundation), Code Snippet: Reference Object Method (Python/OpenCV), Confidence Scoring (0-100%), Deliverables, Display to User (+16 more)

### Community 56 - "Step 7: Basic UI — Detailed Implementation Guide"
Cohesion: 0.12
Nodes (16): 7.10 Loading States, 7.1 UI Framework Choice, 7.2 UI Architecture, 7.3 AR Overlay HUD, 7.4 Coaching Overlay System, 7.5 Product Selector UI, 7.6 Screenshot Capture, 7.7 Debug Info Panel (+8 more)

### Community 57 - "Phase 8: Retailer Dashboard & Pilot"
Cohesion: 0.09
Nodes (23): Auth Flow Diagram, Dashboard Architecture, Dashboard Pages Structure, Deliverables, Deployment Plan, Next.js Middleware Example, Overview, Phase 8: Retailer Dashboard & Pilot (+15 more)

### Community 58 - "models/product.py"
Cohesion: 0.24
Nodes (13): Brand, Category, Product, Product3DAsset, ProductAttribute, ProductCompatibility, ProductDimension, ProductImage (+5 more)

### Community 59 - "products.py"
Cohesion: 0.14
Nodes (20): create_product(), delete_product(), get_product(), get_product_by_sku(), list_products(), update_product(), ErrorDetail, ErrorResponse (+12 more)

### Community 60 - "Aayam — Claude Code Instructions"
Cohesion: 0.09
Nodes (21): 10. CI/CD, 11. Dependency Management, 12. Communication, 13. Security Boundary, 1. Project Identity, 2. Identity Isolation — CRITICAL, 3. GitHub / gh CLI — MANUAL ONLY, 4. Git Safety (+13 more)

### Community 62 - "main.py"
Cohesion: 0.12
Nodes (7): init_db(), custom_404_handler(), custom_422_handler(), custom_500_handler(), health_check(), lifespan(), log_requests()

### Community 63 - "Phase 2: Create the Product Pipeline"
Cohesion: 0.10
Nodes (20): Blender Workflow, Deliverables, Endpoints, Example Upload Script (Python/Boto3), FastAPI Setup, Infrastructure Setup, Overview, Phase 2: Create the Product Pipeline (+12 more)

### Community 64 - "Aayam - Architecture & Technology Decisions"
Cohesion: 0.10
Nodes (19): 1. Door Scanning Workflow, 2. Product Visualization Workflow, 3. Compatibility Check Workflow, 4. QR Code Workflow, 5. Purchase/Quote Workflow, Aayam - Architecture & Technology Decisions, API Design, Data Flow Diagrams (+11 more)

### Community 65 - "Phase 6: Realistic Visualization"
Cohesion: 0.10
Nodes (20): Deliverables, Implementation: Runtime Material Switching, Implementation: Scale Calibration, Implementation: Shadow Receiver Shader (URP), Implementation: Unity Setup, Implementation: URP Lighting Setup, Overview, Phase 6: Realistic Visualization (+12 more)

### Community 66 - ".BuildUI"
Cohesion: 0.19
Nodes (3): AayamSceneBootstrapper, Systems, UIResult

### Community 67 - "Phase 7: Compatibility Engine"
Cohesion: 0.11
Nodes (18): Compatibility Rules Database, Deliverables, Edge Cases, FastAPI Implementation, Overview, Phase 7: Compatibility Engine, Rules Engine Implementation, Sample Rules (+10 more)

### Community 68 - "CameraPermissionHandler"
Cohesion: 0.27
Nodes (3): CameraPermissionHandler, HasPermission, IsRequestInProgress

### Community 69 - "Aayam Backend API"
Cohesion: 0.12
Nodes (16): 1. Prerequisites, 1. Prerequisites, 2. Configure Environment, 2. Create Virtual Environment, 3. Install Dependencies, 3. Start Containers, 4. Configure Environment, 5. Run Database Migrations (+8 more)

### Community 70 - "Step 1: Unity Project Setup — Detailed Implementation Guide"
Cohesion: 0.12
Nodes (16): 1.10 Assembly Definitions, 1.11 Core Bootstrap System, 1.12 Coding Standards, 1.1 Prerequisites, 1.2 Create Unity Project, 1.3 AR Foundation Package Installation, 1.4 XR Plugin Management Configuration, 1.5 URP Configuration (+8 more)

### Community 71 - "Phase 4: Door Coordinate System"
Cohesion: 0.12
Nodes (17): Component Zones, Deliverables, Mathematical Foundation, Mathematical Foundation, Overview, Perspective-n-Point (PnP) for Pose Estimation, Phase 4: Door Coordinate System, Plane Fitting from Point Cloud (+9 more)

### Community 72 - "Step 2: AR Session Configuration — Detailed Implementation Guide"
Cohesion: 0.12
Nodes (16): 2.10 Configuration Scriptable Objects, 2.1 Understanding the AR Architecture, 2.2 Create the AR Scene, 2.3 AR Session Manager Script, 2.4 Camera Configuration, 2.5 Device Capability Detection, 2.6 Supported Devices Matrix, 2.7 AR Session Lifecycle (+8 more)

### Community 73 - "Phase 1, Step 4: Object Placement — Detailed Implementation Guide"
Cohesion: 0.12
Nodes (16): 4.1 Understanding AR Raycasting, 4.2 AR Raycast Manager Setup, 4.3 Placement Indicator (Reticle), 4.4 Object Placement System, 4.5 AR Anchor System, 4.6 Vertical Surface Placement (Door-Specific), 4.7 Placement Feedback, 4.8 Multi-Object Placement (+8 more)

### Community 74 - "Step 6: 3D Model Loading — Detailed Implementation Guide"
Cohesion: 0.12
Nodes (15): 6.1 3D Format Selection, 6.2 GLB Runtime Loading, 6.3 Model Caching System, 6.5 Model Instantiation and Setup, 6.6 Test Models, 6.7 Blender Export Pipeline, 6.8 Model Preview System, Deliverables Checklist (+7 more)

### Community 75 - "Aayam: Project Context"
Cohesion: 0.12
Nodes (16): 10. Success Metrics, 11. What NOT to Do, 12. AI Strategy, 13. Team Plan, 14. Timeline Overview (8 Months), 15. QR Code Workflow, 1. Project Overview, 2. Problem Statement (+8 more)

### Community 76 - "assets.py"
Cohesion: 0.10
Nodes (7): verify_admin_key(), generate_qr(), _safe_segment(), upload_asset(), get_settings(), Settings, get_db()

### Community 77 - "ProductSelector"
Cohesion: 0.35
Nodes (3): ProductCard, ProductSelector, ProductUIItem

### Community 78 - "Phase 1, Step 8: Depth & Occlusion — Detailed Implementation Guide"
Cohesion: 0.11
Nodes (18): 8.10 Depth Visualization (Debug), 8.11 Performance Considerations, 8.1 Why Occlusion Matters, 8.2 Types of Occlusion, 8.3 AR Occlusion Manager Setup, 8.4 Environment Depth Texture, 8.5 Occlusion Shader, 8.6 Plane-Based Occlusion (Fallback System) (+10 more)

### Community 79 - "AnchorController"
Cohesion: 0.26
Nodes (3): AnchorController, AnchorCount, PlacedProduct

### Community 80 - "ObjectSelector"
Cohesion: 0.20
Nodes (4): DragHandler, ObjectSelector, CurrentSelection, HasSelection

### Community 81 - "Phase 0: Validate Before Coding"
Cohesion: 0.15
Nodes (12): Customer Survey Template, Deliverables Checklist, Documentation & Analysis Framework, Interview Script Template, Overview, Phase 0: Validate Before Coding, Risks & Mitigations, Step 1: Retailer Research (Week 1-2) (+4 more)

### Community 82 - "Aayam 🚪✨"
Cohesion: 0.15
Nodes (13): Aayam 🚪✨, 📐 Architecture, 📬 Contact, 🤝 Contributing, 📚 Documentation, 📄 License, 🎯 MVP Focus, 📁 Project Structure (+5 more)

### Community 83 - "system"
Cohesion: 0.15
Nodes (3): ARConfiguration, Aayam.Core.Bootstrap, Aayam.AR.Core

### Community 84 - "ObjectPlacementController"
Cohesion: 0.18
Nodes (3): ObjectPlacementController, CurrentState, PlacedObjectCount

### Community 87 - "generate_placeholder_models.py"
Cohesion: 0.22
Nodes (6): _box_geometry(), _build_glb_bytes(), _color_for_sku(), main(), _pad4(), 1.8 Git Setup

### Community 89 - "brands.py"
Cohesion: 0.40
Nodes (5): get_brand(), list_brand_products(), list_brands(), BrandListResponse, BrandResponse

### Community 90 - "categories.py"
Cohesion: 0.33
Nodes (4): list_categories(), list_category_products(), CategoryListResponse, CategoryResponse

### Community 91 - "com.unity.ext.nunit"
Cohesion: 0.18
Nodes (11): dependencies, depth, source, version, dependencies, depth, source, url (+3 more)

### Community 93 - "Phase 1: Build the AR Foundation"
Cohesion: 0.22
Nodes (9): Architecture, Deliverables, 📋 Detailed Step-by-Step Implementation Guides, Overview, Phase 1: Build the AR Foundation, Risks, Technical Requirements, 🗓️ Timeline (+1 more)

### Community 94 - "bug_report.md"
Cohesion: 0.22
Nodes (8): AR Context, Attachments, Bug Description, Device Information, Expected vs Actual Behavior, Performance Context, Severity, Steps to Reproduce

### Community 95 - "ProductApiService.cs"
Cohesion: 0.15
Nodes (8): DoorCompatibilityDto, ProductAsset3DDto, ProductDetailDto, ProductDimensionsDto, ProductListItemDto, ProductListResponseDto, ProductSummary, Aayam.Services

### Community 98 - "IEnumerator"
Cohesion: 0.20
Nodes (3): ComponentLifecycleTests, LifecycleTracker, Aayam.Tests.PlayMode

### Community 99 - "com.unity.modules.umbra"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.umbra

### Community 103 - "DoorSurfacePlacer"
Cohesion: 0.25
Nodes (7): DoorSurfacePlacer, ProductHeightType, Custom, Handle, Lock, Peephole, Deliverables Checklist

### Community 104 - "PlacementState"
Cohesion: 0.40
Nodes (5): PlacementState, Aiming, Idle, Placed, Placing

### Community 105 - "Current graph (built 2026-10-10)"
Cohesion: 0.40
Nodes (4): Current graph (built 2026-10-10), Graphify notes, Re-checking, Setup (2026-10-10)

### Community 106 - "6.4 Model Optimization for Mobile"
Cohesion: 0.40
Nodes (5): 6.4 Model Optimization for Mobile, LOD (Level of Detail), Material Setup, Polygon Budget, Texture Optimization

### Community 108 - "com.unity.modules.wind"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.wind

### Community 109 - "DepthTextureHandler"
Cohesion: 0.22
Nodes (3): DepthTextureHandler, CurrentDepthTexture, HasDepthData

### Community 110 - "EventBus.cs"
Cohesion: 0.20
Nodes (9): ARSessionStateChangedEvent, DoorCandidateDetectedEvent, ProductMovedEvent, ProductRemovedEvent, ProductSelectedEvent, ScreenChangedEvent, ScreenshotCapturedEvent, TrackingLostEvent (+1 more)

### Community 111 - "Aayam.Models"
Cohesion: 0.29
Nodes (3): DeepLinkParser, Aayam.Models, Aayam.Tests.EditMode

### Community 121 - "DoorCandidateDetector"
Cohesion: 0.33
Nodes (4): DoorCandidateDetector, BestCandidate, BestCandidateScore, HasConfidentCandidate

### Community 126 - "FloorPlaneDetector"
Cohesion: 0.29
Nodes (3): FloorPlaneDetector, FloorWorldPosition, HasFloor

### Community 132 - "9.10 QA Checklist Template"
Cohesion: 0.50
Nodes (4): 9.10 QA Checklist Template, Edge Case Checklist, Functional Checklist, Performance Checklist

### Community 133 - "9.6 Tracking Stability Tests"
Cohesion: 0.50
Nodes (4): 9.6 Tracking Stability Tests, Drift Test, Multi-Environment Test, Re-localization Test

### Community 134 - "Step 1: Database Schema Design (Week 1)"
Cohesion: 0.50
Nodes (4): PostgreSQL Schema, Product Data Model Example (JSON), Sample Data Insertion, Step 1: Database Schema Design (Week 1)

## Knowledge Gaps
- **199 isolated node(s):** `Count`, `TotalSize`, `None`, `CheckingAvailability`, `NotSupported` (+194 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1023 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **41 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Step 1: Unity Project Setup — Detailed Implementation Guide` connect `Step 1: Unity Project Setup — Detailed Implementation Guide` to `phase-1-ar-foundation.md`, `generate_placeholder_models.py`?**
  _High betweenness centrality (0.138) - this node is a cross-community bridge._
- **What connects `Count`, `TotalSize`, `None` to the rest of the system?**
  _199 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Test` be split into smaller, more focused modules?**
  _Cohesion score 0.06416275430359937 - nodes in this community are weakly interconnected._
- **Why does `main()` connect `generate_placeholder_models.py` to `get_storage_dir`, `Aayam — Claude Code Instructions`?**
  _High betweenness centrality (0.136) - this node is a cross-community bridge._
- **Should `dependencies` be split into smaller, more focused modules?**
  _Cohesion score 0.044444444444444446 - nodes in this community are weakly interconnected._
- **Why does `1.8 Git Setup` connect `generate_placeholder_models.py` to `Step 1: Unity Project Setup — Detailed Implementation Guide`?**
  _High betweenness centrality (0.134) - this node is a cross-community bridge._
- **Should `ARHUDController` be split into smaller, more focused modules?**
  _Cohesion score 0.06901960784313725 - nodes in this community are weakly interconnected._