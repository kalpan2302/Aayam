using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Aayam.AR.Planes;
using Aayam.Models;
using Aayam.Services;

namespace Aayam.UI
{
    /// <summary>
    /// Phase 4 Step 5, steps 1-2 of the roadmap's 4-step calibration routine:
    /// "user taps to confirm door edges" + "user inputs a known physical
    /// measurement" - steps 3-4 (the actual scale correction) already exist
    /// in DoorCalibration.cs. DoorCalibration.ComputeScaleCorrection needs
    /// the real tape measurement as measuredSizeMeters - there's no way to
    /// derive that from taps alone without being circular, so both a tap
    /// step (for a concrete, grounded "you are about to measure THIS span"
    /// visual) and a manual numeric entry are required here, not either/or.
    ///
    /// Tap handling matches ObjectPlacementController's live pattern (legacy
    /// Input, not the unused Input System GestureDetector stack), and
    /// intersects taps against the door's own already-solved plane via
    /// DoorEdgeCalibrator - NOT through PlacementReticle/ARRaycastManager,
    /// which this project's own 2026-10-25 investigation found gates on
    /// ARKit's separate (and for this door, undersized) plane tracking.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DoorEdgeCalibrationUI : MonoBehaviour
    {
        private enum CalibrationStep { Idle, WaitingForLeftEdge, WaitingForRightEdge, WaitingForMeasurement, Done }

        [Header("Dependencies")]
        [SerializeField] private DoorCoordinateSystem _doorCoordinateSystem;
        [SerializeField] private DoorGeometryState _doorGeometry;

        [Header("UI")]
        [SerializeField] private Button _startButton;
        [SerializeField] private Text _statusText;
        [SerializeField] private InputField _measurementInputField;
        [SerializeField] private Button _confirmButton;

        [Header("Markers")]
        [SerializeField] private Material _markerMaterialTemplate;

        private Camera _arCamera;
        private CalibrationStep _step = CalibrationStep.Idle;
        private Vector3 _leftEdgeWorld;
        private Vector3 _rightEdgeWorld;
        private float _measuredSpanMeters;
        private GameObject _leftMarker;
        private GameObject _rightMarker;
        private LineRenderer _measurementLine;
        private TextMesh _measurementLabel;

        // One local id per physical door this UI is attached to - reused
        // across repeated calibration confirms in the same run so their
        // history chains together (Phase 5 Step 7).
        private string _doorLocalId;

        private void Awake()
        {
            _arCamera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            _startButton?.onClick.AddListener(HandleStartClicked);
            _confirmButton?.onClick.AddListener(HandleConfirmClicked);
        }

        private void OnDisable()
        {
            _startButton?.onClick.RemoveListener(HandleStartClicked);
            _confirmButton?.onClick.RemoveListener(HandleConfirmClicked);
        }

        private void HandleStartClicked()
        {
            if (_doorCoordinateSystem == null || !_doorCoordinateSystem.HasOrigin)
            {
                SetStatus("No door detected yet - point the camera at the door first.");
                return;
            }

            _step = CalibrationStep.WaitingForLeftEdge;
            SetStatus("Tap the door's LEFT edge.");
        }

        private void Update()
        {
            if (_step != CalibrationStep.WaitingForLeftEdge && _step != CalibrationStep.WaitingForRightEdge)
                return;

            Vector2 screenPos;
            bool tapped = false;

#if UNITY_EDITOR
            tapped = Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject();
            screenPos = Input.mousePosition;
#else
            screenPos = default;
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                tapped = touch.phase == TouchPhase.Began &&
                         !EventSystem.current.IsPointerOverGameObject(touch.fingerId);
                screenPos = touch.position;
            }
#endif
            if (!tapped) return;

            Ray ray = _arCamera.ScreenPointToRay(screenPos);
            Transform doorOrigin = _doorCoordinateSystem.DoorOrigin;
            if (!DoorEdgeCalibrator.TryIntersectDoorPlane(ray, doorOrigin.position, doorOrigin.forward, out Vector3 worldPoint))
            {
                SetStatus("Couldn't find that point on the door plane - try tapping again.");
                return;
            }

            if (_step == CalibrationStep.WaitingForLeftEdge)
            {
                _leftEdgeWorld = worldPoint;
                PlaceMarker(ref _leftMarker, worldPoint, Color.yellow);
                _step = CalibrationStep.WaitingForRightEdge;
                SetStatus("Tap the door's RIGHT edge.");
            }
            else
            {
                _rightEdgeWorld = worldPoint;
                PlaceMarker(ref _rightMarker, worldPoint, Color.yellow);
                _measuredSpanMeters = DoorEdgeCalibrator.MeasureDoorSpaceSpan(_doorCoordinateSystem, _leftEdgeWorld, _rightEdgeWorld);
                UpdateMeasurementVisual();
                _step = CalibrationStep.WaitingForMeasurement;
                SetStatus($"AR thinks this span is {_measuredSpanMeters:F3}m. " +
                          "Measure it with a tape and enter the real value below, then Confirm.");
            }
        }

        private void LateUpdate()
        {
            if (_measurementLabel == null) return;
            _measurementLabel.transform.rotation = Quaternion.LookRotation(
                _measurementLabel.transform.position - _arCamera.transform.position);
        }

        private void HandleConfirmClicked()
        {
            if (_step != CalibrationStep.WaitingForMeasurement)
            {
                SetStatus("Tap both door edges first.");
                return;
            }

            if (_measurementInputField == null || !float.TryParse(_measurementInputField.text, out float measuredMeters) || measuredMeters <= 0f)
            {
                SetStatus("Enter a valid measurement in meters (e.g. 0.75) before confirming.");
                return;
            }

            var result = DoorCalibration.ComputeScaleCorrection(_doorGeometry.DoorWidthMeters, measuredMeters);
            if (!result.Ok)
            {
                SetStatus($"Calibration failed: {result.FailReason}");
                return;
            }

            _doorGeometry.ApplyScaleCorrection(result.ScaleCorrection);
            _step = CalibrationStep.Done;
            SetStatus($"Calibrated - scale correction {result.ScaleCorrection:F4} applied. " +
                      $"Door size is now {_doorGeometry.DoorWidthMeters:F3}m x {_doorGeometry.DoorHeightMeters:F3}m.");

            Debug.Log($"[DoorEdgeCalibrationUI] measuredSpan={_measuredSpanMeters:F4}m enteredMeters={measuredMeters:F4} " +
                      $"scaleCorrection={result.ScaleCorrection:F4} newWidth={_doorGeometry.DoorWidthMeters:F4} newHeight={_doorGeometry.DoorHeightMeters:F4}");

            SaveMeasurementHistory();
        }

        // ─── Phase 5 Step 7: persist the measurement, local-first ─────
        // Roadmap's own Step 6 confidence table puts user-assisted
        // measurements at 85-95%; 0.90 is the point estimate.
        private const float UserAssistedConfidence = 0.90f;

        private void SaveMeasurementHistory()
        {
            _doorLocalId ??= Guid.NewGuid().ToString();

            var entry = new MeasurementHistoryEntry
            {
                DoorLocalId = _doorLocalId,
                TimestampUtc = DateTime.UtcNow.ToString("o"),
                WidthMm = _doorGeometry.DoorWidthMeters * 1000f,
                HeightMm = _doorGeometry.DoorHeightMeters * 1000f,
                WidthConfidence = UserAssistedConfidence,
                HeightConfidence = UserAssistedConfidence,
                Source = MeasurementSource.UserAssisted,
            };

            MeasurementHistoryStore.SaveMeasurement(entry);
            Debug.Log($"[DoorEdgeCalibrationUI] Saved measurement history: widthMm={entry.WidthMm:F1} heightMm={entry.HeightMm:F1}");
        }

        private void PlaceMarker(ref GameObject marker, Vector3 worldPosition, Color color)
        {
            if (marker == null)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.transform.localScale = Vector3.one * 0.08f;
                Destroy(marker.GetComponent<Collider>());
                var renderer = marker.GetComponent<MeshRenderer>();
                if (_markerMaterialTemplate != null)
                {
                    renderer.material = new Material(_markerMaterialTemplate);
                    renderer.material.color = color;
                }
                else
                {
                    Debug.LogWarning("[DoorEdgeCalibrationUI] _markerMaterialTemplate not assigned - " +
                                      "falling back to CreatePrimitive's default material, which has " +
                                      "repeatedly shown as the missing-shader magenta fallback on-device " +
                                      "elsewhere in this project. Assign Assets/_Project/Materials/DoorPnPMarker.mat.");
                }
            }
            marker.transform.position = worldPosition;
        }

        // ─── Visual feedback: measurement line + dimension label ──────
        // Roadmap Step 4 ("measurement line with dimension") - previously
        // missing: the two edge markers rendered with no connecting
        // visual. TextMesh, not Canvas UI - no Canvas needed, matching the
        // lightweight marker-sphere style already used here; billboarded
        // toward _arCamera every frame in LateUpdate.
        private void UpdateMeasurementVisual()
        {
            if (_measurementLine == null)
            {
                var lineObj = new GameObject("MeasurementLine");
                _measurementLine = lineObj.AddComponent<LineRenderer>();
                _measurementLine.positionCount = 2;
                _measurementLine.startWidth = 0.01f;
                _measurementLine.endWidth = 0.01f;
                _measurementLine.material = _markerMaterialTemplate != null
                    ? new Material(_markerMaterialTemplate)
                    : new Material(Shader.Find("Sprites/Default"));
                _measurementLine.startColor = Color.cyan;
                _measurementLine.endColor = Color.cyan;
            }
            _measurementLine.SetPosition(0, _leftEdgeWorld);
            _measurementLine.SetPosition(1, _rightEdgeWorld);

            if (_measurementLabel == null)
            {
                var labelObj = new GameObject("MeasurementLabel");
                _measurementLabel = labelObj.AddComponent<TextMesh>();
                _measurementLabel.fontSize = 48;
                _measurementLabel.characterSize = 0.02f;
                _measurementLabel.anchor = TextAnchor.MiddleCenter;
                _measurementLabel.color = Color.cyan;
            }
            Vector3 midpoint = (_leftEdgeWorld + _rightEdgeWorld) * 0.5f;
            _measurementLabel.transform.position = midpoint + Vector3.up * 0.05f;
            _measurementLabel.text = $"{_measuredSpanMeters:F2}m";
        }

        private void SetStatus(string message)
        {
            if (_statusText != null) _statusText.text = message;
            Debug.Log($"[DoorEdgeCalibrationUI] {message}");
        }
    }
}
