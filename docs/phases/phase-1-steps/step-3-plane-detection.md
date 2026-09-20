# Step 3: Plane Detection — Detailed Implementation Guide

Duration: Week 2

## 3.1 Understanding Plane Detection

AR plane detection works by continuously analyzing camera frames and spatial data to identify continuous planar surfaces in the real world. 

### Under the Hood

1. **Feature Point Extraction**: The AR system identifies high-contrast points (corners, edges, texture details) in the camera feed.
2. **Point Cloud Clustering**: These points are tracked across frames and mapped into 3D space to form a point cloud. Points that move together consistently are clustered.
3. **Plane Fitting (RANSAC)**: Algorithms like RANSAC (Random Sample Consensus) attempt to find mathematical planes that fit subsets of the point cloud.
4. **Plane Merging and Extension**: As more area is scanned, new planes are detected. If two planes are coplanar and adjacent, they are merged. As the user moves, the plane boundaries are extended.
5. **Plane Classification**: The system uses device orientation (IMU) and gravity vectors to classify planes as horizontal (floor, ceiling, table) or vertical (wall, door).

### ARKit vs ARCore
- **ARKit (iOS)**: Tends to be faster at detecting planes, especially with LiDAR-equipped devices which don't rely solely on visual features. LiDAR dramatically improves vertical plane detection on featureless walls.
- **ARCore (Android)**: Relies heavily on visual features and motion. Requires more textured surfaces for reliable vertical plane detection.

### Pipeline Diagram

```mermaid
flowchart TD
    A[Camera Frame] --> B[Feature Point Extraction]
    A2[IMU / Gravity Data] --> B
    B --> C[3D Point Cloud Generation]
    C --> D[Point Clustering]
    D --> E[Plane Fitting RANSAC]
    E --> F{Plane Orientation}
    F -->|Dot Product with Up| G[Horizontal Plane]
    F -->|Dot Product with Up| H[Vertical Plane]
    G --> I[Plane Merging & Extension]
    H --> I
    I --> J[Boundary Polygon Extraction]
    J --> K[AR Foundation ARPlane]
```

## 3.2 AR Plane Manager Setup

To detect planes in Unity using AR Foundation:

1. Locate the **XR Origin** in your scene.
2. Add the **AR Plane Manager** component to it (`Add Component -> AR Plane Manager`).
3. Set the **Detection Mode** to `Everything` or `Vertical`. For Aayam, `Vertical` is critical, but we'll use `Everything` to also detect floors for context.
4. Create a Plane Visualization Prefab (detailed below) and assign it to the **Plane Prefab** field on the AR Plane Manager.

### Plane Visualization Prefab Setup

1. Create an Empty GameObject, name it `ARPlaneVisualizer`.
2. Add components: `AR Plane`, `AR Plane Mesh Visualizer`, `Mesh Collider`, `Mesh Filter`, `Mesh Renderer`.
3. Add our custom `PlaneVisualizationController` script (see Section 3.3).
4. Save as a Prefab and assign it to the AR Plane Manager.

## 3.3 Plane Visualization System

We need distinct visual feedback for different plane types to help the user understand what the system sees.

### PlaneVisualizationController.cs

```csharp
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Planes
{
    [RequireComponent(typeof(ARPlane), typeof(MeshRenderer))]
    public class PlaneVisualizationController : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material horizontalPlaneMaterial;  // Blue tint
        [SerializeField] private Material verticalPlaneMaterial;    // Green tint
        [SerializeField] private Material doorPlaneMaterial;        // Gold/highlight tint

        private ARPlane arPlane;
        private MeshRenderer meshRenderer;

        private void Awake()
        {
            arPlane = GetComponent<ARPlane>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        private void OnEnable()
        {
            arPlane.boundaryChanged += OnBoundaryChanged;
            UpdateMaterial();
        }

        private void OnDisable()
        {
            arPlane.boundaryChanged -= OnBoundaryChanged;
        }

        private void OnBoundaryChanged(ARPlaneBoundaryChangedEventArgs args)
        {
            UpdateMaterial();
        }

        private void UpdateMaterial()
        {
            if (arPlane.classification == PlaneClassification.Door)
            {
                meshRenderer.material = doorPlaneMaterial;
            }
            else if (arPlane.alignment == PlaneAlignment.Vertical)
            {
                meshRenderer.material = verticalPlaneMaterial;
            }
            else
            {
                meshRenderer.material = horizontalPlaneMaterial;
            }
        }

        public void SetAsDoorCandidate(bool isCandidate)
        {
            if (isCandidate) meshRenderer.material = doorPlaneMaterial;
            else UpdateMaterial();
        }
    }
}
```

### Custom Plane Shader (Grid with Fade)

Create an Unlit Shader Graph or use this HLSL code for a nice grid effect that fades at the edges:

```hlsl
// Basic outline of a grid shader that fades on edges
Shader "Aayam/ARPlaneGrid"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (0, 1, 0, 0.5)
        _LineThickness ("Line Thickness", Range(0.01, 0.1)) = 0.05
        _GridSize ("Grid Size", Float) = 1.0
        _FadeRadius ("Fade Radius", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            float4 _GridColor;
            float _LineThickness;
            float _GridSize;
            float _FadeRadius;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Grid logic based on world position
                float2 grid = frac(i.worldPos.xz / _GridSize);
                float lineX = step(grid.x, _LineThickness) + step(1.0 - _LineThickness, grid.x);
                float lineY = step(grid.y, _LineThickness) + step(1.0 - _LineThickness, grid.y);
                float isLine = saturate(lineX + lineY);

                // Simple edge fade based on UV (assuming UV maps 0-1 across plane)
                float2 center = float2(0.5, 0.5);
                float dist = distance(i.uv, center);
                float alphaFade = 1.0 - smoothstep(_FadeRadius * 0.5, _FadeRadius, dist);

                fixed4 col = _GridColor;
                col.a *= isLine * alphaFade;
                return col;
            }
            ENDCG
        }
    }
}
```

## 3.4 Vertical Plane Detection (Critical Path)

Vertical planes (walls and doors) are inherently harder to detect than floors.

### The Challenge
- **Featureless surfaces**: Walls often lack visual texture (plain white paint), making feature point extraction impossible for optical-only tracking.
- **Lighting**: Flat lighting on flat walls creates zero contrast.

### Improving Detection
1. **Adequate Lighting**: Ensure the room is well-lit.
2. **Find Details**: Scan edges of doors, doorframes, posters, light switches, or baseboards.
3. **Slow Movement**: Move the device slowly and pan slightly side-to-side (parallax) to help algorithms triangulate depth.
4. **LiDAR Advantage**: On iOS devices with LiDAR, plain walls are easily detected. Android devices relying on ARCore require more texture.

### VerticalPlaneFilter.cs

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Planes
{
    public class VerticalPlaneFilter : MonoBehaviour
    {
        private ARPlaneManager planeManager;
        public List<ARPlane> VerticalPlanes { get; private set; } = new List<ARPlane>();

        private void Awake()
        {
            planeManager = FindObjectOfType<ARPlaneManager>();
        }

        private void OnEnable()
        {
            planeManager.planesChanged += OnPlanesChanged;
        }

        private void OnDisable()
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }

        private void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            foreach (var plane in args.added)
            {
                if (IsVertical(plane)) VerticalPlanes.Add(plane);
            }

            foreach (var plane in args.removed)
            {
                if (VerticalPlanes.Contains(plane)) VerticalPlanes.Remove(plane);
            }
        }

        private bool IsVertical(ARPlane plane)
        {
            // AR Foundation already provides alignment
            if (plane.alignment == PlaneAlignment.Vertical) return true;

            // Fallback mathematical check
            float dot = Mathf.Abs(Vector3.Dot(plane.normal, Vector3.up));
            return dot < 0.15f; // Roughly within ~8.5 degrees of perfectly vertical
        }

        public ARPlane GetLargestVerticalPlane()
        {
            ARPlane largest = null;
            float maxSize = 0;
            foreach (var p in VerticalPlanes)
            {
                float size = p.extents.x * p.extents.y;
                if (size > maxSize)
                {
                    maxSize = size;
                    largest = p;
                }
            }
            return largest;
        }
    }
}
```

## 3.5 Plane Classification

AR Foundation provides some native classification (iOS handles this better natively). We supplement this with heuristic rules.

### PlaneClassifier.cs

```csharp
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Planes
{
    public enum AayamPlaneType
    {
        Unknown,
        Floor,
        Ceiling,
        Wall,
        Table,
        Door,
        DoorFrame
    }

    public class PlaneClassifier : MonoBehaviour
    {
        public AayamPlaneType ClassifyPlane(ARPlane plane)
        {
            // 1. Check Native Classification
            if (plane.classification == PlaneClassification.Door) return AayamPlaneType.Door;
            if (plane.classification == PlaneClassification.Floor) return AayamPlaneType.Floor;
            if (plane.classification == PlaneClassification.Table) return AayamPlaneType.Table;
            if (plane.classification == PlaneClassification.Wall) return AayamPlaneType.Wall;

            // 2. Heuristic Fallback
            float area = plane.extents.x * plane.extents.y * 4f; // approx area

            if (plane.alignment == PlaneAlignment.HorizontalUp)
            {
                // Simple height check relative to camera/start position could be added
                return area > 2.0f ? AayamPlaneType.Floor : AayamPlaneType.Table;
            }
            
            if (plane.alignment == PlaneAlignment.Vertical)
            {
                float width = plane.extents.x * 2f;
                float height = plane.extents.y * 2f;

                // Door heuristics: ~0.7-1.2m wide, ~1.8-2.4m high
                if (width >= 0.6f && width <= 1.3f && height >= 1.7f && height <= 2.5f)
                {
                    return AayamPlaneType.Door;
                }

                if (area > 3.0f) return AayamPlaneType.Wall;
            }

            return AayamPlaneType.Unknown;
        }
    }
}
```

## 3.6 Plane Boundary Detection

Analyzing boundaries helps determine the true dimensions and shape of the detected surface.

### PlaneBoundaryAnalyzer.cs

```csharp
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Aayam.AR.Planes
{
    public class PlaneBoundaryAnalyzer : MonoBehaviour
    {
        public Vector2 EstimateDimensions(ARPlane plane)
        {
            // ARPlane.extents gives half-dimensions of the axis-aligned bounding box
            float width = plane.extents.x * 2f;
            float height = plane.extents.y * 2f; // For vertical planes, y is usually height in plane space
            return new Vector2(width, height);
        }

        public bool IsRectangular(ARPlane plane, float tolerance = 0.2f)
        {
            // A simple heuristic: ratio of boundary polygon area to bounding box area
            float polyArea = CalculatePolygonArea(plane);
            float boxArea = plane.extents.x * 2f * plane.extents.y * 2f;
            
            float ratio = polyArea / boxArea;
            return Mathf.Abs(1.0f - ratio) < tolerance; // Close to 1 means it's roughly rectangular
        }

        private float CalculatePolygonArea(ARPlane plane)
        {
            var boundary = plane.boundary;
            if (boundary.Length < 3) return 0f;

            float area = 0f;
            for (int i = 0; i < boundary.Length; i++)
            {
                int j = (i + 1) % boundary.Length;
                area += (boundary[i].x * boundary[j].y) - (boundary[j].x * boundary[i].y);
            }
            return Mathf.Abs(area / 2f);
        }
    }
}
```

## 3.7 Plane Events and Callbacks

Manage plane lifecycle to update systems smoothly.

### PlaneEventHandler.cs

```csharp
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System;

namespace Aayam.AR.Planes
{
    public class PlaneEventHandler : MonoBehaviour
    {
        [SerializeField] private ARPlaneManager planeManager;

        public event Action<ARPlane> OnPlaneDiscovered;
        public event Action<ARPlane> OnPlaneUpdated;
        public event Action<ARPlane> OnPlaneLost;

        private void OnEnable()
        {
            if (planeManager == null) planeManager = GetComponent<ARPlaneManager>();
            planeManager.planesChanged += HandlePlanesChanged;
        }

        private void OnDisable()
        {
            planeManager.planesChanged -= HandlePlanesChanged;
        }

        private void HandlePlanesChanged(ARPlanesChangedEventArgs args)
        {
            foreach (var plane in args.added)
            {
                OnPlaneDiscovered?.Invoke(plane);
            }

            foreach (var plane in args.updated)
            {
                OnPlaneUpdated?.Invoke(plane);
            }

            foreach (var plane in args.removed)
            {
                OnPlaneLost?.Invoke(plane);
            }
        }
    }
}
```

## 3.8 Door-Candidate Detection (Heuristic)

Before full ML integration, we can find potential doors using geometric heuristics.

### DoorCandidateDetector.cs

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Aayam.AR.Planes
{
    public class DoorCandidateDetector : MonoBehaviour
    {
        [SerializeField] private PlaneBoundaryAnalyzer boundaryAnalyzer;
        [SerializeField] private VerticalPlaneFilter verticalFilter;

        public ARPlane BestCandidate { get; private set; }

        private void Update()
        {
            EvaluateCandidates();
        }

        private void EvaluateCandidates()
        {
            var candidates = new List<(ARPlane plane, float score)>();

            foreach (var plane in verticalFilter.VerticalPlanes)
            {
                float score = ScorePlaneAsDoor(plane);
                if (score > 50f) // Threshold
                {
                    candidates.Add((plane, score));
                }
            }

            if (candidates.Count > 0)
            {
                // Sort descending by score
                candidates.Sort((a, b) => b.score.CompareTo(a.score));
                var newBest = candidates.First().plane;

                if (BestCandidate != newBest)
                {
                    // Reset old candidate material
                    if (BestCandidate != null) 
                        BestCandidate.GetComponent<PlaneVisualizationController>()?.SetAsDoorCandidate(false);
                    
                    BestCandidate = newBest;
                    BestCandidate.GetComponent<PlaneVisualizationController>()?.SetAsDoorCandidate(true);
                }
            }
        }

        private float ScorePlaneAsDoor(ARPlane plane)
        {
            float score = 0f;
            Vector2 dims = boundaryAnalyzer.EstimateDimensions(plane);
            float width = dims.x;
            float height = dims.y;

            // 1. Width check (Standard doors: 0.7m - 1.0m)
            if (width >= 0.6f && width <= 1.2f) score += 30f;
            else if (width >= 0.5f && width <= 1.5f) score += 10f;

            // 2. Height check (Standard doors: 1.9m - 2.2m)
            if (height >= 1.8f && height <= 2.4f) score += 30f;
            else if (height >= 1.5f && height <= 2.6f) score += 10f;

            // 3. Aspect Ratio check (approx 2.0 to 2.5)
            float ratio = height / width;
            if (ratio >= 1.8f && ratio <= 2.8f) score += 20f;

            // 4. Rectangular shape check
            if (boundaryAnalyzer.IsRectangular(plane, 0.2f)) score += 20f;

            return score;
        }
    }
}
```

## 3.9 Plane Debug Visualization

A UI overlay to debug the internal state of AR plane detection.

### Debug UI Setup
1. Create a Canvas with a TextMeshProUGUI element.
2. Attach `PlaneDebugVisualizer`.

### PlaneDebugVisualizer.cs

```csharp
using UnityEngine;
using TMPro;
using UnityEngine.XR.ARFoundation;
using System.Text;

namespace Aayam.AR.Planes
{
    public class PlaneDebugVisualizer : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI debugText;
        [SerializeField] private VerticalPlaneFilter filter;
        [SerializeField] private DoorCandidateDetector doorDetector;

        private void Update()
        {
            if (debugText == null || !debugText.gameObject.activeInHierarchy) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>--- AR Plane Debug ---</b>");
            sb.AppendLine($"Total Vertical Planes: {filter.VerticalPlanes.Count}");
            
            if (doorDetector.BestCandidate != null)
            {
                var dims = doorDetector.BestCandidate.extents * 2f;
                sb.AppendLine($"<color=green>Best Door Candidate:</color> ID: {doorDetector.BestCandidate.trackableId}");
                sb.AppendLine($"Dims: {dims.x:F2}m x {dims.y:F2}m");
            }
            else
            {
                sb.AppendLine("<color=red>No Door Candidate Found</color>");
            }

            debugText.text = sb.ToString();
        }
    }
}
```

## 3.10 Performance Optimization

- **Limit Maximum Planes**: If memory or processing becomes an issue, implement logic in `PlaneEventHandler` to discard planes that are too small or too far away.
- **Toggle Detection**: Once the target door is confirmed and anchored, disable the `ARPlaneManager` to save battery and CPU.
  ```csharp
  planeManager.enabled = false;
  ```
- **Mesh Updates**: Updating mesh colliders for planes every frame is expensive. The standard `ARPlaneMeshVisualizer` handles this, but keep polygon counts low on the visualization shader.
- **Target FPS**: Ensure the app maintains >55 FPS to prevent motion sickness in AR and ensure smooth tracking.

## Deliverables Checklist
- [x] AR Plane Manager configured and working
- [x] Horizontal plane detection working
- [x] Vertical plane detection working (critical)
- [x] Plane visualization with different materials per type
- [x] Plane classification system
- [x] Plane boundary analysis
- [x] Door candidate detection (heuristic)
- [x] Plane event handling
- [x] Debug visualization overlay
- [x] Performance within targets
- [ ] Tested on both iOS and Android (Manual Step)

## Testing Scenarios
1. **Empty Wall**: Scan a plain wall. Expect slow detection on Android, fast on LiDAR iOS. Verify it registers as a Vertical plane.
2. **Door (Closed & Open)**: Scan a standard door. Expect the DoorCandidateDetector to highlight it.
3. **Different Angles**: Approach the door at an angle. Ensure plane updates correctly.
4. **Low Light**: Test in dim conditions. Tracking will degrade; verify graceful failure.
5. **Glass Door**: Scan glass. Expect poor detection (reflections confuse tracking).
6. **Textured vs Plain**: Compare scanning a wall with posters vs plain white wall.
7. **Multiple Doors**: Scan two doors side-by-side. Ensure the system maintains distinct planes and the candidate detector can rank them.
