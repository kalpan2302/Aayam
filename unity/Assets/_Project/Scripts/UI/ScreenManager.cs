using System.Collections.Generic;
using System;
using UnityEngine;

namespace Aayam.UI
{
    // ─── Screen Enum ──────────────────────────────────────────────

    public enum AppScreen
    {
        Loading,
        ARScan,          // Main AR experience
        ProductBrowser,  // Bottom-sheet product list
        ProductDetail,   // Product info card
        Settings,
        Debug
    }

    // ─── Screen Manager ───────────────────────────────────────────

    /// <summary>
    /// Controls which UI screen is active. Maintains a navigation back-stack.
    /// Each AppScreen maps to a root GameObject that is activated/deactivated.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        [Serializable]
        public class ScreenEntry
        {
            public AppScreen   Screen;
            public GameObject  Root;
        }

        // ─── Events ───────────────────────────────────────────────
        public static event Action<AppScreen, AppScreen> OnScreenChanged;  // (from, to)

        // ─── Inspector ────────────────────────────────────────────
        [Header("Screen Mappings")]
        [SerializeField] private List<ScreenEntry> _screens = new();

        [Header("Initial Screen")]
        [SerializeField] private AppScreen _initialScreen = AppScreen.Loading;

        // ─── State ────────────────────────────────────────────────
        public AppScreen CurrentScreen { get; private set; }
        private readonly Stack<AppScreen> _navStack = new();

        // ─── Lifecycle ────────────────────────────────────────────

        private void Start()
        {
            // Hide all first
            foreach (var entry in _screens)
                entry.Root?.SetActive(false);

            ShowScreen(_initialScreen);
        }

        // ─── Navigation ───────────────────────────────────────────

        /// <summary>Navigate to a screen. Push the current screen onto the back-stack.</summary>
        public void ShowScreen(AppScreen screen)
        {
            AppScreen previous = CurrentScreen;

            // Deactivate all, activate only target
            foreach (var entry in _screens)
                entry.Root?.SetActive(entry.Screen == screen);

            _navStack.Push(screen);
            CurrentScreen = screen;

            OnScreenChanged?.Invoke(previous, CurrentScreen);
            Debug.Log($"[ScreenManager] {previous} → {CurrentScreen}");
        }

        /// <summary>Navigate back to the previous screen.</summary>
        public void GoBack()
        {
            if (_navStack.Count <= 1) return;

            _navStack.Pop();                       // Remove current
            AppScreen previous = _navStack.Peek(); // Peek previous (not pop — ShowScreen will push again)

            // Avoid duplicate push: deactivate/activate without extra push
            AppScreen previous2 = CurrentScreen;
            foreach (var entry in _screens)
                entry.Root?.SetActive(entry.Screen == previous);

            CurrentScreen = previous;
            OnScreenChanged?.Invoke(previous2, CurrentScreen);
        }

        /// <summary>Returns true if the given screen is currently active.</summary>
        public bool IsActive(AppScreen screen) => CurrentScreen == screen;

        /// <summary>Returns the screen root GameObject or null.</summary>
        public GameObject GetRoot(AppScreen screen)
        {
            foreach (var entry in _screens)
                if (entry.Screen == screen) return entry.Root;
            return null;
        }
    }
}
