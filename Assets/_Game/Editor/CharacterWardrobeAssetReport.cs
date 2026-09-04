using System.Text;
using UnityEditor;
using UnityEngine;

namespace BattleRoyale.Editor
{
    public static class CharacterWardrobeAssetReport
    {
        public static void WriteSteveReport()
        {
            const string assetPath = "Assets/_Game/Resources/UserCharacters/Steve/Model/Steve.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogError($"WARDROBE_REPORT missing asset: {assetPath}");
                EditorApplication.Exit(1);
                return;
            }

            var report = new StringBuilder(4096);
            report.AppendLine($"WARDROBE_REPORT asset={assetPath}");
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            report.AppendLine($"renderers={renderers.Length}");
            foreach (var renderer in renderers)
            {
                report.AppendLine($"renderer={Path(renderer.transform, prefab.transform)} mesh={renderer.sharedMesh?.name} " +
                    $"vertices={renderer.sharedMesh?.vertexCount ?? 0} root={renderer.rootBone?.name ?? "none"} " +
                    $"bones={renderer.bones?.Length ?? 0} materials={renderer.sharedMaterials?.Length ?? 0} bounds={renderer.localBounds}");
                if (renderer.sharedMaterials == null) continue;
                foreach (var material in renderer.sharedMaterials)
                    report.AppendLine($"  material={material?.name ?? "none"}");
            }

            var animator = prefab.GetComponentInChildren<Animator>(true);
            report.AppendLine($"animator={animator?.name ?? "none"} humanoid={animator?.avatar?.isHuman ?? false}");
            if (animator != null)
                foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
                    report.AppendLine($"bone={Path(bone, animator.transform)}");
            Debug.Log(report.ToString());
        }

        private static string Path(Transform item, Transform root)
        {
            if (item == null) return "none";
            var value = item.name;
            while (item.parent != null && item != root)
            {
                item = item.parent;
                if (item != root) value = item.name + "/" + value;
            }
            return value;
        }
    }
}
