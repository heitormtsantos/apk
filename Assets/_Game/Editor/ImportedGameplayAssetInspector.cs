using System.Text;
using UnityEditor;
using UnityEngine;

namespace BattleRoyale.Editor
{
    public static class ImportedGameplayAssetInspector
    {
        public static void Dump()
        {
            var report = new StringBuilder();
            DumpModel("Assets/_Game/Resources/ImportedGameplay/Weapons/Weapons.fbx", report);
            DumpModel("Assets/_Game/Resources/ImportedGameplay/Parachute/tripo_convert_7677a47a-0ace-4ae8-9f32-4d51bafa5976.fbx", report);
            DumpModel("Assets/_Game/Resources/ImportedGameplay/DeathCrate/tripo_convert_4706240d-c3fe-4c4e-9ace-307d01f05418.fbx", report);
            System.IO.File.WriteAllText("ImportedGameplayAssetReport.txt", report.ToString());
            Debug.Log(report.ToString());
        }

        private static void DumpModel(string path, StringBuilder report)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            report.AppendLine($"MODEL|{path}|ROOT={root?.name ?? "NULL"}");
            if (root == null) return;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var renderers = transform.GetComponents<Renderer>();
                foreach (var renderer in renderers)
                {
                    var mesh = renderer is SkinnedMeshRenderer skinned
                        ? skinned.sharedMesh
                        : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    report.AppendLine($"NODE|{HierarchyPath(transform, root.transform)}|RENDERER={renderer.GetType().Name}|MESH={mesh?.name}|BOUNDS={renderer.bounds.size}|MATERIALS={string.Join(",", System.Array.ConvertAll(renderer.sharedMaterials, material => material != null ? material.name : "NULL"))}");
                }
            }
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                report.AppendLine($"SUBASSET|{asset.GetType().Name}|{asset.name}");
        }

        private static string HierarchyPath(Transform value, Transform root)
        {
            var path = value.name;
            while (value.parent != null && value != root)
            {
                value = value.parent;
                path = value.name + "/" + path;
            }
            return path;
        }
    }

    public static class ImportedGameplayAssetBuilder
    {
        private const string SourcePath = "Assets/_Game/Resources/ImportedGameplay/Weapons/Weapons.fbx";
        private const string OutputFolder = "Assets/_Game/Resources/ImportedGameplay/WeaponPrefabs";

        public static void BuildWeaponPrefabs()
        {
            EnsureFolder(OutputFolder);
            AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new System.InvalidOperationException($"Weapon FBX not imported: {SourcePath}");
            Create(source, "Assault_Rifles/ScarH", "ScarH");
            Create(source, "Spec_Ops/Kriss_Vector", "Kriss_Vector");
            Create(source, "Shotguns/Spas_12", "Spas_12");
            Create(source, "Snipers/L115_Awp", "L115_Awp");
            Create(source, "Pistols/Glock17", "Glock17");
            AssetDatabase.SaveAssets();
        }

        private static void Create(GameObject source, string hierarchyPath, string outputName)
        {
            var node = source.transform.Find(hierarchyPath);
            if (node == null) throw new System.InvalidOperationException($"Weapon node missing: {hierarchyPath}");
            var instance = Object.Instantiate(node.gameObject);
            instance.name = outputName;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(instance, $"{OutputFolder}/{outputName}.prefab");
            Object.DestroyImmediate(instance);
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
