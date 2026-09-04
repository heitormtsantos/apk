using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BattleRoyale.Tests
{
    public sealed class WardrobeSystemTests
    {
        private readonly List<UnityEngine.Object> cleanup = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null) UnityEngine.Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
        }

        [Test]
        public void StarterDatabaseContainsUniqueItemsForEverySlot()
        {
            var database = Resources.Load<ClothingDatabase>("Wardrobe/ClothingDatabase");
            Assert.That(database, Is.Not.Null);
            Assert.That(database.AllItems.Count, Is.EqualTo(15));
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in database.AllItems)
            {
                Assert.That(item, Is.Not.Null);
                Assert.That(ids.Add(item.Id), Is.True, $"Duplicate wardrobe id: {item.Id}");
                Assert.That(item.IsValid(out var reason), Is.True, reason);
                Assert.That(item.Icon, Is.Not.Null, $"Missing icon for wardrobe item: {item.Id}");
            }
            foreach (ClothingSlot slot in Enum.GetValues(typeof(ClothingSlot)))
                Assert.That(database.GetBySlot(slot).Count, Is.GreaterThan(0), slot.ToString());
        }

        [Test]
        public void EquippingASecondShirtReplacesOnlyTheShirtAndRemapsBones()
        {
            var manager = CreateManager(out var character);
            var database = manager.Database;
            var defaultShirt = database.GetById("shirt_default");
            var redShirt = database.GetById("shirt_red");
            var pants = database.GetById("pants_default");

            Assert.That(manager.Equip(defaultShirt), Is.True);
            Assert.That(manager.Equip(pants), Is.True);
            Assert.That(manager.Equip(redShirt), Is.True);
            Assert.That(manager.GetEquippedItem(ClothingSlot.Shirt), Is.SameAs(redShirt));
            Assert.That(manager.GetEquippedItem(ClothingSlot.Pants), Is.SameAs(pants));
            Assert.That(manager.EquippedItems.Count, Is.EqualTo(2));

            var clothes = character.transform.Find("Clothes");
            Assert.That(clothes, Is.Not.Null);
            Assert.That(clothes.childCount, Is.EqualTo(2));
            var shirtVisual = clothes.Find("Shirt - Camiseta Vermelha");
            Assert.That(shirtVisual, Is.Not.Null);
            var renderer = shirtVisual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(renderer, Is.Not.Null);
            foreach (var bone in renderer.bones)
            {
                Assert.That(bone, Is.Not.Null);
                Assert.That(bone.IsChildOf(shirtVisual), Is.False,
                    $"Bone '{bone.name}' still points to clothing prefab.");
            }
        }

        [Test]
        public void HiddenBodyPartsAreRecomputedFromAllEquippedItems()
        {
            var root = Track(new GameObject("Body"));
            var torso = CreateRenderer(root.transform, "Torso");
            var arms = CreateRenderer(root.transform, "Arms");
            var legs = CreateRenderer(root.transform, "Legs");
            var visibility = root.AddComponent<CharacterBodyVisibility>();
            visibility.Configure(new[]
            {
                new BodyPartRenderers(BodyPart.Torso, new Renderer[] { torso }),
                new BodyPartRenderers(BodyPart.Arms, new Renderer[] { arms }),
                new BodyPartRenderers(BodyPart.Legs, new Renderer[] { legs })
            });
            var shirt = CreateData("shirt", ClothingSlot.Shirt, BodyPart.Torso, BodyPart.Arms);
            var pants = CreateData("pants", ClothingSlot.Pants, BodyPart.Legs, BodyPart.Arms);

            visibility.RefreshHiddenBodyParts(new[] { shirt, pants });
            Assert.That(torso.enabled, Is.False);
            Assert.That(arms.enabled, Is.False);
            Assert.That(legs.enabled, Is.False);

            visibility.RefreshHiddenBodyParts(new[] { pants });
            Assert.That(torso.enabled, Is.True);
            Assert.That(arms.enabled, Is.False);
            Assert.That(legs.enabled, Is.False);
        }

        [Test]
        public void OutfitBundleEquipsOneItemPerSlot()
        {
            var manager = CreateManager(out _);
            var bundle = Resources.Load<OutfitBundleData>("Wardrobe/RavenMilitaryBundle");
            Assert.That(bundle, Is.Not.Null);
            Assert.That(manager.EquipBundle(bundle), Is.True);
            Assert.That(manager.EquippedItems.Count, Is.EqualTo(6));
            foreach (var item in bundle.Items) Assert.That(manager.IsEquipped(item), Is.True, item.Id);
        }

        [Test]
        public void PlayerPrefsSaveUsesStableSlotKeys()
        {
            var prefix = "Wardrobe.Tests." + Guid.NewGuid().ToString("N") + ".";
            var save = new PlayerPrefsClothingSaveSystem(prefix);
            save.Save(ClothingSlot.Shirt, "shirt_red");
            Assert.That(save.TryLoad(ClothingSlot.Shirt, out var id), Is.True);
            Assert.That(id, Is.EqualTo("shirt_red"));
            Assert.That(save.Key(ClothingSlot.Shirt), Is.EqualTo(prefix + "Shirt"));
            save.Delete(ClothingSlot.Shirt);
            Assert.That(save.TryLoad(ClothingSlot.Shirt, out _), Is.False);
        }

        [Test]
        public void PreviewRotationUsesOnlyHorizontalDrag()
        {
            Assert.That(CharacterPreviewRotator.RotationDelta(100f, 0.28f, false), Is.EqualTo(28f));
            Assert.That(CharacterPreviewRotator.RotationDelta(100f, 0.28f, true), Is.EqualTo(-28f));
        }

        private CharacterClothingManager CreateManager(out GameObject character)
        {
            var prefab = Resources.Load<GameObject>("UserCharacters/Steve/Model/Steve");
            Assert.That(prefab, Is.Not.Null);
            character = Track(UnityEngine.Object.Instantiate(prefab));
            var owner = Track(new GameObject("Wardrobe Owner"));
            var manager = owner.AddComponent<CharacterClothingManager>();
            manager.SetSaveSystem(new MemorySave());
            manager.ConfigureRuntime(character.transform, null,
                Resources.Load<ClothingDatabase>("Wardrobe/ClothingDatabase"));
            Assert.That(manager.Initialized, Is.True);
            return manager;
        }

        private ClothingItemData CreateData(string id, ClothingSlot slot, params BodyPart[] hidden)
        {
            var data = Track(ScriptableObject.CreateInstance<ClothingItemData>());
            var placeholder = Track(new GameObject(id + " Prefab"));
            placeholder.AddComponent<SkinnedMeshRenderer>();
            data.ConfigureEditor(id, id, slot, placeholder, Array.Empty<Material>(), hidden);
            return data;
        }

        private MeshRenderer CreateRenderer(Transform parent, string objectName)
        {
            var child = Track(new GameObject(objectName));
            child.transform.SetParent(parent, false);
            return child.AddComponent<MeshRenderer>();
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private sealed class MemorySave : IClothingSaveSystem
        {
            private readonly Dictionary<ClothingSlot, string> values = new();
            public bool TryLoad(ClothingSlot slot, out string itemId) => values.TryGetValue(slot, out itemId);
            public void Save(ClothingSlot slot, string itemId) => values[slot] = itemId;
            public void Delete(ClothingSlot slot) => values.Remove(slot);
        }
    }
}
