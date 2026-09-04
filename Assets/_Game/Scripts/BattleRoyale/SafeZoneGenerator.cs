using System;
using UnityEngine;

namespace BattleRoyale
{
    public readonly struct SafeZoneGenerationRequest
    {
        public SafeZoneGenerationRequest(Vector3 center, float currentRadius, float nextRadius,
            float offsetMultiplier, int seed, int attempts, Bounds playableBounds, bool constrainToBounds)
        {
            Center = center;
            CurrentRadius = currentRadius;
            NextRadius = nextRadius;
            OffsetMultiplier = offsetMultiplier;
            Seed = seed;
            Attempts = attempts;
            PlayableBounds = playableBounds;
            ConstrainToBounds = constrainToBounds;
        }

        public Vector3 Center { get; }
        public float CurrentRadius { get; }
        public float NextRadius { get; }
        public float OffsetMultiplier { get; }
        public int Seed { get; }
        public int Attempts { get; }
        public Bounds PlayableBounds { get; }
        public bool ConstrainToBounds { get; }
    }

    public static class SafeZoneGenerator
    {
        public static SafeZoneGeometry GenerateNextZone(SafeZoneGenerationRequest request,
            Func<Vector3, bool> validator = null)
        {
            var currentRadius = Mathf.Max(0f, request.CurrentRadius);
            var nextRadius = Mathf.Clamp(request.NextRadius, 0f, currentRadius);
            var maximumOffset = Mathf.Max(0f, currentRadius - nextRadius)
                * Mathf.Clamp01(request.OffsetMultiplier);
            var random = new System.Random(request.Seed);
            var attempts = Mathf.Max(1, request.Attempts);
            var fallback = ClampCenter(request.Center, nextRadius, request.PlayableBounds,
                request.ConstrainToBounds);

            for (var i = 0; i < attempts; i++)
            {
                var offset = RandomInsideCircle(random) * maximumOffset;
                var candidate = request.Center + new Vector3(offset.x, 0f, offset.y);
                candidate = ClampCenter(candidate, nextRadius, request.PlayableBounds, request.ConstrainToBounds);
                if (!ContainsCircle(request.Center, currentRadius, candidate, nextRadius, 0.001f)) continue;
                if (validator == null || validator(candidate)) return new SafeZoneGeometry(candidate, nextRadius);
            }

            return new SafeZoneGeometry(fallback, nextRadius);
        }

        public static bool ContainsCircle(Vector3 outerCenter, float outerRadius, Vector3 innerCenter,
            float innerRadius, float tolerance = 0f)
        {
            var delta = innerCenter - outerCenter;
            delta.y = 0f;
            return delta.magnitude + Mathf.Max(0f, innerRadius) <= Mathf.Max(0f, outerRadius) + tolerance;
        }

        private static Vector2 RandomInsideCircle(System.Random random)
        {
            var angle = (float)(random.NextDouble() * Math.PI * 2d);
            var radius = Mathf.Sqrt((float)random.NextDouble());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static Vector3 ClampCenter(Vector3 center, float radius, Bounds bounds, bool enabled)
        {
            if (!enabled || bounds.size.sqrMagnitude <= 0.001f) return center;
            var minX = bounds.min.x + radius;
            var maxX = bounds.max.x - radius;
            var minZ = bounds.min.z + radius;
            var maxZ = bounds.max.z - radius;
            if (minX > maxX) minX = maxX = bounds.center.x;
            if (minZ > maxZ) minZ = maxZ = bounds.center.z;
            center.x = Mathf.Clamp(center.x, minX, maxX);
            center.z = Mathf.Clamp(center.z, minZ, maxZ);
            return center;
        }
    }
}
