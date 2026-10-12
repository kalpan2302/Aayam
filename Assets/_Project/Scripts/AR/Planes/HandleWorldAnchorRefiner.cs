using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Aayam.AR.Planes
{
    /// <summary>
    /// Phase 3 Step 8: converts VisualDoorSegmenter's handle position
    /// (normalized 0-1 within the door quad, from DoorGeometryExtractor)
    /// into a world-space point on the door, using the EXISTING geometric
    /// placement's AR plane as the coordinate frame - deliberately avoids
    /// projecting camera-image pixels through screen space at all, which is
    /// the risky mapping DoorConfidenceCoordinator's own doc comment flagged
    /// (the same bug class that cost time in Phase 2's QR scanner
    /// debugging). Instead: ARPlane.transform.TransformPoint(localOffset),
    /// where localOffset is built from the plane's own extents and the
    /// handle's normalized (u,v) - confirmed against ARPlane's source
    /// (`normal => transform.up`, boundary points in local-XZ "plane
    /// space") rather than assumed.
    ///
    /// Positions a visible marker at the result for verification - this is
    /// a visual stepping stone, not yet wired into actual product
    /// placement (ObjectPlacementController is untouched).
    /// </summary>
    public class HandleWorldAnchorRefiner : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private DoorCandidateDetector _geometricDetector;
        [SerializeField] private VisualDoorSegmenter _segmenter;

        [Header("Marker")]
        [SerializeField] private GameObject _markerPrefab;
        [SerializeField] private Color _markerColor = Color.magenta;

        public Vector3? LastWorldPosition { get; private set; }

        private GameObject _markerInstance;

        private void OnEnable()
        {
            if (_segmenter != null)
                _segmenter.OnDoorGeometry += HandleGeometry;
        }

        private void OnDisable()
        {
            if (_segmenter != null)
                _segmenter.OnDoorGeometry -= HandleGeometry;
        }

        private void OnDestroy()
        {
            if (_markerInstance != null)
                Destroy(_markerInstance);
        }

        private void HandleGeometry(VisualDoorSegmenter.DoorResult result)
        {
            if (!result.Ok) return;

            var plane = _geometricDetector != null ? _geometricDetector.BestCandidate : null;
            if (plane == null)
            {
                // No world-space frame to place into yet (no confident
                // geometric candidate) - this is a normal, expected state
                // while scanning, not an error.
                return;
            }

            Vector3? worldPos = ComputeWorldPosition(plane, result.HandleNormalized);
            if (worldPos == null) return;

            // Left in deliberately, not removed: the coordinate math checked
            // out against logged values (transform.right verified to equal
            // Cross(normal, Vector3.up), the theoretically-correct "viewer's
            // right" direction - see docs/phases/phase-3-implementation-plan.md
            // Step 8), but a full visual on-device confirmation (marker
            // landing visibly on the real handle) is still outstanding -
            // this is the evidence trail for whoever does that check next.
            Debug.Log($"[HandleWorldAnchorRefiner] planePos={plane.transform.position} " +
                      $"planeRight={plane.transform.right} planeUp={plane.transform.up} planeForward={plane.transform.forward} " +
                      $"extents={plane.extents} handleNorm={result.HandleNormalized} worldPos={worldPos.Value}");

            LastWorldPosition = worldPos;
            ShowMarker(worldPos.Value);
        }

        /// <summary>
        /// Exposed static + pure for unit testing against a mocked plane
        /// pose without needing a live AR session.
        /// </summary>
        public static Vector3? ComputeWorldPosition(ARPlane plane, Vector2 handleNormalized)
        {
            if (plane == null) return null;

            Vector2 extents = plane.extents; // half-width (x), half-height (y, i.e. local Z) - see ARPlane.extents doc
            // X sign flipped empirically (2026-10-12): the theoretical derivation
            // (transform.right == Cross(normal, Vector3.up) == textbook "viewer's
            // right hand" direction, verified by hand against two logged real
            // ARPlane instances) checked out on paper but was wrong in practice -
            // two independent on-device tests both placed the marker right-of-
            // center for a confirmed left-side handle (the 2D mask-space
            // detection itself was verified correct against the same photos, by
            // eye, before concluding the bug was here). Likely cause: ARKit's
            // plane normal doesn't point toward the viewer the way the
            // derivation assumed. Not re-derived a third time - fixed to match
            // observed on-device reality instead, which is the actual bar.
            float localX = (0.5f - handleNormalized.x) * 2f * extents.x;
            float localZ = (handleNormalized.y - 0.5f) * 2f * extents.y;

            // ARPlane boundary/extents are in "plane space": local X and Z,
            // with local Y = transform.up = the plane's normal (confirmed
            // via ARPlane.cs source, not assumed) - so Y=0 keeps the point
            // ON the plane's surface.
            var localOffset = new Vector3(localX, 0f, localZ);
            return plane.transform.TransformPoint(localOffset);
        }

        private void ShowMarker(Vector3 worldPosition)
        {
            if (_markerInstance == null)
            {
                if (_markerPrefab != null)
                {
                    _markerInstance = Instantiate(_markerPrefab);
                }
                else
                {
                    // CreatePrimitive's default material uses the Built-in
                    // Pipeline Standard shader, which renders magenta
                    // ("missing shader") under this project's URP - exactly
                    // what PlacementReticle's own undiagnosed fallback
                    // sphere hits (found and root-caused earlier this
                    // session, 2026-10-11). Assigning an explicit
                    // URP-compatible shader here instead of repeating it.
                    _markerInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    _markerInstance.transform.localScale = Vector3.one * 0.03f;
                    Destroy(_markerInstance.GetComponent<Collider>());
                    var renderer = _markerInstance.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        var shader = Shader.Find("Universal Render Pipeline/Unlit");
                        if (shader != null)
                        {
                            renderer.material = new Material(shader);
                            renderer.material.color = _markerColor;
                        }
                        else
                        {
                            renderer.material.color = _markerColor;
                        }
                    }
                }
            }
            _markerInstance.transform.position = worldPosition;
        }
    }
}
