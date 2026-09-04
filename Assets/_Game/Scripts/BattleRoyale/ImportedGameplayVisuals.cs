using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public static class ImportedGameplayVisuals
    {
        public const string WeaponPrefabRoot = "ImportedGameplay/WeaponPrefabs/";
        public const string ParachuteModel =
            "ImportedGameplay/Parachute/tripo_convert_7677a47a-0ace-4ae8-9f32-4d51bafa5976";
        public const string ParachuteTextureRoot =
            "ImportedGameplay/Parachute/tripo_convert_7677a47a-0ace-4ae8-9f32-4d51bafa5976.fbm/parachute_canopy_3d_model_";
        public const string DeathCrateModel =
            "ImportedGameplay/DeathCrate/tripo_convert_4706240d-c3fe-4c4e-9ace-307d01f05418";
        public const string DeathCrateTextureRoot =
            "ImportedGameplay/DeathCrate/tripo_convert_4706240d-c3fe-4c4e-9ace-307d01f05418.fbm/futuristic_crate_3d_model_";

        private static readonly Dictionary<string, Material> Materials = new();

        public static GameObject InstantiateTextured(string modelPath, string textureRoot,
            Transform parent, string instanceName, float metallic, float smoothness)
        {
            var prefab = Resources.Load<GameObject>(modelPath);
            if (prefab == null) return null;
            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = instanceName;
            ApplyPbrMaterial(instance, textureRoot, metallic, smoothness);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                Release(collider);
            return instance;
        }

        public static void NormalizeLargestDimension(GameObject root, float targetSize)
        {
            if (root == null || targetSize <= 0f || !TryBounds(root, out var bounds)) return;
            var largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest <= 0.001f) return;
            root.transform.localScale *= targetSize / largest;
        }

        public static void AlignBottomToParent(GameObject root, float localGroundY = 0f)
        {
            if (root == null || root.transform.parent == null || !TryBounds(root, out var bounds)) return;
            var parentGround = root.transform.parent.TransformPoint(Vector3.up * localGroundY).y;
            root.transform.position += Vector3.up * (parentGround - bounds.min.y);
        }

        private static void ApplyPbrMaterial(GameObject root, string textureRoot,
            float metallic, float smoothness)
        {
            var material = MaterialFor(textureRoot, metallic, smoothness);
            if (material == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var replacements = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (var index = 0; index < replacements.Length; index++) replacements[index] = material;
                renderer.sharedMaterials = replacements;
            }
        }

        private static Material MaterialFor(string textureRoot, float metallic, float smoothness)
        {
            if (Materials.TryGetValue(textureRoot, out var cached) && cached != null) return cached;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            var material = new Material(shader) { name = $"{textureRoot} PBR" };
            var baseColor = Resources.Load<Texture2D>(textureRoot + "basecolor");
            var normal = Resources.Load<Texture2D>(textureRoot + "normal");
            var metallicMap = Resources.Load<Texture2D>(textureRoot + "metallic");
            if (baseColor != null) material.SetTexture("_BaseMap", baseColor);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (metallicMap != null)
            {
                material.SetTexture("_MetallicGlossMap", metallicMap);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            Materials[textureRoot] = material;
            return material;
        }

        private static bool TryBounds(GameObject root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }
            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            return true;
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => Materials.Clear();
    }
}
