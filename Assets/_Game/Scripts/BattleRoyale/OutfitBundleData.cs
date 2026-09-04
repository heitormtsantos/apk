using System;
using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(menuName = "Raven Drop/Wardrobe/Outfit Bundle", fileName = "OutfitBundle")]
    public sealed class OutfitBundleData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private ClothingItemData[] items = Array.Empty<ClothingItemData>();

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public ClothingItemData[] Items => items ?? Array.Empty<ClothingItemData>();

#if UNITY_EDITOR
        public void ConfigureEditor(string stableId, string label, ClothingItemData[] bundleItems)
        {
            id = stableId;
            displayName = label;
            items = bundleItems ?? Array.Empty<ClothingItemData>();
        }
#endif
    }
}
