using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BattleRoyale.Tests
{
    public sealed class MatchShopSystemTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void WalletNeverBecomesNegativeAndRaisesChanges()
        {
            var wallet = CreateObject("Wallet").AddComponent<MatchCurrencyWallet>();
            var observed = -1;
            wallet.OnCurrencyChanged += value => observed = value;

            wallet.SetCurrency(100);
            Assert.That(wallet.TrySpend(101), Is.False);
            Assert.That(wallet.CurrentCurrency, Is.EqualTo(100));
            Assert.That(wallet.TrySpend(40), Is.True);
            Assert.That(wallet.CurrentCurrency, Is.EqualTo(60));
            wallet.SetCurrency(-20);

            Assert.That(wallet.CurrentCurrency, Is.Zero);
            Assert.That(observed, Is.Zero);
        }

        [Test]
        public void AmmoPurchaseIsAtomicAndUpdatesStock()
        {
            var fixture = CreateFixture(MatchShopProductType.Ammo, 150, 30);
            fixture.Item.ammoKind = AmmoKind.Light;
            fixture.Item.stockMode = MatchShopStockMode.Global;
            fixture.Item.maximumStock = 2;
            fixture.Terminal.ResetRuntimeStock();
            fixture.Player.Wallet.SetCurrency(500);

            Assert.That(fixture.Controller.RequestPurchase(fixture.Item, 10f), Is.EqualTo(PurchaseResult.Success));
            Assert.That(fixture.Player.Wallet.CurrentCurrency, Is.EqualTo(350));
            Assert.That(fixture.Player.Inventory.AmmoFor(AmmoKind.Light), Is.EqualTo(30));
            Assert.That(fixture.Terminal.GetRemainingStock(fixture.Item, fixture.Player), Is.EqualTo(1));
        }

        [Test]
        public void RejectedPurchaseNeverRemovesCurrencyOrChangesInventory()
        {
            var fixture = CreateFixture(MatchShopProductType.Ammo, 150, 30);
            fixture.Item.ammoKind = AmmoKind.Medium;
            fixture.Player.Wallet.SetCurrency(100);

            Assert.That(fixture.Controller.RequestPurchase(fixture.Item, 10f),
                Is.EqualTo(PurchaseResult.InsufficientCurrency));
            Assert.That(fixture.Player.Wallet.CurrentCurrency, Is.EqualTo(100));
            Assert.That(fixture.Player.Inventory.AmmoFor(AmmoKind.Medium), Is.Zero);

            fixture.Player.Wallet.SetCurrency(1000);
            fixture.Player.Inventory.AddAmmo(AmmoKind.Medium, InventorySystem.MaxAmmoPerKind);
            Assert.That(fixture.Controller.RequestPurchase(fixture.Item, 11f),
                Is.EqualTo(PurchaseResult.InventoryFull));
            Assert.That(fixture.Player.Wallet.CurrentCurrency, Is.EqualTo(1000));
            Assert.That(fixture.Player.Inventory.AmmoFor(AmmoKind.Medium),
                Is.EqualTo(InventorySystem.MaxAmmoPerKind));
        }

        [Test]
        public void PurchaseCooldownBlocksRepeatedRequestWithoutDoubleCharge()
        {
            var fixture = CreateFixture(MatchShopProductType.MedKit, 100, 1, 0.5f);
            fixture.Player.Wallet.SetCurrency(500);

            Assert.That(fixture.Controller.RequestPurchase(fixture.Item, 4f), Is.EqualTo(PurchaseResult.Success));
            Assert.That(fixture.Controller.RequestPurchase(fixture.Item, 4.1f), Is.EqualTo(PurchaseResult.Cooldown));
            Assert.That(fixture.Player.Wallet.CurrentCurrency, Is.EqualTo(400));
            Assert.That(fixture.Player.Inventory.MedKits, Is.EqualTo(1));
        }

        [Test]
        public void ExpensiveItemRequiresSecondConfirmationWithoutEarlyCharge()
        {
            var fixture = CreateFixture(MatchShopProductType.MedKit, 300, 1);
            fixture.Item.requireConfirmation = true;
            fixture.Player.Wallet.SetCurrency(600);

            Assert.That(fixture.Controller.RequestPurchase(fixture.Item, 2f),
                Is.EqualTo(PurchaseResult.ConfirmationRequired));
            Assert.That(fixture.Player.Wallet.CurrentCurrency, Is.EqualTo(600));
            Assert.That(fixture.Player.Inventory.MedKits, Is.Zero);
            Assert.That(fixture.Controller.RequestPurchase(fixture.Item, 3f), Is.EqualTo(PurchaseResult.Success));
            Assert.That(fixture.Player.Wallet.CurrentCurrency, Is.EqualTo(300));
            Assert.That(fixture.Player.Inventory.MedKits, Is.EqualTo(1));
        }

        [Test]
        public void ShoppingInputPolicyBlocksCombatWithoutPausingMovementByDefault()
        {
            var fixture = CreateFixture(MatchShopProductType.MedKit, 100, 1);
            var input = new BRInputFrame(Vector2.one, Vector2.one, true, true, true, true,
                true, true, true, true, true, true, 1, true, false, true, Vector2.one,
                AimInputDevice.Touch);

            var filtered = fixture.Controller.ApplyInputRestrictions(input);

            Assert.That(filtered.Move, Is.EqualTo(Vector2.one));
            Assert.That(filtered.Look, Is.EqualTo(Vector2.zero));
            Assert.That(filtered.Aim, Is.False);
            Assert.That(filtered.Fire, Is.False);
            Assert.That(filtered.FirePressed, Is.False);
            Assert.That(filtered.Interact, Is.True);
        }

        [Test]
        public void CurrencyPickupAndEliminationRewardUseTheSameMatchWallet()
        {
            var player = CreateParticipant();
            var pickup = CreateObject("Credits").AddComponent<LootPickup>();
            pickup.ConfigureCurrency(80);

            Assert.That(pickup.Collect(player), Is.True);
            Assert.That(player.Wallet.CurrentCurrency, Is.EqualTo(80));

            var economy = Track(ScriptableObject.CreateInstance<MatchEconomyConfig>());
            economy.killReward = 220;
            var rewards = CreateObject("Rewards").AddComponent<MatchRewardSystem>();
            rewards.Configure(economy);

            Assert.That(rewards.RewardElimination(player), Is.EqualTo(220));
            Assert.That(player.Wallet.CurrentCurrency, Is.EqualTo(300));
        }

        [Test]
        public void DatabaseFiltersUnavailableItemsAndSortsCategories()
        {
            var database = Track(ScriptableObject.CreateInstance<MatchShopDatabase>());
            var later = CreateItem("later", MatchShopProductType.MedKit, 10, 1);
            later.category = MatchShopCategory.Healing;
            later.sortOrder = 20;
            var first = CreateItem("first", MatchShopProductType.MedKit, 10, 1);
            first.category = MatchShopCategory.Healing;
            first.sortOrder = 2;
            var unavailable = CreateItem("hidden", MatchShopProductType.MedKit, 10, 1);
            unavailable.category = MatchShopCategory.Healing;
            unavailable.available = false;
            database.items.Add(later);
            database.items.Add(unavailable);
            database.items.Add(first);

            var result = database.GetByCategory(MatchShopCategory.Healing);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0], Is.SameAs(first));
            Assert.That(database.GetAvailableItems(), Has.Count.EqualTo(2));
        }

        private ShopFixture CreateFixture(MatchShopProductType type, int price, int quantity,
            float cooldown = 0f)
        {
            var managerObject = CreateObject("Match Manager");
            var manager = managerObject.AddComponent<MatchManager>();
            typeof(MatchManager).GetProperty(nameof(MatchManager.State),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(manager, MatchState.Active);

            var player = CreateParticipant();
            var database = Track(ScriptableObject.CreateInstance<MatchShopDatabase>());
            var item = CreateItem("test-item", type, price, quantity);
            database.items.Add(item);
            var terminal = CreateObject("Shop Terminal").AddComponent<MatchShopTerminal>();
            terminal.shopDatabase = database;
            terminal.interactionDistance = 4f;
            terminal.transform.position = player.transform.position;
            terminal.ResetRuntimeStock();

            var economy = Track(ScriptableObject.CreateInstance<MatchEconomyConfig>());
            economy.purchaseRequestCooldown = cooldown;
            var controller = managerObject.AddComponent<MatchShopController>();
            controller.Configure(manager, player, economy, null);
            Assert.That(controller.OpenShop(terminal), Is.True);
            return new ShopFixture(player, item, terminal, controller);
        }

        private BRParticipant CreateParticipant()
        {
            var participant = CreateObject("Player").AddComponent<BRParticipant>();
            participant.Configure("Player", true, 0, 0);
            participant.SetPhase(ParticipantPhase.Grounded);
            return participant;
        }

        private MatchShopItemData CreateItem(string id, MatchShopProductType type, int price, int quantity)
        {
            var item = Track(ScriptableObject.CreateInstance<MatchShopItemData>());
            item.ConfigureRuntime(id, id, MatchShopCategory.Utility, type, price, quantity);
            return item;
        }

        private GameObject CreateObject(string name)
        {
            var value = new GameObject(name);
            created.Add(value);
            return value;
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private readonly struct ShopFixture
        {
            public readonly BRParticipant Player;
            public readonly MatchShopItemData Item;
            public readonly MatchShopTerminal Terminal;
            public readonly MatchShopController Controller;

            public ShopFixture(BRParticipant player, MatchShopItemData item, MatchShopTerminal terminal,
                MatchShopController controller)
            {
                Player = player;
                Item = item;
                Terminal = terminal;
                Controller = controller;
            }
        }
    }
}
