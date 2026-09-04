using UnityEditor;

namespace BattleRoyale.Editor
{
    public sealed class UserCharacterAssetPostprocessor : AssetPostprocessor
    {
        public const string SteveRoot = "Assets/_Game/Resources/UserCharacters/Steve/";
        public const string ScarRoot = "Assets/_Game/Resources/Weapons/TastyTonyScarH/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SteveRoot, System.StringComparison.Ordinal)) return;
            if (assetImporter is not TextureImporter importer) return;

            var isDetailMap = assetPath.Contains("Glossiness", System.StringComparison.OrdinalIgnoreCase)
                || assetPath.Contains("Specular", System.StringComparison.OrdinalIgnoreCase);
            importer.maxTextureSize = isDetailMap ? 1024 : 2048;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;

            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = isDetailMap ? 512 : 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 50;
            importer.SetPlatformTextureSettings(android);
        }

        private void OnPreprocessModel()
        {
            var steveAsset = assetPath.StartsWith(SteveRoot, System.StringComparison.Ordinal);
            var scarAsset = assetPath.StartsWith(ScarRoot, System.StringComparison.Ordinal);
            if (!steveAsset && !scarAsset) return;
            if (assetImporter is not ModelImporter importer) return;

            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;

            if (assetPath.Contains("/Animations/", System.StringComparison.Ordinal))
            {
                importer.importBlendShapes = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            }
            else if (scarAsset)
            {
                importer.importAnimation = false;
                importer.importBlendShapes = false;
            }
            else
            {
                importer.importBlendShapes = false;
            }
        }
    }
}
