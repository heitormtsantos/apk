using UnityEngine;

namespace BattleRoyale
{
    public static class DeploymentSteering
    {
        public const float InputDeadZone = 0.1f;
        public const float IdleMomentum = 0.22f;

        public static bool HasInput(Vector2 input) => input.sqrMagnitude >= InputDeadZone * InputDeadZone;

        public static Vector3 CameraRelativeTravel(Vector2 input, Vector3 cameraForward,
            Vector3 cameraRight, Vector3 fallbackForward)
        {
            if (!HasInput(input)) return Vector3.zero;

            var forward = Flatten(cameraForward);
            if (forward.sqrMagnitude <= 0.000001f) forward = Flatten(fallbackForward);
            if (forward.sqrMagnitude <= 0.000001f) forward = Vector3.forward;
            forward.Normalize();

            var right = Flatten(cameraRight);
            right -= forward * Vector3.Dot(right, forward);
            if (right.sqrMagnitude <= 0.000001f) right = Vector3.Cross(Vector3.up, forward);
            right.Normalize();

            var magnitude = Mathf.Clamp01(input.magnitude);
            var direction = forward * input.y + right * input.x;
            if (direction.sqrMagnitude <= 0.000001f) return Vector3.zero;
            return direction.normalized * magnitude;
        }

        public static Vector3 IdleTravel(Vector3 previousDirection)
        {
            var planar = Flatten(previousDirection);
            return planar.sqrMagnitude <= 0.000001f
                ? Vector3.zero
                : planar.normalized * IdleMomentum;
        }

        public static Quaternion SmoothFacing(Quaternion current, Vector3 travel,
            float degreesPerSecond, float deltaTime)
        {
            var planar = Flatten(travel);
            if (planar.sqrMagnitude <= 0.000001f) return current;
            var target = Quaternion.LookRotation(planar.normalized, Vector3.up);
            return Quaternion.RotateTowards(current, target,
                Mathf.Max(0f, degreesPerSecond) * Mathf.Max(0f, deltaTime));
        }

        private static Vector3 Flatten(Vector3 value) => new(value.x, 0f, value.z);
    }
}
