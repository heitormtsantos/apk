using UnityEngine;

namespace BattleRoyale
{
    public static class GameplayTuning
    {
        public const float GroundAcceleration = 25f;
        public const float GroundBraking = 34f;
        public const float AirAcceleration = 7.5f;
        public const float AdsMoveMultiplier = 0.72f;

        public static Vector3 AcceleratePlanar(Vector3 current, Vector3 target, bool grounded, float deltaTime)
        {
            current.y = 0f;
            target.y = 0f;
            var braking = target.sqrMagnitude < current.sqrMagnitude && Vector3.Dot(current, target) >= 0f;
            var rate = grounded ? (braking ? GroundBraking : GroundAcceleration) : AirAcceleration;
            return Vector3.MoveTowards(current, target, Mathf.Max(0f, rate * deltaTime));
        }

        public static float MovementMultiplier(bool sprinting, bool crouching, bool aiming)
        {
            var multiplier = sprinting && !aiming ? 1.24f : 1f;
            if (crouching) multiplier *= 0.58f;
            if (aiming) multiplier *= AdsMoveMultiplier;
            return multiplier;
        }

        public static Vector2 RecoilStep(int shotIndex, float pitch, float yaw)
        {
            var index = Mathf.Max(0, shotIndex);
            var horizontalPattern = new[] { -0.22f, 0.12f, 0.34f, -0.08f, -0.38f, 0.2f, 0.42f, -0.18f };
            var climb = pitch * (0.82f + Mathf.Min(index, 8) * 0.055f);
            return new Vector2(climb, yaw * horizontalPattern[index % horizontalPattern.Length]);
        }

        public static float CrosshairGap(float spreadDegrees, float fieldOfView, float screenHeight)
        {
            var halfFov = Mathf.Max(1f, fieldOfView * 0.5f) * Mathf.Deg2Rad;
            var angular = Mathf.Tan(Mathf.Max(0f, spreadDegrees) * Mathf.Deg2Rad);
            return Mathf.Clamp(angular / Mathf.Tan(halfFov) * screenHeight * 0.42f, 3f, 24f);
        }
    }
}
