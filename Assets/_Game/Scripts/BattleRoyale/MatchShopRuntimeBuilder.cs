using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public static class MatchShopRuntimeBuilder
    {
        public static MatchShopDatabase CreateDefaultCatalog(IReadOnlyList<WeaponDefinition> weapons)
        {
            var database = ScriptableObject.CreateInstance<MatchShopDatabase>();
            database.name = "Default Match Shop Catalog (Runtime)";
            database.hideFlags = HideFlags.DontSave;
            var order = 0;
            if (weapons != null)
                foreach (var weapon in weapons)
                {
                    if (weapon == null) continue;
                    var price = weapon.weaponClass switch
                    {
                        WeaponClass.Sniper => 1800,
                        WeaponClass.Rifle => 1200,
                        WeaponClass.Smg => 900,
                        WeaponClass.Shotgun => 850,
                        _ => 650
                    };
                    var item = Item($"weapon-{weapon.displayName.ToLowerInvariant().Replace(' ', '-')}",
                        weapon.displayName, MatchShopCategory.Weapons, MatchShopProductType.Weapon, price, 1, order++);
                    item.weapon = weapon;
                    item.weaponId = weapon.displayName;
                    item.bonusAmmo = weapon.magazineSize;
                    item.requireConfirmation = price >= 1200;
                    database.items.Add(item);
                }

            AddAmmo(database, "ammo-light", "Municao Leve x30", AmmoKind.Light, 120, order++);
            AddAmmo(database, "ammo-medium", "Municao AR x30", AmmoKind.Medium, 150, order++);
            AddAmmo(database, "ammo-shell", "Cartuchos x12", AmmoKind.Shell, 160, order++, 12);
            AddAmmo(database, "ammo-long", "Municao Sniper x10", AmmoKind.Long, 200, order++, 10);
            database.items.Add(Item("medkit", "Kit Medico", MatchShopCategory.Healing,
                MatchShopProductType.MedKit, 300, 1, order++));
            AddEquipment(database, "vest-1", "Colete Nivel 1", MatchShopProductType.Vest, 400, 1, order++);
            AddEquipment(database, "vest-2", "Colete Nivel 2", MatchShopProductType.Vest, 800, 2, order++);
            AddEquipment(database, "vest-3", "Colete Nivel 3", MatchShopProductType.Vest, 1400, 3, order++);
            AddEquipment(database, "helmet-1", "Capacete Nivel 1", MatchShopProductType.Helmet, 350, 1, order++);
            AddEquipment(database, "helmet-2", "Capacete Nivel 2", MatchShopProductType.Helmet, 700, 2, order++);
            AddEquipment(database, "helmet-3", "Capacete Nivel 3", MatchShopProductType.Helmet, 1200, 3, order++);
            database.items.Add(Item("gloo-wall", "Parede de Gel", MatchShopCategory.GlooWall,
                MatchShopProductType.GlooWall, 300, 1, order++));
            database.items.Add(Item("grenade", "Granada", MatchShopCategory.Throwables,
                MatchShopProductType.Grenade, 250, 1, order++));
            database.items.Add(Item("stabilizer", "Estabilizador", MatchShopCategory.Utility,
                MatchShopProductType.Stabilizer, 450, 1, order++));
            return database;
        }

        public static List<MatchShopTerminal> SpawnDefaultTerminals(MatchShopDatabase catalog)
        {
            var result = new List<MatchShopTerminal>();
            var bounds = PlayableArea.Bounds;
            var samples = new[]
            {
                new Vector2(0.50f, 0.50f), new Vector2(0.27f, 0.34f),
                new Vector2(0.72f, 0.31f), new Vector2(0.35f, 0.72f),
                new Vector2(0.70f, 0.70f), new Vector2(0.18f, 0.56f),
                new Vector2(0.83f, 0.52f), new Vector2(0.52f, 0.18f)
            };
            var count = WorldMapGeometry.RecommendedShopCount(bounds);
            for (var i = 0; i < count && i < samples.Length; i++)
            {
                var normalized = samples[i];
                var preferred = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, normalized.x),
                    bounds.max.y, Mathf.Lerp(bounds.min.z, bounds.max.z, normalized.y));
                if (!PlayableArea.TryGround(preferred, out var position, 0f)
                    && !PlayableArea.TryFindOpenGround(preferred, out position)) continue;
                var duplicate = false;
                foreach (var existing in result)
                    if ((existing.transform.position - position).sqrMagnitude < 100f) duplicate = true;
                if (duplicate) continue;
                result.Add(BuildTerminal(i, position, catalog));
            }
            return result;
        }

        private static MatchShopTerminal BuildTerminal(int index, Vector3 position, MatchShopDatabase catalog)
        {
            var root = new GameObject($"Match Shop Terminal {index + 1}");
            root.transform.position = position;
            var terminal = root.AddComponent<MatchShopTerminal>();
            terminal.shopId = $"buy-station-{index + 1:00}";
            terminal.shopDatabase = catalog;
            terminal.interactionDistance = 3f;
            terminal.closeDistance = 4.5f;
            terminal.usableOutsideSafeZone = index != 2;
            var baseMaterial = BRMaterialFactory.Create("Buy Station Graphite", new Color(0.055f, 0.075f, 0.09f));
            var screenMaterial = BRMaterialFactory.CreateEmissive("Buy Station Screen",
                new Color(0.02f, 0.82f, 0.90f), 2.8f);
            Part(root.transform, "Station Base", PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f),
                new Vector3(1.15f, 0.1f, 1.15f), baseMaterial);
            Part(root.transform, "Station Body", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f),
                new Vector3(1.1f, 1.4f, 0.72f), baseMaterial);
            Part(root.transform, "Purchase Screen", PrimitiveType.Cube, new Vector3(0f, 1.02f, -0.39f),
                new Vector3(0.82f, 0.48f, 0.035f), screenMaterial);
            var point = new GameObject("Interaction Point");
            point.transform.SetParent(root.transform, false);
            point.transform.localPosition = new Vector3(0f, 0f, -1.15f);
            terminal.interactionPoint = point.transform;
            terminal.ResetRuntimeStock();
            return terminal;
        }

        private static void Part(Transform parent, string name, PrimitiveType primitive, Vector3 position,
            Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static MatchShopItemData Item(string id, string name, MatchShopCategory category,
            MatchShopProductType type, int price, int quantity, int sortOrder)
        {
            var item = ScriptableObject.CreateInstance<MatchShopItemData>();
            item.ConfigureRuntime(id, name, category, type, price, quantity);
            item.sortOrder = sortOrder;
            return item;
        }

        private static void AddAmmo(MatchShopDatabase database, string id, string name, AmmoKind kind,
            int price, int order, int quantity = 30)
        {
            var item = Item(id, name, MatchShopCategory.Ammo, MatchShopProductType.Ammo, price, quantity, order);
            item.ammoKind = kind;
            database.items.Add(item);
        }

        private static void AddEquipment(MatchShopDatabase database, string id, string name,
            MatchShopProductType type, int price, int level, int order)
        {
            var item = Item(id, name, MatchShopCategory.Armor, type, price, 1, order);
            item.equipmentLevel = level;
            item.stockMode = level >= 3 ? MatchShopStockMode.Global : MatchShopStockMode.Unlimited;
            item.maximumStock = level >= 3 ? 2 : 0;
            database.items.Add(item);
        }
    }
}
