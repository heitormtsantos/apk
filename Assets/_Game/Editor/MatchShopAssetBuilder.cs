using UnityEditor;
using UnityEngine;

namespace BattleRoyale.Editor
{
    public static class MatchShopAssetBuilder
    {
        private const string Directory = "Assets/_Game/Resources/MatchShop";
        private const string EconomyPath = Directory + "/DefaultMatchEconomy.asset";
        private const string DatabasePath = Directory + "/DefaultMatchShopDatabase.asset";

        [MenuItem("Raven Drop/Match Shop/Rebuild Default Catalog")]
        public static void BuildDefaultCatalog()
        {
            EnsureDirectory();
            var economy = AssetDatabase.LoadAssetAtPath<MatchEconomyConfig>(EconomyPath);
            if (economy == null)
            {
                economy = ScriptableObject.CreateInstance<MatchEconomyConfig>();
                AssetDatabase.CreateAsset(economy, EconomyPath);
            }
            economy.startingCurrency = 600;
            economy.killReward = 200;
            economy.assistReward = 100;
            economy.objectiveReward = 150;
            economy.reviveReward = 100;
            economy.currencyPickupMin = 60;
            economy.currencyPickupMax = 180;
            economy.purchaseRequestCooldown = 0.12f;
            economy.Normalize();
            EditorUtility.SetDirty(economy);

            var database = AssetDatabase.LoadAssetAtPath<MatchShopDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<MatchShopDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }
            else
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(DatabasePath))
                    if (asset is MatchShopItemData) Object.DestroyImmediate(asset, true);
                database.items.Clear();
            }

            var order = 0;
            Weapon(database, "weapon-vesper-9", "Vesper-9", 1200, order++);
            Weapon(database, "weapon-kairo-smg", "Kairo SMG", 900, order++);
            Weapon(database, "weapon-bastion-12", "Bastion-12", 850, order++);
            Weapon(database, "weapon-longveil", "Longveil", 1800, order++);
            Weapon(database, "weapon-mako-sidearm", "Mako Sidearm", 650, order++);
            Ammo(database, "ammo-light", "Municao Leve x30", AmmoKind.Light, 120, 30, order++);
            Ammo(database, "ammo-medium", "Municao AR x30", AmmoKind.Medium, 150, 30, order++);
            Ammo(database, "ammo-shell", "Cartuchos x12", AmmoKind.Shell, 160, 12, order++);
            Ammo(database, "ammo-long", "Municao Sniper x10", AmmoKind.Long, 200, 10, order++);
            Product(database, "medkit", "Kit Medico", MatchShopCategory.Healing,
                MatchShopProductType.MedKit, 300, 1, order++);
            Equipment(database, "vest-1", "Colete Nivel 1", MatchShopProductType.Vest, 400, 1, order++);
            Equipment(database, "vest-2", "Colete Nivel 2", MatchShopProductType.Vest, 800, 2, order++);
            Equipment(database, "vest-3", "Colete Nivel 3", MatchShopProductType.Vest, 1400, 3, order++);
            Equipment(database, "helmet-1", "Capacete Nivel 1", MatchShopProductType.Helmet, 350, 1, order++);
            Equipment(database, "helmet-2", "Capacete Nivel 2", MatchShopProductType.Helmet, 700, 2, order++);
            Equipment(database, "helmet-3", "Capacete Nivel 3", MatchShopProductType.Helmet, 1200, 3, order++);
            Product(database, "gloo-wall", "Parede de Gel", MatchShopCategory.GlooWall,
                MatchShopProductType.GlooWall, 300, 1, order++);
            Product(database, "grenade", "Granada", MatchShopCategory.Throwables,
                MatchShopProductType.Grenade, 250, 1, order++);
            Product(database, "stabilizer", "Estabilizador", MatchShopCategory.Utility,
                MatchShopProductType.Stabilizer, 450, 1, order++);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Default match economy and shop catalog created in {Directory}");
        }

        private static void EnsureDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
                AssetDatabase.CreateFolder("Assets/_Game", "Resources");
            if (!AssetDatabase.IsValidFolder(Directory))
                AssetDatabase.CreateFolder("Assets/_Game/Resources", "MatchShop");
        }

        private static MatchShopItemData Product(MatchShopDatabase database, string id, string name,
            MatchShopCategory category, MatchShopProductType type, int price, int quantity, int order)
        {
            var item = ScriptableObject.CreateInstance<MatchShopItemData>();
            item.name = id;
            item.ConfigureRuntime(id, name, category, type, price, quantity);
            item.hideFlags = HideFlags.None;
            item.sortOrder = order;
            AssetDatabase.AddObjectToAsset(item, database);
            database.items.Add(item);
            return item;
        }

        private static void Weapon(MatchShopDatabase database, string id, string weaponName, int price, int order)
        {
            var item = Product(database, id, weaponName, MatchShopCategory.Weapons,
                MatchShopProductType.Weapon, price, 1, order);
            item.weaponId = weaponName;
            item.bonusAmmo = 30;
            item.requireConfirmation = price >= 1200;
        }

        private static void Ammo(MatchShopDatabase database, string id, string name, AmmoKind kind,
            int price, int quantity, int order)
        {
            var item = Product(database, id, name, MatchShopCategory.Ammo,
                MatchShopProductType.Ammo, price, quantity, order);
            item.ammoKind = kind;
        }

        private static void Equipment(MatchShopDatabase database, string id, string name,
            MatchShopProductType type, int price, int level, int order)
        {
            var item = Product(database, id, name, MatchShopCategory.Armor, type, price, 1, order);
            item.equipmentLevel = level;
            if (level < 3) return;
            item.stockMode = MatchShopStockMode.Global;
            item.maximumStock = 2;
        }
    }
}
