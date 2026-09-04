using UnityEngine;

namespace BattleRoyale
{
    public enum BackpackRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic
    }

    [CreateAssetMenu(menuName = "Raven Drop/Backpack Data", fileName = "Backpack_Lv1")]
    public sealed class BackpackData : ScriptableObject
    {
        public string id = "backpack_lv1";
        public string displayName = "Mochila Lv.1";
        [Range(1, 3)] public int level = 1;
        public Sprite icon;
        public GameObject worldLootPrefab;
        public GameObject equippedVisualPrefab;
        [Min(0f)] public float additionalCapacity = 50f;
        public BackpackRarity rarity = BackpackRarity.Common;
        [Min(0f)] public float lootWeight = 60f;
        public bool canDrop = true;
        public bool canSpawnAsLoot = true;
        public AudioClip pickupSound;
    }
}
