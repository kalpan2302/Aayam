using UnityEngine;

namespace Aayam.Utilities
{
    /// <summary>
    /// Common math utilities for AR coordinate calculations.
    /// </summary>
    public static class MathUtils
    {
        /// <summary>
        /// Convert millimeters to Unity units (meters).
        /// Unity uses 1 unit = 1 meter. AR Foundation works in meters.
        /// </summary>
        public static float MmToMeters(float mm) => mm * 0.001f;

        /// <summary>
        /// Convert meters to millimeters.
        /// </summary>
        public static float MetersToMm(float meters) => meters * 1000f;

        /// <summary>
        /// Convert centimeters to Unity units (meters).
        /// </summary>
        public static float CmToMeters(float cm) => cm * 0.01f;

        /// <summary>
        /// Check if a value is within a range (inclusive).
        /// </summary>
        public static bool IsInRange(float value, float min, float max)
        {
            return value >= min && value <= max;
        }

        /// <summary>
        /// Remap a value from one range to another.
        /// </summary>
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float t = Mathf.InverseLerp(fromMin, fromMax, value);
            return Mathf.Lerp(toMin, toMax, t);
        }

        /// <summary>
        /// Calculate the angle between two vectors projected onto a plane.
        /// Useful for rotation gestures on surfaces.
        /// </summary>
        public static float SignedAngleOnPlane(Vector3 from, Vector3 to, Vector3 planeNormal)
        {
            Vector3 projFrom = Vector3.ProjectOnPlane(from, planeNormal).normalized;
            Vector3 projTo = Vector3.ProjectOnPlane(to, planeNormal).normalized;
            return Vector3.SignedAngle(projFrom, projTo, planeNormal);
        }

        /// <summary>
        /// Check if a plane is vertical (within a tolerance angle in degrees).
        /// </summary>
        public static bool IsVerticalPlane(Vector3 planeNormal, float toleranceDegrees = 10f)
        {
            float dot = Mathf.Abs(Vector3.Dot(planeNormal, Vector3.up));
            float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            return angle > (90f - toleranceDegrees);
        }

        /// <summary>
        /// Check if a plane is horizontal (within a tolerance angle in degrees).
        /// </summary>
        public static bool IsHorizontalPlane(Vector3 planeNormal, float toleranceDegrees = 10f)
        {
            float dot = Mathf.Abs(Vector3.Dot(planeNormal, Vector3.up));
            float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            return angle < toleranceDegrees;
        }

        /// <summary>
        /// Estimate if a vertical plane's dimensions could be a door.
        /// Standard Indian doors: width 700-1200mm, height 1800-2400mm.
        /// </summary>
        public static bool CouldBeDoor(float widthMeters, float heightMeters)
        {
            float widthMm = MetersToMm(widthMeters);
            float heightMm = MetersToMm(heightMeters);

            return IsInRange(widthMm, 600f, 1300f) && IsInRange(heightMm, 1700f, 2500f);
        }

        /// <summary>
        /// Smooth damp a quaternion rotation (Unity doesn't provide this built-in).
        /// </summary>
        public static Quaternion SmoothDampQuaternion(
            Quaternion current,
            Quaternion target,
            ref Quaternion velocity,
            float smoothTime)
        {
            float dot = Quaternion.Dot(current, target);
            float direction = dot > 0f ? 1f : -1f;
            target.x *= direction;
            target.y *= direction;
            target.z *= direction;
            target.w *= direction;

            var result = new Vector4(
                Mathf.SmoothDamp(current.x, target.x, ref velocity.x, smoothTime),
                Mathf.SmoothDamp(current.y, target.y, ref velocity.y, smoothTime),
                Mathf.SmoothDamp(current.z, target.z, ref velocity.z, smoothTime),
                Mathf.SmoothDamp(current.w, target.w, ref velocity.w, smoothTime)
            ).normalized;

            return new Quaternion(result.x, result.y, result.z, result.w);
        }
    }
}
