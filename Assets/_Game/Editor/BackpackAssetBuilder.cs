using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BattleRoyale.Editor
{
    public static class BackpackAssetBuilder
    {
        private const string Root = "Assets/_Game/Resources/Backpacks";
        private const string Source = Root + "/Source";

        [MenuItem("Raven Drop/Backpacks/Rebuild Backpack Assets")]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Data");

            ConfigureTexture(Source + "/Lv1/Backpack_Lv1_BaseColor.jpg", false);
            ConfigureTexture(Source + "/Lv1/Backpack_Lv1_Normal.jpg", true);
            ConfigureTexture(Source + "/Lv2/Backpack_Lv2_BaseColor.jpg", false);
            ConfigureTexture(Source + "/Lv2/Backpack_Lv2_Normal.jpg", true);

            var material1 = BuildMaterial(1);
            var material2 = BuildMaterial(2);
            var visual1 = BuildVisualPrefab(1, Source + "/Lv1/Backpack_Lv1.fbx", material1);
            var visual2 = BuildVisualPrefab(2, Source + "/Lv2/Backpack_Lv2.fbx", material2);
            var visual3 = BuildVisualPrefab(3, Source + "/Lv2/Backpack_Lv2.fbx", material2);

            var level1 = BuildData(1, visual1, 50f, 60f, BackpackRarity.Common);
            var level2 = BuildData(2, visual2, 100f, 30f, BackpackRarity.Uncommon);
            var level3 = BuildData(3, visual3, 150f, 10f, BackpackRarity.Epic);
            BuildConfig(level1, level2, level3);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("BACKPACK_BUILD=PASS;LEVELS=3;LV3_VISUAL=LV2;BASE_CAPACITY=50;CAPACITIES=100,150,200");
        }

        private static Material BuildMaterial(int level)
        {
            var path = $"{Root}/Materials/Backpack_Lv{level}_URP.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = $"Backpack Lv{level} URP";
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{Source}/Lv{level}/Backpack_Lv{level}_BaseColor.jpg"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{Source}/Lv{level}/Backpack_Lv{level}_Normal.jpg"));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic", 0.08f);
            material.SetFloat("_Smoothness", 0.32f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject BuildVisualPrefab(int level, string modelPath, Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null) throw new FileNotFoundException($"Backpack model not imported: {modelPath}");
            var root = new GameObject($"Backpack_Lv{level}_Visual");
            var pivot = new GameObject("Visual").transform;
            pivot.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = level == 3 ? "Backpack Lv2 Model (Lv3 Placeholder)" : $"Backpack Lv{level} Model";
            model.transform.SetParent(pivot, false);
            model.transform.localPosition = Vector3.zero;
            // Source FBX files use Z as their vertical axis. Convert to Unity Y-up in the wrapper.
            model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            model.transform.localScale = Vector3.one;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

            if (TryBounds(model.transform, out var sourceBounds))
                Debug.Log($"BACKPACK_SOURCE=LV{level};SIZE={sourceBounds.size};CENTER={sourceBounds.center}");

            Normalize(model.transform, 0.54f);
            Center(model.transform);
            var path = $"{Root}/Prefabs/Backpack_Lv{level}_Visual.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static BackpackData BuildData(int level, GameObject visual, float additionalCapacity,
            float weight, BackpackRarity rarity)
        {
            var path = $"{Root}/Data/Backpack_Lv{level}.asset";
            var data = AssetDatabase.LoadAssetAtPath<BackpackData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BackpackData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.id = $"backpack_lv{level}";
            data.displayName = $"Mochila Lv.{level}";
            data.level = level;
            data.additionalCapacity = additionalCapacity;
            data.lootWeight = weight;
            data.rarity = rarity;
            data.canDrop = true;
            data.canSpawnAsLoot = true;
            data.worldLootPrefab = visual;
            data.equippedVisualPrefab = visual;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void BuildConfig(params BackpackData[] levels)
        {
            var path = Root + "/DefaultBackpackConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<BackpackConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BackpackConfig>();
                AssetDatabase.CreateAsset(config, path);
            }
            config.baseCapacity = 50f;
            config.ammoUnitCost = 0.05f;
            config.medKitUnitCost = 4f;
            config.glooWallUnitCost = 4f;
            config.grenadeUnitCost = 3f;
            config.attachmentCost = 2f;
            config.autoEquipHigherLevel = true;
            config.dropPreviousBackpack = false;
            config.dropBackpackOnDeath = true;
            config.levels = new List<BackpackData>(levels);
            EditorUtility.SetDirty(config);
        }

        private static void ConfigureTexture(string path, bool normal)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static void Normalize(Transform root, float targetLargest)
        {
            if (!TryBounds(root, out var bounds)) return;
            var largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest > 0.0001f) root.localScale *= targetLargest / largest;
        }

        private static void Center(Transform root)
        {
            if (!TryBounds(root, out var bounds)) return;
            var parent = root.parent;
            var center = parent.InverseTransformPoint(bounds.center);
            root.localPosition -= center;
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
