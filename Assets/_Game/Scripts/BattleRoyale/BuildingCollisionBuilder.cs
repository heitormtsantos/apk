using UnityEngine;

namespace BattleRoyale
{
    public static class BuildingCollisionBuilder
    {
        public static int Build(GameObject building)
        {
            if (building == null) return 0;
            foreach (var collider in building.GetComponentsInChildren<Collider>(true))
                Remove(collider);

            var created = 0;
            foreach (var filter in building.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || ShouldIgnore(HierarchyName(filter.transform))) continue;
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                created++;
            }

            if (created == 0)
            {
                var fallback = building.AddComponent<BoxCollider>();
                FitToRenderers(building, fallback);
                Debug.LogWarning($"BuildingCollisionBuilder: {building.name} has no collision mesh; fitted fallback collider added.");
                return 1;
            }
            return created;
        }

        public static int BuildWorldMap(GameObject map)
        {
            if (map == null) return 0;
            foreach (var collider in map.GetComponentsInChildren<Collider>(true)) Remove(collider);
            var created = 0;
            foreach (var filter in map.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var path = WorldMapGeometry.HierarchyName(filter.transform);
                if (!WorldMapGeometry.IsCollidable(path)) continue;
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                created++;
            }
            return created;
        }

        public static void BuildBox(GameObject prop)
        {
            if (prop == null) return;
            foreach (var collider in prop.GetComponentsInChildren<Collider>(true)) Remove(collider);
            var box = prop.AddComponent<BoxCollider>();
            FitToRenderers(prop, box);
        }

        private static void Remove(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        private static bool ShouldIgnore(string objectName)
        {
            var name = objectName.ToLowerInvariant();
            return name.Contains("glass") || name.Contains("window") || name.Contains("curtain")
                || name.Contains("sign") || name.Contains("decal") || name.Contains("light");
        }

        private static string HierarchyName(Transform transform)
        {
            var result = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                result += "/" + parent.name;
            return result;
        }

        private static void FitToRenderers(GameObject root, BoxCollider collider)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            var scale = root.transform.lossyScale;
            collider.size = new Vector3(
                bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
                bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
        }
    }
}
