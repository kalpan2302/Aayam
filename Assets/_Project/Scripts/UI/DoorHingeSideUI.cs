using UnityEngine;
using UnityEngine.UI;
using Aayam.AR.Planes;

namespace Aayam.UI
{
    /// <summary>
    /// Phase 4 Step 6's "tap to confirm hinge side" user-assist path.
    /// DoorOpeningDirection.WithUserOverride(bool hingeOnLeft) already exists
    /// and is fully implemented - this is purely the missing UI side that
    /// produces that bool. No AR raycasting needed (unlike
    /// DoorEdgeCalibrationUI) - just two buttons.
    /// </summary>
    public class DoorHingeSideUI : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private DoorGeometryState _doorGeometry;

        [Header("UI")]
        [SerializeField] private Button _hingeLeftButton;
        [SerializeField] private Button _hingeRightButton;
        [SerializeField] private Text _statusText;

        private void OnEnable()
        {
            _hingeLeftButton?.onClick.AddListener(HandleHingeLeftClicked);
            _hingeRightButton?.onClick.AddListener(HandleHingeRightClicked);
        }

        private void OnDisable()
        {
            _hingeLeftButton?.onClick.RemoveListener(HandleHingeLeftClicked);
            _hingeRightButton?.onClick.RemoveListener(HandleHingeRightClicked);
        }

        private void HandleHingeLeftClicked() => SetHingeSide(hingeOnLeft: true);

        private void HandleHingeRightClicked() => SetHingeSide(hingeOnLeft: false);

        private void SetHingeSide(bool hingeOnLeft)
        {
            if (_doorGeometry == null)
            {
                SetStatus("No door detected yet - point the camera at the door first.");
                return;
            }

            _doorGeometry.SetOpeningDirectionOverride(hingeOnLeft);
            SetStatus($"Hinge: {(hingeOnLeft ? "Left" : "Right")} - confirmed");
            Debug.Log($"[DoorHingeSideUI] hingeOnLeft={hingeOnLeft} (user confirmed)");
        }

        private void SetStatus(string message)
        {
            if (_statusText != null) _statusText.text = message;
        }
    }
}
