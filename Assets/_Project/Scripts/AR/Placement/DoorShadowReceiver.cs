using UnityEngine;
using Aayam.AR.Planes;

namespace Aayam.AR.Placement
{
    /// <summary>
    /// Phase 6 Step 4: a transparent shadow-receiving quad sized to the
    /// already-solved door. Reuses Phase 4/5's door geometry
    /// (DoorCoordinateSystem/DoorGeometryState) rather than building a
    /// separate floor-shadow system - the door surface is the real-world
    /// plane most placed hardware's shadow actually falls on. Uses
    /// ARShadowReceiver.mat (Built-in RP - no URP installed in this
    /// project, so this can't be a Shader Graph receiver).
    ///
    /// Polls in Update rather than subscribing to an event, matching
    /// DoorEdgeCalibrationUI's own established pattern for "wait until the
    /// door is solved" checks.
    /// </summary>
    public class DoorShadowReceiver : MonoBehaviour
    {
        [SerializeField] private DoorCoordinateSystem _doorCoordinateSystem;
        [SerializeField] private DoorGeometryState _doorGeometry;
        [SerializeField] private Material _shadowReceiverMaterial;

        // Tiny offset along the door normal to avoid z-fighting with the
        // door's own visualized surface/markers.
        private const float NormalOffsetMeters = 0.002f;

        private GameObject _quad;

        private void Update()
        {
            if (_doorCoordinateSystem == null || !_doorCoordinateSystem.HasOrigin) return;
            if (_doorGeometry == null || !_doorGeometry.HasGeometry) return;

            EnsureQuad();
            PositionQuad();
        }

        private void EnsureQuad()
        {
            if (_quad != null) return;

            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad.name = "DoorShadowReceiver";
            Destroy(_quad.GetComponent<Collider>());

            var renderer = _quad.GetComponent<MeshRenderer>();
            if (_shadowReceiverMaterial != null)
            {
                renderer.material = _shadowReceiverMaterial;
            }
            else
            {
                Debug.LogWarning("[DoorShadowReceiver] _shadowReceiverMaterial not assigned - " +
                                  "falling back to CreatePrimitive's default material, which has " +
                                  "repeatedly shown as the missing-shader magenta fallback on-device " +
                                  "elsewhere in this project. Assign Assets/_Project/Materials/ARShadowReceiver.mat.");
            }
            renderer.receiveShadows = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private bool _loggedOnce;

        private void PositionQuad()
        {
            float width = _doorGeometry.DoorWidthMeters;
            float height = _doorGeometry.DoorHeightMeters;

            // Door-space convention (DoorCoordinateSystem's own doc):
            // origin at bottom-left, X = width axis, Y = height axis.
            Vector3 center = _doorCoordinateSystem.DoorToWorldSpace(
                new Vector3(width * 0.5f, height * 0.5f, NormalOffsetMeters));

            _quad.transform.SetPositionAndRotation(center, _doorCoordinateSystem.DoorOrigin.rotation);
            _quad.transform.localScale = new Vector3(width, height, 1f);

            if (!_loggedOnce)
            {
                _loggedOnce = true;
                Debug.Log($"[ShadowDiag] quad center={center:F3} size={width:F3}x{height:F3} " +
                          $"activeInHierarchy={_quad.activeInHierarchy} " +
                          $"rendererEnabled={_quad.GetComponent<MeshRenderer>().enabled} " +
                          $"material={_quad.GetComponent<MeshRenderer>().sharedMaterial?.name}");
            }
        }
    }
}
