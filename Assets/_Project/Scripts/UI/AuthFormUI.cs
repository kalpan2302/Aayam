using UnityEngine;
using UnityEngine.UI;
using Aayam.Core.Bootstrap;
using Aayam.Services;

namespace Aayam.UI
{
    /// <summary>
    /// Fitora sign-in/sign-up (2026-11-04, real backend auth) - one shared
    /// script bound twice, once per screen (Screen_SignIn with
    /// _isSignUpMode=false, Screen_SignUp with _isSignUpMode=true), since
    /// the two forms differ only by one extra field (name) and which
    /// endpoint they call. The "switch mode" link navigates to the other
    /// screen rather than toggling in place, matching the reference's
    /// separate-screens flow.
    /// </summary>
    public class AuthFormUI : MonoBehaviour
    {
        [SerializeField] private bool _isSignUpMode;
        [SerializeField] private ScreenManager _screenManager;
        [SerializeField] private AppScreen _onSuccessScreen = AppScreen.ProductBrowser;

        [Header("Fields")]
        [SerializeField] private GameObject _nameFieldContainer; // hidden in sign-in mode
        [SerializeField] private InputField _nameField;
        [SerializeField] private InputField _emailField;
        [SerializeField] private InputField _passwordField;

        [Header("Actions")]
        [SerializeField] private Button _submitButton;
        [SerializeField] private Text _submitButtonLabel;
        [SerializeField] private Text _errorText;
        [SerializeField] private Button _switchModeButton;

        private AuthApiService _authApi;
        private bool _submitting;

        private void Awake()
        {
            var config = AppConfig.Load();
            _authApi = new AuthApiService(config.ApiBaseUrl);
        }

        private void OnEnable()
        {
            _submitButton?.onClick.AddListener(HandleSubmit);
            _switchModeButton?.onClick.AddListener(HandleSwitchMode);
            _nameFieldContainer?.SetActive(_isSignUpMode);
            SetError("");
        }

        private void OnDisable()
        {
            _submitButton?.onClick.RemoveListener(HandleSubmit);
            _switchModeButton?.onClick.RemoveListener(HandleSwitchMode);
        }

        private void HandleSwitchMode()
        {
            _screenManager?.ShowScreen(_isSignUpMode ? AppScreen.SignIn : AppScreen.SignUp);
        }

        private async void HandleSubmit()
        {
            if (_submitting) return;

            string email = _emailField != null ? _emailField.text.Trim() : "";
            string password = _passwordField != null ? _passwordField.text : "";
            string name = _nameField != null ? _nameField.text.Trim() : "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                SetError("Enter your email and password.");
                return;
            }
            if (_isSignUpMode && string.IsNullOrEmpty(name))
            {
                SetError("Enter your name.");
                return;
            }

            _submitting = true;
            SetError("");
            if (_submitButtonLabel != null) _submitButtonLabel.text = _isSignUpMode ? "Creating account…" : "Signing in…";

            AuthResult result = _isSignUpMode
                ? await _authApi.SignUpAsync(email, password, name)
                : await _authApi.LoginAsync(email, password);

            _submitting = false;
            if (_submitButtonLabel != null) _submitButtonLabel.text = _isSignUpMode ? "Sign Up" : "Sign In";

            if (!result.Success)
            {
                SetError(result.ErrorMessage ?? "Something went wrong - try again.");
                return;
            }

            AuthSession.SetSession(result.Token);
            Debug.Log($"[AuthFormUI] {(_isSignUpMode ? "Signed up" : "Signed in")} as '{result.Token.User?.Email}'.");
            _screenManager?.ShowScreen(_onSuccessScreen);
        }

        private void SetError(string message)
        {
            if (_errorText != null)
            {
                _errorText.text = message;
                _errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            }
        }
    }
}
