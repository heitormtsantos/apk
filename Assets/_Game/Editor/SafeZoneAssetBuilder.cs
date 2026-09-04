using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BattleRoyale.Editor
{
    public static class SafeZoneAssetBuilder
    {
        private const string Directory = "Assets/_Game/Resources/SafeZone";
        private const string AssetPath = Directory + "/DefaultSafeZoneConfig.asset";

        [MenuItem("Raven Drop/Safe Zone/Rebuild Default Config")]
        public static void BuildDefaultConfig()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources/SafeZone"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
                    AssetDatabase.CreateFolder("Assets/_Game", "Resources");
                AssetDatabase.CreateFolder("Assets/_Game/Resources", "SafeZone");
            }

            var config = AssetDatabase.LoadAssetAtPath<SafeZoneConfig>(AssetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SafeZoneConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
            }

            config.initialRadius = 86f;
            config.damageTickInterval = 0.5f;
            config.boundaryTolerance = 0.1f;
            config.minimumFinalRadius = 2f;
            config.wallHeight = 260f;
            config.wallBottomOffset = -40f;
            config.wallSegments = 64;
            config.phases = new List<SafeZonePhaseData>
            {
                new("Phase 1", 24f, 16f, 76f, 1.5f) { warningTime = 8f },
                new("Phase 2", 18f, 16f, 52f, 3f) { warningTime = 7f },
                new("Phase 3", 14f, 14f, 32f, 5f) { warningTime = 6f },
                new("Phase 4", 10f, 12f, 17f, 8f) { warningTime = 5f },
                new("Final", 8f, 10f, 6f, 12f) { warningTime = 4f, maxCenterOffsetMultiplier = 0.5f }
            };
            config.Normalize();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Default SafeZoneConfig created at {AssetPath}");
        }
    }
}
