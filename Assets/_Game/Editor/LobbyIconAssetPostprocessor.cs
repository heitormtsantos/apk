using UnityEditor;
using UnityEngine;

namespace BattleRoyale.Editor
{
    public sealed class LobbyIconAssetPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/_Game/Resources/UI/LobbyIcons/")
                && !assetPath.Contains("/_Game/Resources/UI/HudIcons/")
                && !assetPath.Contains("/_Game/Resources/UI/ScopeOverlays/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = assetPath.Contains("/_Game/Resources/UI/ScopeOverlays/") ? 1024 : 128;
        }

        public override uint GetVersion() => 1;
    }
}
