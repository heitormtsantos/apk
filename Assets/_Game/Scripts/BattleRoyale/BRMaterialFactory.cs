using UnityEngine;

namespace BattleRoyale
{
    public static class BRMaterialFactory
    {
        public static Material Create(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            return material;
        }

        public static Material CreateEmissive(string name, Color color, float intensity = 2f)
        {
            var material = Create(name, color);
            if (!material.HasProperty("_EmissionColor")) return material;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * Mathf.Max(1f, intensity));
            return material;
        }

        public static Material CreateChecker(string name, Color a, Color b, int cells = 8)
        {
            var material = Create(name, Color.white);
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = $"{name} Texture" };
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Repeat;
            for (var y = 0; y < texture.height; y++)
            for (var x = 0; x < texture.width; x++)
            {
                var check = ((x / cells) + (y / cells)) % 2 == 0;
                texture.SetPixel(x, y, check ? a : b);
            }
            texture.Apply();
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            else material.mainTexture = texture;
            return material;
        }

        public static Material CreateTerrain(string name, Color low, Color high)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = $"{name} Texture",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 4
            };
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var broad = Mathf.PerlinNoise(x * 0.035f, y * 0.035f);
                var detail = Mathf.PerlinNoise(31f + x * 0.16f, 47f + y * 0.16f);
                var blend = 0.25f + broad * 0.42f + detail * 0.08f;
                texture.SetPixel(x, y, Color.Lerp(low, high, blend));
            }
            texture.Apply(true, false);
            var material = Create(name, Color.white);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            else material.mainTexture = texture;
            material.mainTextureScale = new Vector2(10f, 10f);
            return material;
        }
    }
}
