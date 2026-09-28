using UnityEngine;

namespace Aayam.AR.Placement
{
    /// <summary>
    /// Cross-platform placement feedback: haptics, audio, and visual scale-in animation.
    /// </summary>
    public static class PlacementFeedback
    {
        // â”€â”€â”€ Audio â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static AudioSource _audioSource;
        private static AudioClip   _placeClip;
        private static AudioClip   _removeClip;

        /// <summary>
        /// Call once at startup to provide audio assets.
        /// </summary>
        public static void Initialize(AudioSource source, AudioClip placeClip, AudioClip removeClip = null)
        {
            _audioSource = source;
            _placeClip   = placeClip;
            _removeClip  = removeClip;
        }

        // â”€â”€â”€ Success (Place) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Plays haptic + audio + triggers scale animation on the newly placed object.
        /// </summary>
        public static void PlaySuccessFeedback(GameObject placedObject = null)
        {
            PlayHaptic(HapticType.Light);
            PlayAudio(_placeClip);

            if (placedObject != null)
                TriggerScaleIn(placedObject);
        }

        // â”€â”€â”€ Remove â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public static void PlayRemoveFeedback()
        {
            PlayHaptic(HapticType.Medium);
            PlayAudio(_removeClip);
        }

        // â”€â”€â”€ Error â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public static void PlayErrorFeedback()
        {
            PlayHaptic(HapticType.Heavy);
        }

        // â”€â”€â”€ Haptics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void PlayHaptic(HapticType type)
        {
#if UNITY_IOS && !UNITY_EDITOR
            switch (type)
            {
                case HapticType.Light:
                    Handheld.Vibrate();
                    break;
                case HapticType.Medium:
                    Handheld.Vibrate();
                    break;
                case HapticType.Heavy:
                    Handheld.Vibrate();
                    break;
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        // â”€â”€â”€ Audio â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void PlayAudio(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }

        // â”€â”€â”€ Visual Scale-In Animation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Animates the placed object scaling from 0 to 1 ("pop-in" effect).
        /// Uses a simple coroutine â€” no external dependency required.
        /// </summary>
        private static void TriggerScaleIn(GameObject obj)
        {
            var runner = obj.GetComponent<PlacementAnimationRunner>();
            if (runner == null)
                runner = obj.AddComponent<PlacementAnimationRunner>();

            runner.PlayScaleIn();
        }

        private enum HapticType { Light, Medium, Heavy }
    }

    // â”€â”€â”€ Animation Runner (MonoBehaviour helper) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Simple MonoBehaviour to run the scale-in animation coroutine on a placed object.
    /// Added at runtime, removed after animation completes.
    /// </summary>
    public class PlacementAnimationRunner : MonoBehaviour
    {
        private const float AnimDuration = 0.35f;

        public void PlayScaleIn()
        {
            StopAllCoroutines();
            StartCoroutine(ScaleInCoroutine());
        }

        private System.Collections.IEnumerator ScaleInCoroutine()
        {
            float elapsed  = 0f;
            float duration = AnimDuration;

            transform.localScale = Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Ease-out back curve for a satisfying pop
                float eased = EaseOutBack(t);
                transform.localScale = Vector3.one * eased;
                yield return null;
            }

            transform.localScale = Vector3.one;
            Destroy(this);   // Remove runner component when done
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
