using System;
using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(menuName = "Raven Drop/Wardrobe/Clothing Item", fileName = "ClothingItem")]
    public sealed class ClothingItemData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private ClothingSlot slot;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject clothingPrefab;
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material[] materials = Array.Empty<Material>();
        [SerializeField] private bool owned = true;
        [SerializeField] private ClothingRarity rarity;
        [SerializeField, Min(0)] private int price;
        [SerializeField] private BodyPart[] bodyPartsToHide = Array.Empty<BodyPart>();
        [SerializeField] private bool defaultItem;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public ClothingSlot Slot => slot;
        public Sprite Icon => icon;
        public GameObject ClothingPrefab => clothingPrefab;
        public Mesh Mesh => mesh;
        public Material[] Materials => materials ?? Array.Empty<Material>();
        public bool Owned => owned;
        public ClothingRarity Rarity => rarity;
        public int Price => Mathf.Max(0, price);
        public BodyPart[] BodyPartsToHide => bodyPartsToHide ?? Array.Empty<BodyPart>();
        public bool IsDefault => defaultItem;

        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                reason = "Item id is empty.";
                return false;
            }
            if (clothingPrefab == null)
            {
                reason = $"Item '{id}' has no clothing prefab.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(string stableId, string label, ClothingSlot clothingSlot,
            GameObject prefab, Material[] materialOverrides, BodyPart[] hiddenParts,
            bool isOwned = true, bool isDefaultItem = false, ClothingRarity itemRarity = ClothingRarity.Common,
            int itemPrice = 0, Sprite itemIcon = null, Mesh meshOverride = null)
        {
            id = stableId;
            displayName = label;
            slot = clothingSlot;
            clothingPrefab = prefab;
            materials = materialOverrides ?? Array.Empty<Material>();
            bodyPartsToHide = hiddenParts ?? Array.Empty<BodyPart>();
            owned = isOwned;
            defaultItem = isDefaultItem;
            rarity = itemRarity;
            price = Mathf.Max(0, itemPrice);
            icon = itemIcon;
            mesh = meshOverride;
        }
#endif
    }
}
