using UnityEngine;
using Aayam.AR.Occlusion;

namespace Aayam.AR.Measurement
{
    /// <summary>
    /// Phase 5 Stage 4: Try-pattern wrapper around DepthTextureHandler's
    /// existing screen-point depth query. Adds the "is this a real sample"
    /// distinction the raw API doesn't make (it returns 0f both when no
    /// depth texture exists yet AND when every pixel in the sample kernel
    /// was near-zero/invalid), plus a plausibility bound so a corrupt
    /// sample can't silently feed a door measurement.
    /// </summary>
    public class DepthMeasurementSampler
    {
        /// <summary>No real door is farther than this from the camera - guards against bad samples.</summary>
        public const float MaxPlausibleDepthMeters = 10f;

        private readonly DepthTextureHandler _depthHandler;

        public DepthMeasurementSampler(DepthTextureHandler depthHandler)
        {
            _depthHandler = depthHandler;
        }

        /// <summary>
        /// Samples the smoothed depth (meters, along the camera's forward
        /// axis - matching Camera.ScreenToWorldPoint's z convention) at a
        /// screen-space point. Returns false if no depth data is available
        /// yet or the sample is implausible.
        /// </summary>
        public bool TrySampleDepthMeters(Vector2 screenPoint, out float depthMeters)
        {
            depthMeters = 0f;
            if (_depthHandler == null || !_depthHandler.HasDepthData) return false;

            float sample = _depthHandler.GetSmoothedDepthAtScreenPoint(screenPoint);
            if (sample <= 0f || sample > MaxPlausibleDepthMeters) return false;

            depthMeters = sample;
            return true;
        }
    }
}
