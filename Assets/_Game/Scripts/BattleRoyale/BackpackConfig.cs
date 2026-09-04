using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(menuName = "Raven Drop/Backpack Config", fileName = "DefaultBackpackConfig")]
    public sealed class BackpackConfig : ScriptableObject
    {
        [Min(1f)] public float baseCapacity = 50f;
        [Min(0.001f)] public float ammoUnitCost = 0.05f;
        [Min(0.001f)] public float medKitUnitCost = 4f;
        [Min(0.001f)] public float glooWallUnitCost = 4f;
        [Min(0.001f)] public float grenadeUnitCost = 3f;
        [Min(0f)] public float attachmentCost = 2f;
        public bool autoEquipHigherLevel = true;
        public bool dropPreviousBackpack;
        public bool dropBackpackOnDeath = true;
        public List<BackpackData> levels = new();

        public BackpackData FindLevel(int level)
        {
            foreach (var data in levels)
                if (data != null && data.level == level) return data;
            return null;
        }

        public BackpackData Roll(System.Random random, bool airdrop)
        {
            if (random == null) return null;
            if (airdrop)
            {
                var levelThree = FindLevel(3);
                if (levelThree != null && levelThree.canSpawnAsLoot) return levelThree;
            }
            var total = 0f;
            foreach (var data in levels)
                if (data != null && data.canSpawnAsLoot) total += Mathf.Max(0f, data.lootWeight);
            if (total <= 0f) return null;
            var roll = (float)random.NextDouble() * total;
            foreach (var data in levels)
            {
                if (data == null || !data.canSpawnAsLoot) continue;
                roll -= Mathf.Max(0f, data.lootWeight);
                if (roll <= 0f) return data;
            }
            return null;
        }

        public static BackpackConfig LoadOrRuntimeDefault()
        {
            var loaded = Resources.Load<BackpackConfig>("Backpacks/DefaultBackpackConfig");
            if (loaded != null) return loaded;
            var fallback = CreateInstance<BackpackConfig>();
            fallback.hideFlags = HideFlags.DontSave;
            return fallback;
        }
    }
}
