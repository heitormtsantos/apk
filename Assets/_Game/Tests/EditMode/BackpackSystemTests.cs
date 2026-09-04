using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BattleRoyale.Tests
{
    public sealed class BackpackSystemTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            DeathLootContainer.ClearAll();
            for (var i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void PlayerStartsWithoutBackpackAtBaseCapacity()
        {
            var player = CreatePlayer();
            Assert.That(player.Backpack.CurrentLevel, Is.Zero);
            Assert.That(player.Inventory.MaxCapacity, Is.EqualTo(50f));
        }

        [Test]
        public void PhysicalLevelsReplaceOnlyWithHigherLevel()
        {
            var player = CreatePlayer();
            var lv1 = Data(1);
            var lv2 = Data(2);
            var lv3 = Data(3);
            Assert.That(player.Backpack.TryEquipCollectedItem(lv1, out _), Is.EqualTo(BackpackEquipResult.Equipped));
            Assert.That(player.Backpack.TryEquipCollectedItem(lv2, out var replaced), Is.EqualTo(BackpackEquipResult.Equipped));
            Assert.That(replaced, Is.SameAs(lv1));
            Assert.That(player.Backpack.TryEquipCollectedItem(lv3, out _), Is.EqualTo(BackpackEquipResult.Equipped));
            Assert.That(player.Backpack.TryEquipCollectedItem(lv1, out _), Is.EqualTo(BackpackEquipResult.SameOrLowerLevel));
            Assert.That(player.Backpack.Current, Is.SameAs(lv3));
        }

        [Test]
        public void CapacityMatchesEveryPhysicalLevel()
        {
            var player = CreatePlayer();
            Assert.That(player.Inventory.MaxCapacity, Is.EqualTo(50f));
            player.Backpack.TryEquipCollectedItem(Data(1), out _);
            Assert.That(player.Inventory.MaxCapacity, Is.EqualTo(100f));
            player.Backpack.TryEquipCollectedItem(Data(2), out _);
            Assert.That(player.Inventory.MaxCapacity, Is.EqualTo(150f));
            player.Backpack.TryEquipCollectedItem(Data(3), out _);
            Assert.That(player.Inventory.MaxCapacity, Is.EqualTo(200f));
        }

        [Test]
        public void BackpackNeverChangesWithoutCollectedItem()
        {
            var player = CreatePlayer();
            player.Backpack.TryEquipCollectedItem(Data(1), out _);
            for (var i = 0; i < 100; i++) player.Inventory.NotifyEquipmentChanged();
            Assert.That(player.Backpack.CurrentLevel, Is.EqualTo(1));
        }

        [Test]
        public void LowerWorldPickupRemainsOnGround()
        {
            var player = CreatePlayer();
            player.Backpack.TryEquipCollectedItem(Data(3), out _);
            var pickupObject = Track(new GameObject("Backpack Pickup"));
            var pickup = pickupObject.AddComponent<LootPickup>();
            pickup.ConfigureBackpack(Data(1));
            var result = pickup.CollectQuantitative(player);
            Assert.That(result.Success, Is.False);
            Assert.That(pickup.Collected, Is.False);
            Assert.That(pickup.gameObject.activeSelf, Is.True);
            Assert.That(player.Backpack.CurrentLevel, Is.EqualTo(3));
        }

        [Test]
        public void HigherWorldPickupEquipsAndDisappears()
        {
            var player = CreatePlayer();
            player.Backpack.TryEquipCollectedItem(Data(1), out _);
            var pickupObject = Track(new GameObject("Backpack Pickup"));
            var pickup = pickupObject.AddComponent<LootPickup>();
            pickup.ConfigureBackpack(Data(2));
            var result = pickup.CollectQuantitative(player);
            Assert.That(result.Success, Is.True);
            Assert.That(pickup.Collected, Is.True);
            Assert.That(pickup.gameObject.activeSelf, Is.False);
            Assert.That(player.Backpack.CurrentLevel, Is.EqualTo(2));
        }

        [Test]
        public void BackpackCannotBeDroppedWhileInventoryExceedsBaseCapacity()
        {
            var player = CreatePlayer();
            player.Backpack.TryEquipCollectedItem(Data(1), out _);
            player.Inventory.AddAmmoQuantitative(AmmoKind.Light, 240);
            player.Inventory.AddAmmoQuantitative(AmmoKind.Medium, 240);
            player.Inventory.AddAmmoQuantitative(AmmoKind.Shell, 240);
            player.Inventory.AddMedKitsQuantitative(5);
            Assert.That(player.Inventory.CurrentUsage, Is.GreaterThan(player.Inventory.BaseCapacity));
            Assert.That(player.Backpack.RemoveForDrop(), Is.Null);
            Assert.That(player.Backpack.CurrentLevel, Is.EqualTo(1));
        }

        [Test]
        public void DeathBoxContainsPhysicalBackpackAndRemovesItFromVictim()
        {
            var victim = CreatePlayer();
            victim.Backpack.TryEquipCollectedItem(Data(2), out _);
            var box = DeathLootContainer.Create(victim);
            Assert.That(victim.Backpack.CurrentLevel, Is.Zero);
            Assert.That(box.Entries, Has.Some.Matches<DeathLootEntry>(entry =>
                entry.Kind == DeathLootKind.Backpack && entry.Backpack.level == 2));
        }

        [Test]
        public void TakingBackpackFromDeathBoxIsARealCollection()
        {
            var victim = CreatePlayer("Victim", 0);
            var receiver = CreatePlayer("Receiver", 1);
            victim.Backpack.TryEquipCollectedItem(Data(2), out _);
            var box = DeathLootContainer.Create(victim);
            var entry = FindBackpack(box);
            var result = box.TakeById(entry.EntryId, receiver);
            Assert.That(result.Success, Is.True);
            Assert.That(receiver.Backpack.CurrentLevel, Is.EqualTo(2));
        }

        [Test]
        public void AirdropContainsLevelThreeBackpack()
        {
            var box = DeathLootContainer.CreateAirdrop(Vector3.zero, new List<WeaponDefinition>(), 77);
            Assert.That(box.Entries, Has.Some.Matches<DeathLootEntry>(entry =>
                entry.Kind == DeathLootKind.Backpack && entry.Backpack.level == 3));
        }

        private BRParticipant CreatePlayer(string name = "Player", int seat = 0)
        {
            var root = Track(new GameObject(name));
            var player = root.AddComponent<BRParticipant>();
            player.Configure(name, true, seat);
            player.SetPhase(ParticipantPhase.Grounded);
            return player;
        }

        private static BackpackData Data(int level)
        {
            var data = Resources.Load<BackpackData>($"Backpacks/Data/Backpack_Lv{level}");
            Assert.That(data, Is.Not.Null, $"Backpack Lv{level} data was not generated");
            return data;
        }

        private static DeathLootEntry FindBackpack(DeathLootContainer box)
        {
            foreach (var entry in box.Entries)
                if (entry.Kind == DeathLootKind.Backpack) return entry;
            Assert.Fail("Backpack entry not found");
            return null;
        }

        private GameObject Track(GameObject value)
        {
            created.Add(value);
            return value;
        }
    }
}
