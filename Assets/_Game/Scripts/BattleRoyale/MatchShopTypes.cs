namespace BattleRoyale
{
    public enum MatchShopCategory
    {
        Weapons,
        Ammo,
        Healing,
        Armor,
        GlooWall,
        Throwables,
        Utility,
        Special,
        Services
    }

    public enum MatchShopProductType
    {
        Weapon,
        Ammo,
        MedKit,
        Vest,
        Helmet,
        GlooWall,
        Grenade,
        Stabilizer,
        Service
    }

    public enum MatchShopStockMode
    {
        Unlimited,
        PerPlayer,
        Global
    }

    public enum WeaponPurchaseBehavior
    {
        RejectIfNoSlot,
        ReplaceSelectedWeapon
    }

    public enum PurchaseResult
    {
        Success,
        InvalidItem,
        ShopUnavailable,
        TooFarAway,
        ItemUnavailable,
        OutOfStock,
        InsufficientCurrency,
        InventoryFull,
        InvalidQuantity,
        ConfirmationRequired,
        Cooldown,
        OutsideSafeZone,
        PlayerUnavailable,
        MatchUnavailable,
        Failed
    }
}
