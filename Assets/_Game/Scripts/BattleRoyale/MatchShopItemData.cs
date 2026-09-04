using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(fileName = "MatchShopItem", menuName = "Battle Royale/Match Shop Item")]
    public sealed class MatchShopItemData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public MatchShopCategory category;
        public MatchShopProductType productType;
        [Min(0)] public int price;
        [Min(1)] public int quantityGranted = 1;
        public bool available = true;
        public int sortOrder;
        public MatchShopStockMode stockMode = MatchShopStockMode.Unlimited;
        [Min(0)] public int maximumStock;
        [Min(0)] public int maximumPurchasesPerPlayer;
        public bool allowMultiplePurchases = true;
        public bool requireConfirmation;
        public bool requireFullPackageSpace = true;
        public WeaponPurchaseBehavior weaponPurchaseBehavior = WeaponPurchaseBehavior.RejectIfNoSlot;
        public GameObject optionalWorldPrefab;

        [Header("Existing gameplay data")]
        public WeaponDefinition weapon;
        public string weaponId;
        public AmmoKind ammoKind;
        [Range(1, 3)] public int equipmentLevel = 1;
        [Min(0)] public int bonusAmmo;

        public bool IsValid => !string.IsNullOrWhiteSpace(id) && available && price >= 0 && quantityGranted > 0;

        public void ConfigureRuntime(string itemId, string itemName, MatchShopCategory itemCategory,
            MatchShopProductType type, int itemPrice, int quantity = 1)
        {
            id = itemId;
            displayName = itemName;
            category = itemCategory;
            productType = type;
            price = Mathf.Max(0, itemPrice);
            quantityGranted = Mathf.Max(1, quantity);
            available = true;
            hideFlags = HideFlags.DontSave;
        }
    }
}
