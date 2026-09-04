using UnityEditor;

namespace BattleRoyale.Editor
{
    public sealed class ImportedGameplayAssetPostprocessor : AssetPostprocessor
    {
        private const string Root = "Assets/_Game/Resources/ImportedGameplay/";
        private const string BermudaRoot = "Assets/_Game/Resources/UserMap/BermudaRemastered/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)
                && !assetPath.StartsWith(BermudaRoot, System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var lower = assetPath.ToLowerInvariant();
            if (lower.Contains("_normal")) importer.textureType = TextureImporterType.NormalMap;
            if (lower.Contains("_metallic") || lower.Contains("_roughness") || lower.Contains("_rm."))
                importer.sRGBTexture = false;
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.meshCompression = ModelImporterMeshCompression.Low;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
