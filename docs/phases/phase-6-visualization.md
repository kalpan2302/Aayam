# Phase 6: Realistic Visualization
**Duration:** 2-4 weeks (Month 5)

This phase transforms the app into a commercially impressive visualization tool. The primary goal is to ensure products look realistic when placed on the actual door in the physical space.

## Overview
- **Goal:** Products look realistic on the actual door.
- **Key Areas:** Correct scale, occlusion, lighting, shadows, and materials.

---

## Step 1: Correct Scale Rendering (Week 1)
- Product dimensions must match reality exactly.
- Unity transform scale from millimeters to Unity units (1 Unity unit = 1 meter).
- Scale validation system.
- Side-by-side comparison with real measurements.

### Implementation: Scale Calibration
```csharp
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ScaleCalibration : MonoBehaviour
{
    [SerializeField] private float realWorldWidthInMM = 150f; // e.g., 150mm door handle
    [SerializeField] private GameObject productPrefab;

    private GameObject instantiatedProduct;

    public void SpawnWithCorrectScale(Vector3 position, Quaternion rotation)
    {
        instantiatedProduct = Instantiate(productPrefab, position, rotation);
        
        // Unity units are in meters. Convert MM to Meters.
        float scaleInMeters = realWorldWidthInMM / 1000f;
        
        // Assuming the prefab's native scale in the model represents its real-world bounds at scale 1,
        // we might need to adjust it relative to its bounding box.
        Renderer renderer = instantiatedProduct.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            float currentWidth = renderer.bounds.size.x; // assuming X is width
            float scaleFactor = scaleInMeters / currentWidth;
            instantiatedProduct.transform.localScale *= scaleFactor;
        }
    }
}
```

---

## Step 2: Environment Lighting (Week 1)
- AR Environment Probe setup.
- Capture real-world lighting and apply to virtual objects via reflection probes.
- Light estimation (ambient intensity, color temperature, direction).
- ARKit light estimation and ARCore environmental HDR.

### Implementation: URP Lighting Setup
```csharp
using UnityEngine;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(ARCameraManager))]
public class ARLightEstimation : MonoBehaviour
{
    private ARCameraManager arCameraManager;
    [SerializeField] private Light directionalLight;

    void Awake()
    {
        arCameraManager = GetComponent<ARCameraManager>();
    }

    void OnEnable()
    {
        arCameraManager.frameReceived += OnCameraFrameReceived;
    }

    void OnDisable()
    {
        arCameraManager.frameReceived -= OnCameraFrameReceived;
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        if (args.lightEstimation.averageBrightness.HasValue)
        {
            directionalLight.intensity = args.lightEstimation.averageBrightness.Value;
        }

        if (args.lightEstimation.averageColorTemperature.HasValue)
        {
            directionalLight.colorTemperature = args.lightEstimation.averageColorTemperature.Value;
        }

        if (args.lightEstimation.mainLightDirection.HasValue)
        {
            directionalLight.transform.rotation = Quaternion.LookRotation(args.lightEstimation.mainLightDirection.Value);
        }

        if (args.lightEstimation.mainLightColor.HasValue)
        {
            directionalLight.color = args.lightEstimation.mainLightColor.Value;
        }
    }
}
```

---

## Step 3: Occlusion (Week 2)
- AR Occlusion Manager configuration.
- Environment occlusion:
  - Real objects in front of virtual objects render correctly.
  - Door handle behind your hand disappears.
- Depth-based occlusion (LiDAR/ToF devices).
- Fallback for non-depth devices:
  - Human body occlusion (ARKit).
  - Plane-based occlusion.

### Implementation: Unity Setup
1. Add the `AR Occlusion Manager` component to the AR Camera.
2. Set `Environment Depth Mode` to `Fastest` or `Best` (for LiDAR).
3. Set `Human Segmentation Stencil Mode` and `Human Segmentation Depth Mode` to `Fastest` for hand/body occlusion on non-LiDAR devices (ARKit primarily).

---

## Step 4: Shadows (Week 2)
- Shadow casting from virtual objects.
- Shadow receiving on real surfaces.
- Shadow plane setup (invisible plane that receives shadows).
- Soft shadows for realism.
- Shadow direction matching real light.
- URP shadow configuration.

### Implementation: Shadow Receiver Shader (URP)
Create an unlit shader in Shader Graph that multiplies the shadow attenuation with the camera feed to create a transparent shadow receiver plane.

1. **Material:** Create a material `ARShadowReceiver`.
2. **Shader:** Use a custom URP shader or an AR Foundation shadow receiver if available in the package.
3. Place a plane on the AR tracked planes and assign this material. Ensure the plane has `Receive Shadows` checked.

---

## Step 5: PBR Materials (Week 3)
- Physically Based Rendering for product materials.
- **Material types to support:**
  - Brushed metal (stainless steel, brass, chrome)
  - Matte finishes (matte black, matte white)
  - Glossy finishes (polished chrome, gold)
  - Wood grain (for wooden handles)
  - Textured surfaces (knurled, hammered)
- **Material parameter mapping:**
  - Albedo/Base Color
  - Metallic
  - Roughness/Smoothness
  - Normal map
  - Ambient Occlusion
- Environment reflections for metallic surfaces.

### URP Shader Graph Setup
- Use the `Lit` Shader Graph in URP.
- Connect base textures to Albedo.
- Ensure Metalness is set to 1.0 for metals, 0.0 for dielectrics.
- Map Roughness properly (low for polished, higher for brushed).
- Connect AR Environment probe reflection data to the environment lighting properties.

---

## Step 6: Material Variants (Week 3)
- Same product in different finishes.
- Runtime material switching.
- Material preview thumbnails.
- UI for material selection.
- **Example:** Handle in matte black / brushed nickel / polished brass.

### Implementation: Runtime Material Switching
```csharp
using UnityEngine;

public class MaterialSwitcher : MonoBehaviour
{
    [SerializeField] private Renderer productRenderer;
    [SerializeField] private Material[] materialVariants;

    public void SwitchMaterial(int index)
    {
        if (index >= 0 && index < materialVariants.Length)
        {
            productRenderer.material = materialVariants[index];
        }
    }
}
```

---

## Step 7: Product Comparison View (Week 4)
- Side-by-side comparison in AR.
- A/B comparison (tap to switch between two products).
- Before/after slider.
- Save comparison screenshots.
- Share comparison.

---

## Step 8: Screenshot & Video Capture (Week 4)
- High-quality AR screenshot.
- AR video recording.
- Watermark with Aayam branding.
- Share to WhatsApp, Instagram, etc.
- Before/after composites.

---

## Step 9: Performance Optimization
- **Frame rate targets:** 60 FPS
- **Memory budget:** < 200MB
- Draw call optimization.
- Shader complexity limits.
- LOD system for products.
- Texture streaming.
- GPU profiling checklist.

---

## Visual Quality Checklist
- [ ] Products are correct size
- [ ] Lighting matches environment
- [ ] Reflections look natural
- [ ] Shadows are present and directionally correct
- [ ] Occlusion works (hand in front of product)
- [ ] Materials look realistic (metal looks like metal)
- [ ] No z-fighting or visual artifacts
- [ ] Smooth 60 FPS

---

## Deliverables
- [ ] Correct scale rendering
- [ ] Environment-matched lighting
- [ ] Occlusion working (depth + fallback)
- [ ] Realistic shadows
- [ ] PBR materials for all products
- [ ] Material variants
- [ ] Product comparison view
- [ ] Screenshot/video capture
- [ ] 60 FPS on target devices
- [ ] Commercially impressive demo video

---

## Risks
- Performance on mid-range devices.
- Lighting estimation accuracy.
- Occlusion quality without LiDAR.
- Material appearance in different lighting conditions.
