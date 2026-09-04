using UnityEditor;

namespace BattleRoyale.Editor
{
    public sealed class QuaterniusAssetPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            var quaternius = assetPath.Contains("/_Game/Resources/Quaternius/");
            var kenney = assetPath.Contains("/_Game/Resources/KenneyBuildings/");
            if (!quaternius && !kenney) return;
            var importer = (ModelImporter)assetImporter;
            importer.importAnimation = quaternius && assetPath.Contains("Character_Soldier");
            importer.animationType = assetPath.Contains("Character_Soldier")
                ? ModelImporterAnimationType.Generic
                : ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = kenney;
        }

        public override uint GetVersion() => 3;
    }
}
