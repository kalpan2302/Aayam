using UnityEngine;
using UnityEngine.UI;
using Aayam.Services;

namespace Aayam.UI
{
    /// <summary>Fitora profile screen (2026-11-04) - shows the signed-in user's
    /// name/email (from AuthSession) and logs out back to Sign In.</summary>
    public class ProfileScreenUI : MonoBehaviour
    {
        [SerializeField] private ScreenManager _screenManager;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _emailText;
        [SerializeField] private Button _logoutButton;

        private void OnEnable()
        {
            _logoutButton?.onClick.AddListener(HandleLogout);
            Refresh();
        }

        private void OnDisable()
        {
            _logoutButton?.onClick.RemoveListener(HandleLogout);
        }

        private void Refresh()
        {
            if (!AuthSession.IsLoggedIn)
            {
                _screenManager?.ShowScreen(AppScreen.SignIn);
                return;
            }
            if (_nameText != null) _nameText.text = AuthSession.UserName;
            if (_emailText != null) _emailText.text = AuthSession.UserEmail;
        }

        private void HandleLogout()
        {
            AuthSession.Clear();
            _screenManager?.ShowScreen(AppScreen.SignIn);
        }
    }
}
