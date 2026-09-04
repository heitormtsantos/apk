using System.IO;
using UnityEditor;
using UnityEngine;

namespace BattleRoyale.Editor
{
    public static class AircraftAssetBuilder
    {
        private const string Root = "Assets/_Game/Resources/Aircraft";
        private const string Source = Root + "/Source";
        private const string ModelPath = Source + "/BattleRoyaleCargoPlane.fbx";
        private const string PrefabPath = Root + "/CargoPlane_GameplayVisual.prefab";

        [MenuItem("Raven Drop/Aircraft/Rebuild Cargo Plane")]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Root + "/Materials");
            ConfigureTexture(Source + "/CargoPlane_Basecolor.jpg", false);
            ConfigureTexture(Source + "/CargoPlane_Normal.jpg", true);
            ConfigureTexture(Source + "/CargoPlane_Metallic.jpg", false);
            ConfigureTexture(Source + "/CargoPlane_Roughness.jpg", false);

            var material = BuildMaterial();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) throw new FileNotFoundException($"Aircraft model not imported: {ModelPath}");

            var root = new GameObject("CargoPlane_GameplayVisual");
            var axisFix = new GameObject("Z-Up To Unity Y-Up").transform;
            axisFix.SetParent(root.transform, false);
            axisFix.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "Cargo Plane Source Model";
            model.transform.SetParent(axisFix, false);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

            if (!TryBounds(root.transform, out var rawBounds))
                throw new InvalidDataException("Aircraft model has no renderers");
            var rawSize = rawBounds.size;
            var largest = Mathf.Max(rawSize.x, rawSize.y, rawSize.z);
            if (largest > 0.0001f) axisFix.localScale = Vector3.one / largest;
            Center(axisFix, root.transform);
            TryBounds(root.transform, out var normalizedBounds);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"AIRCRAFT_BUILD=PASS;RAW_SIZE={rawSize};NORMALIZED_SIZE={normalizedBounds.size};"
                + $"PREFAB={PrefabPath};FORWARD=+Z;MATERIAL=CargoPlane_URP");
        }

        private static Material BuildMaterial()
        {
            var path = Root + "/Materials/CargoPlane_URP.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = "Cargo Plane URP";
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/CargoPlane_Basecolor.jpg"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/CargoPlane_Normal.jpg"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/CargoPlane_Metallic.jpg"));
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Metallic", 0.18f);
            material.SetFloat("_Smoothness", 0.34f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTexture(string path, bool normal)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static void Center(Transform scaledRoot, Transform targetRoot)
        {
            if (!TryBounds(targetRoot, out var bounds)) return;
            scaledRoot.position -= bounds.center;
        }

        private static bool TryBounds(Transform root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }
            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
    }
}
