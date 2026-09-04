using UnityEngine;

namespace BattleRoyale
{
    public static class PlayableArea
    {
        private static Bounds bounds = new(Vector3.zero, new Vector3(180f, 40f, 180f));

        public static Bounds Bounds => bounds;
        public static Vector2 Size => new(bounds.size.x, bounds.size.z);
        public static Vector3 Center => new(bounds.center.x, 0f, bounds.center.z);

        public static void Configure(Bounds value)
        {
            value.Expand(new Vector3(12f, 0f, 12f));
            bounds = value;
        }

        public static Vector3 Sample(System.Random random, float inset = 2f)
        {
            var min = bounds.min + new Vector3(inset, 0f, inset);
            var max = bounds.max - new Vector3(inset, 0f, inset);
            return new Vector3(
                Mathf.Lerp(min.x, max.x, (float)random.NextDouble()),
                bounds.max.y + 24f,
                Mathf.Lerp(min.z, max.z, (float)random.NextDouble()));
        }

        public static Vector3 Clamp(Vector3 point, float inset = 0f)
        {
            point.x = Mathf.Clamp(point.x, bounds.min.x + inset, bounds.max.x - inset);
            point.z = Mathf.Clamp(point.z, bounds.min.z + inset, bounds.max.z - inset);
            return point;
        }

        public static bool TryGround(Vector3 sample, out Vector3 position, float clearanceRadius = 0.38f)
        {
            var origin = new Vector3(sample.x, bounds.max.y + 40f, sample.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, bounds.size.y + 90f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.normal.y < 0.68f) continue;
                var name = hit.collider.gameObject.name;
                if (name.Contains("Water") || name.Contains("Technical Ground")) continue;
                var feet = hit.point + Vector3.up * 0.05f;
                if (clearanceRadius > 0f && Physics.CheckCapsule(feet + Vector3.up * 0.35f,
                        feet + Vector3.up * 1.41f, clearanceRadius, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                position = feet;
                return true;
            }
            position = default;
            return false;
        }

        public static bool TryFindOpenGround(Vector3 preferred, out Vector3 position)
        {
            var found = false;
            var bestScore = float.NegativeInfinity;
            position = preferred;
            for (var x = 1; x < 8; x++)
            for (var z = 1; z < 8; z++)
            {
                var candidate = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, x / 8f), 0f,
                    Mathf.Lerp(bounds.min.z, bounds.max.z, z / 8f));
                if (!TryGround(candidate, out var grounded)) continue;
                var score = OpenSpaceScore(grounded) - Vector3.Distance(grounded, preferred) * 0.08f
                    - Mathf.Max(0f, grounded.y - 3.5f) * 2f;
                if (score <= bestScore) continue;
                bestScore = score;
                position = grounded;
                found = true;
            }
            return found || TryGround(preferred, out position);
        }

        private static float OpenSpaceScore(Vector3 position)
        {
            var score = 0f;
            var origin = position + Vector3.up * 1.05f;
            if (Physics.Raycast(origin, Vector3.up, 8f, ~0, QueryTriggerInteraction.Ignore)) score -= 120f;
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI * 2f / 12f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                score += Physics.Raycast(origin, direction, out var hit, 9f, ~0, QueryTriggerInteraction.Ignore)
                    ? hit.distance : 9f;
            }
            return score;
        }
    }
}
