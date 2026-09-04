using UnityEngine;
using System.Collections.Generic;

namespace BattleRoyale
{
    public static class BRAssetVisuals
    {
        private static readonly Dictionary<Material, Material> ConvertedMaterials = new();
        private static readonly HashSet<Texture> TunedTextures = new();

        public static void PrepareForUrp(GameObject root)
        {
            if (root == null) return;
            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var source = renderer.sharedMaterials;
                var converted = new Material[source.Length];
                for (var i = 0; i < source.Length; i++)
                {
                    converted[i] = ConvertMaterial(source[i], urpShader, renderer.name);
                    TuneTextureSampling(converted[i]);
                }
                renderer.sharedMaterials = converted;
            }
        }

        public static void TuneTextureSampling(Material material)
        {
            if (material == null) return;
            foreach (var property in material.GetTexturePropertyNames())
            {
                if (material.GetTexture(property) is not Texture2D texture || !TunedTextures.Add(texture)) continue;
                texture.filterMode = texture.mipmapCount > 1 ? FilterMode.Trilinear : FilterMode.Bilinear;
                texture.anisoLevel = Mathf.Max(4, texture.anisoLevel);
                if (texture.mipmapCount > 1) texture.mipMapBias = -0.18f;
            }
        }

        public static float NormalizeHeight(GameObject root, float targetHeight)
        {
            if (root == null || targetHeight <= 0f) return 1f;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds? visibleBounds = null;
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (visibleBounds.HasValue)
                {
                    var bounds = visibleBounds.Value;
                    bounds.Encapsulate(renderer.bounds);
                    visibleBounds = bounds;
                }
                else visibleBounds = renderer.bounds;
            }
            if (!visibleBounds.HasValue) return 1f;
            var factor = HeightScaleFactor(visibleBounds.Value.size.y, targetHeight);
            root.transform.localScale *= factor;
            return factor;
        }

        public static float HeightScaleFactor(float currentHeight, float targetHeight) =>
            currentHeight > 0.001f && targetHeight > 0f ? targetHeight / currentHeight : 1f;

        public static void ApplyBuildingPalette(GameObject root, int seed)
        {
            if (root == null) return;
            var walls = new[]
            {
                new Color(0.72f, 0.76f, 0.73f), new Color(0.66f, 0.45f, 0.36f),
                new Color(0.55f, 0.64f, 0.70f), new Color(0.76f, 0.67f, 0.48f)
            };
            var wall = walls[Mathf.Abs(seed) % walls.Length];
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var key = renderer.name.ToLowerInvariant();
                var source = renderer.sharedMaterials;
                var recolored = new Material[source.Length];
                for (var i = 0; i < source.Length; i++)
                {
                    var materialName = source[i] != null ? source[i].name.ToLowerInvariant() : string.Empty;
                    var combined = key + " " + materialName;
                    var color = combined.Contains("roof") ? new Color(0.12f, 0.15f, 0.17f) :
                        combined.Contains("window") || combined.Contains("glass") ? new Color(0.34f, 0.62f, 0.76f) :
                        combined.Contains("door") ? new Color(0.30f, 0.18f, 0.11f) : wall;
                    recolored[i] = BRMaterialFactory.Create($"{renderer.name} Building", color);
                }
                renderer.sharedMaterials = recolored;
            }
        }

        private static Material ConvertMaterial(Material source, Shader urpShader, string fallbackName)
        {
            if (source != null && source.shader == urpShader) return source;
            if (source != null && source.shader != null && source.shader.name.Contains("glTF")) return source;
            if (source != null && ConvertedMaterials.TryGetValue(source, out var cached) && cached != null) return cached;
            var material = new Material(urpShader)
            {
                name = source != null ? $"{source.name} URP" : $"{fallbackName} URP"
            };
            if (source == null) return material;
            if (source.HasProperty("baseColorFactor")) material.SetColor("_BaseColor", source.GetColor("baseColorFactor"));
            else if (source.HasProperty("_BaseColor")) material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            else if (source.HasProperty("_Color")) material.SetColor("_BaseColor", source.GetColor("_Color"));
            var texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") :
                source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") :
                source.HasProperty("baseColorTexture") ? source.GetTexture("baseColorTexture") : null;
            if (texture != null) material.SetTexture("_BaseMap", texture);
            if (source.HasProperty("_Metallic")) material.SetFloat("_Metallic", source.GetFloat("_Metallic"));
            if (source.HasProperty("_Glossiness")) material.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));
            ConvertedMaterials[source] = material;
            return material;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            ConvertedMaterials.Clear();
            TunedTextures.Clear();
        }
    }
}
