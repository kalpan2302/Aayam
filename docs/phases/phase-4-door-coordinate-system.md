# Phase 4: Door Coordinate System

**Duration:** Part of Month 3-4 (overlaps with Phase 3 and 5)

## Overview

- **Goal:** Establish a door-specific coordinate system for precise product placement.
- Products shouldn't be on random AR planes - they need door-relative coordinates.

### The Door Coordinate System
```text
        TOP
         ↑
         │
LEFT ← DOOR → RIGHT
         │
         ↓
       BOTTOM
```

**Coordinate axes:**
- X → horizontal (along door width)
- Y → vertical (along door height)
- Z → perpendicular to door surface (depth/thickness)

## Step 1: Door Detection to Coordinate System (Week 1)

From CV detection (Phase 3), extract:
- Four corners of the door
- Door center point
- Door normal vector

### Transformation Matrix
Create transformation matrices to convert between World Space and Door Space:
- World space → Door space
- Door space → World space

#### Mathematical Foundation
To map points between spaces, we use Homogeneous Transformation Matrices.

Let the Door Coordinate Frame be $D$ and World Coordinate Frame be $W$.

The transformation from Door to World space is given by a $4 \times 4$ matrix $T_{W \leftarrow D}$:
$$
T_{W \leftarrow D} = \begin{bmatrix}
R_{3\times3} & t_{3\times1} \\
0_{1\times3} & 1
\end{bmatrix}
$$
where $R$ is the rotation matrix aligning the door's axes to the world, and $t$ is the translation vector to the door's origin.

Conversely, World to Door transformation is the inverse:
$$
T_{D \leftarrow W} = T_{W \leftarrow D}^{-1} = \begin{bmatrix}
R^T & -R^T t \\
0 & 1
\end{bmatrix}
$$

**Unity C# Implementation:**
```csharp
using UnityEngine;

public class DoorCoordinateSystem : MonoBehaviour
{
    public Transform doorOrigin; // The detected origin of the door (e.g., bottom-left)

    /// <summary>
    /// Converts a world coordinate to door coordinate space.
    /// </summary>
    public Vector3 WorldToDoorSpace(Vector3 worldPoint)
    {
        return doorOrigin.InverseTransformPoint(worldPoint);
    }

    /// <summary>
    /// Converts a door space coordinate to world space.
    /// </summary>
    public Vector3 DoorToWorldSpace(Vector3 doorPoint)
    {
        return doorOrigin.TransformPoint(doorPoint);
    }

    /// <summary>
    /// Transforms a rotation from world space to door space.
    /// </summary>
    public Quaternion WorldToDoorRotation(Quaternion worldRot)
    {
        return Quaternion.Inverse(doorOrigin.rotation) * worldRot;
    }

    /// <summary>
    /// Transforms a rotation from door space to world space.
    /// </summary>
    public Quaternion DoorToWorldRotation(Quaternion doorRot)
    {
        return doorOrigin.rotation * doorRot;
    }
}
```

## Step 2: Door Reference Frame (Week 1-2)

- **Define door origin:** Bottom-left corner (or bottom-center, configurable).
- **Standard Door Dimensions (Indian Context):**
  - Dimensions: 2100mm x 900mm, 2100mm x 1000mm
  - Common thickness: 35mm, 38mm, 40mm, 45mm, 50mm

### Component Zones
Zones restrict where specific hardware can be placed:
- **Handle zone:** X = 680-770mm, Y = 950-1050mm
- **Lock zone:** X = 680-770mm, Y = 950-1050mm
- **Peephole zone:** X = Center (Width/2), Y = 1450-1550mm
- **Hinge zone:** X = 0 or X = Width, Y = Various locations along edge

## Step 3: Product Mounting Coordinates (Week 2)

Each product has anchor coordinates relative to the door's coordinate system.

```json
{
  "product": "HANDLE_001",
  "anchor": {
    "x_mm": 720,
    "y_mm": 1000, 
    "z_mm": 0
  },
  "rotation": {
    "rx": 0,
    "ry": 0,
    "rz": 0
  },
  "mounting_surface": "front"
}
```

- **Auto-placement:** Instantly position hardware based on its type in standard zones.
- **User adjustment:** Allow drag-and-drop within defined valid zones.
- **Constraint system:** Hard constraints (e.g., prevent placing handles near the top edge).

## Step 4: AR to Door Space Mapping (Week 2-3)

- Convert AR world coordinates to door coordinates seamlessly.
- **Tracking Drift Compensation:** Continuously refine the transformation matrix as the AR environment understanding improves (e.g., matching ARKit/ARCore planes to the initial door estimation).
- **Relocalization:** Save spatial anchors relative to the door. If tracking is lost, restoring the anchor restores the coordinate system.
- Support for multiple doors in the future by maintaining a list of active `DoorCoordinateSystem` instances.

**Unity C# Snippet (Drift Compensation Concept):**
```csharp
public void UpdateDoorAnchor(Pose newDetectedPose, float confidence)
{
    // Blend old and new pose based on confidence to reduce jitter
    if (confidence > threshold)
    {
        doorOrigin.position = Vector3.Lerp(doorOrigin.position, newDetectedPose.position, Time.deltaTime * lerpSpeed);
        doorOrigin.rotation = Quaternion.Slerp(doorOrigin.rotation, newDetectedPose.rotation, Time.deltaTime * lerpSpeed);
    }
}
```

## Step 5: Accuracy and Calibration (Week 3)

- **Ground Truth Comparison:** Validate placed objects against physical measurements.
- **Calibration Routine:**
  1. Detect the door using CV/AR.
  2. Ask the user to confirm door edges by tapping.
  3. User inputs a known physical measurement (e.g., actual door width in mm).
  4. System calibrates the internal scale factor to match the physical world.
- Error reporting and UI confidence scores (e.g., "Tracking Quality: Low - Scan more area").

## Step 6: Door Opening Direction (Week 3)

- Detect hinge side through structural analysis or user prompt.
- Infer opening direction (left-hand or right-hand, inward or outward).
- Critical for smart lock and latch compatibility.
- Initially user-assisted ("Tap the side with hinges").

## Mathematical Foundation

### Perspective-n-Point (PnP) for Pose Estimation
To find the camera pose relative to the door from 2D image detections, we use PnP. Given 3D points of the standard door corners $X_i \in \mathbb{R}^3$ and their 2D projections $x_i \in \mathbb{R}^2$, we solve for Rotation $R$ and Translation $t$ minimizing projection error:
$$
\min_{R, t} \sum_{i} \left\| x_i - \pi(R X_i + t) \right\|^2
$$
where $\pi$ is the camera projection function.

### Plane Fitting from Point Cloud
Using AR depth data, fit a plane equation $ax + by + cz + d = 0$ to points on the door surface using RANSAC or PCA. The normal vector $\mathbf{n} = [a, b, c]^T$ defines the Z-axis of our Door Coordinate System.

## Deliverables
- [ ] Door coordinate system implemented
- [ ] World ↔ Door space transforms
- [ ] Product anchor point system
- [ ] Auto-placement by product type
- [ ] User adjustment with constraints
- [ ] Calibration routine
- [ ] Opening direction detection
- [ ] Accuracy < 5mm for placement

## Risks
- **Tracking accuracy:** AR drift limits coordinate precision over long sessions.
- **Non-standard door shapes:** Arched or sliding doors complicate the standard coordinate model.
- **Perspective distortion:** Extreme viewing angles make edge detection and PnP inaccurate.
