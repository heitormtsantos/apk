using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleRoyale.Editor
{
    public static class CharacterAssetValidation
    {
        private const string ScarSourceResource = "Weapons/TastyTonyScarH/Low-Poly FN SCAR H";
        private const string ScarOptimizedResource = "Weapons/TastyTonyScarH/Optimized/SCAR_H_Optimized";
        private const string ScarOptimizedFolder = "Assets/_Game/Resources/Weapons/TastyTonyScarH/Optimized";

        [MenuItem("Raven Drop/Build Optimized Weapon Assets")]
        public static void BuildOptimizedWeaponAssets()
        {
            var source = Resources.Load<GameObject>(ScarSourceResource);
            if (source == null) throw new InvalidOperationException("SCAR-H source model is missing.");

            if (AssetDatabase.IsValidFolder(ScarOptimizedFolder)) AssetDatabase.DeleteAsset(ScarOptimizedFolder);
            EnsureFolder(ScarOptimizedFolder);

            var instance = UnityEngine.Object.Instantiate(source);
            instance.name = "SCAR_H_Optimized_Source";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null || !renderer.enabled) continue;
                for (var subMesh = 0; subMesh < filter.sharedMesh.subMeshCount; subMesh++)
                {
                    var material = renderer.sharedMaterials[Mathf.Min(subMesh, renderer.sharedMaterials.Length - 1)];
                    if (!groups.TryGetValue(material, out var combines))
                    {
                        combines = new List<CombineInstance>();
                        groups.Add(material, combines);
                    }
                    combines.Add(new CombineInstance
                    {
                        mesh = filter.sharedMesh,
                        subMeshIndex = subMesh,
                        transform = instance.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix
                    });
                }
            }

            var optimized = new GameObject("SCAR-H Optimized");
            var index = 0;
            foreach (var group in groups)
            {
                var mesh = new Mesh
                {
                    name = $"SCAR-H Combined {index:00}",
                    indexFormat = IndexFormat.UInt32
                };
                mesh.CombineMeshes(group.Value.ToArray(), true, true, false);
                mesh.RecalculateBounds();
                var meshPath = $"{ScarOptimizedFolder}/SCAR_H_Combined_{index:00}.asset";
                AssetDatabase.CreateAsset(mesh, meshPath);

                var part = new GameObject($"SCAR-H Material {index:00}");
                part.transform.SetParent(optimized.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
                index++;
            }

            PrefabUtility.SaveAsPrefabAsset(optimized, $"{ScarOptimizedFolder}/SCAR_H_Optimized.prefab");
            UnityEngine.Object.DestroyImmediate(optimized);
            UnityEngine.Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"BR_SCAR_OPTIMIZED=SOURCE_RENDERERS={source.GetComponentsInChildren<Renderer>(true).Length};" +
                $"OUTPUT_RENDERERS={groups.Count};RESOURCE={ScarOptimizedResource}");
        }

        [MenuItem("Raven Drop/Validate Imported Character Assets")]
        public static void ValidateImportedAssets()
        {
            var profile = CharacterVisualCatalog.ResolveAvailable(CharacterVisualCatalog.Player);
            if (profile != CharacterVisualCatalog.Player)
                throw new InvalidOperationException("Mixamo player profile resolved to its fallback.");

            var prefab = profile.LoadPrefab();
            var animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("Steve FBX does not expose a valid humanoid Avatar.");

            var clips = profile.LoadClips();
            var expected = new[]
            {
                "Idle", "Standard Walk", "Running", "Jumping", "Rifle Aiming Idle", "Rifle Idle",
                "Rifle Run", "Rifle Walk", "Crouch Idle", "Crouched Run", "Reloading"
            };
            foreach (var name in expected)
            {
                if (!clips.Any(clip => clip != null && clip.name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Required animation clip is missing: {name}");
            }

            var scar = Resources.Load<GameObject>(ScarOptimizedResource);
            if (scar == null || scar.GetComponentsInChildren<Renderer>(true).Length == 0)
                throw new InvalidOperationException("Optimized SCAR-H model did not import with a renderer.");

            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand == null) throw new InvalidOperationException("Humanoid Avatar has no right-hand bone.");

            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var vertices = renderers.Sum(renderer => renderer.sharedMesh != null ? renderer.sharedMesh.vertexCount : 0);
            Debug.Log($"BR_CHARACTER_ASSET=PROFILE={profile.Id};AVATAR={animator.avatar.name};" +
                $"CLIPS={clips.Length};SKINNED_RENDERERS={renderers.Length};VERTICES={vertices};" +
                $"RIGHT_HAND={rightHand.name};SCAR_RENDERERS={scar.GetComponentsInChildren<Renderer>(true).Length}");
        }

        private static void EnsureFolder(string folder)
        {
            var current = "Assets";
            foreach (var segment in folder.Substring("Assets/".Length).Split('/'))
            {
                var next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }
    }
}
