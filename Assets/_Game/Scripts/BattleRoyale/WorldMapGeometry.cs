using UnityEngine;

namespace BattleRoyale
{
    public enum WorldSurfaceRole
    {
        Unknown,
        Terrain,
        Road,
        Building,
        Rock,
        Bridge,
        Water
    }

    public static class WorldMapGeometry
    {
        public const string BermudaResourcePath = "UserMap/BermudaRemastered/bermuda_remastered";
        public const string BermudaMapName = "Bermuda Remastered Map";
        public const float BermudaWorldDiameter = 2032f;

        public static WorldSurfaceRole Classify(string hierarchyName)
        {
            var name = hierarchyName?.ToLowerInvariant() ?? string.Empty;
            if (name.Contains("water")) return WorldSurfaceRole.Water;
            if (name.Contains("stone_bridge") || name.Contains("bridge")) return WorldSurfaceRole.Bridge;
            if (name.Contains("rockwall") || name.Contains("rock")) return WorldSurfaceRole.Rock;
            if (name.Contains("building")) return WorldSurfaceRole.Building;
            if (name.Contains("terrain_road") || name.Contains("road")) return WorldSurfaceRole.Road;
            if (name.Contains("terrain")) return WorldSurfaceRole.Terrain;
            return WorldSurfaceRole.Unknown;
        }

        public static bool IsWater(string hierarchyName) => Classify(hierarchyName) == WorldSurfaceRole.Water;

        public static bool IsCollidable(string hierarchyName) => Classify(hierarchyName) is
            WorldSurfaceRole.Terrain or WorldSurfaceRole.Road or WorldSurfaceRole.Building
            or WorldSurfaceRole.Rock or WorldSurfaceRole.Bridge;

        public static bool IsPlayableBoundsSurface(string hierarchyName) => Classify(hierarchyName) is
            WorldSurfaceRole.Terrain or WorldSurfaceRole.Road;

        public static string HierarchyName(Transform transform)
        {
            if (transform == null) return string.Empty;
            var result = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                result = parent.name + "/" + result;
            return result;
        }

        public static bool TryCalculatePlayableBounds(Renderer[] renderers, out Bounds bounds)
        {
            if (TryEncapsulate(renderers, IsPlayableBoundsSurface, out bounds)) return true;
            return TryEncapsulate(renderers, path => !IsWater(path), out bounds);
        }

        public static int RecommendedLootCount(Bounds bounds, int participantCount)
        {
            var area = Mathf.Max(1f, bounds.size.x * bounds.size.z);
            var densityCount = Mathf.CeilToInt(area / 12000f);
            return Mathf.Clamp(densityCount, Mathf.Max(96, participantCount * 10), 320);
        }

        public static int RecommendedShopCount(Bounds bounds)
            => Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(bounds.size.x, bounds.size.z) / 300f), 3, 8);

        public static float RequiredUniformScale(Bounds importedBounds, float targetDiameter = BermudaWorldDiameter)
        {
            var diameter = Mathf.Max(importedBounds.size.x, importedBounds.size.z);
            return diameter > 0.001f ? Mathf.Max(0.001f, targetDiameter) / diameter : 1f;
        }

        private static bool TryEncapsulate(Renderer[] renderers, System.Func<string, bool> include,
            out Bounds bounds)
        {
            var found = false;
            bounds = default;
            if (renderers == null) return false;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !include(HierarchyName(renderer.transform))) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }
    }
}
