using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleRoyale
{
    [Serializable]
    public sealed class ClothingCategoryButton
    {
        [SerializeField] private ClothingSlot slot;
        [SerializeField] private Button button;
        [SerializeField] private GameObject selectedIndicator;

        public ClothingSlot Slot => slot;
        public Button Button => button;
        public GameObject SelectedIndicator => selectedIndicator;
    }

    public sealed class WardrobeUI : MonoBehaviour
    {
        [SerializeField] private CharacterClothingManager manager;
        [SerializeField] private ClothingDatabase database;
        [SerializeField] private ClothingItemButton itemButtonPrefab;
        [SerializeField] private Transform itemsContent;
        [SerializeField] private TMP_Text categoryTitle;
        [SerializeField] private ClothingCategoryButton[] categoryButtons = Array.Empty<ClothingCategoryButton>();
        [SerializeField] private ClothingSlot initialCategory = ClothingSlot.Shirt;

        private readonly List<ClothingItemButton> itemButtons = new();
        private ClothingSlot currentCategory;
        private bool subscribed;

        public ClothingSlot CurrentCategory => currentCategory;
        public int VisibleItemCount { get; private set; }

        private void Start()
        {
            Bind(manager, database);
            ShowCategory(initialCategory);
        }

        public void Bind(CharacterClothingManager clothingManager, ClothingDatabase clothingDatabase = null)
        {
            Unsubscribe();
            manager = clothingManager;
            database = clothingDatabase != null ? clothingDatabase : manager != null ? manager.Database : null;
            Subscribe();
            BindCategoryButtons();
        }

        public void ShowCategory(ClothingSlot slot)
        {
            currentCategory = slot;
            if (categoryTitle != null) categoryTitle.text = CategoryLabel(slot);
            for (var i = 0; i < categoryButtons.Length; i++)
            {
                var category = categoryButtons[i];
                if (category?.SelectedIndicator != null) category.SelectedIndicator.SetActive(category.Slot == slot);
            }
            RefreshItems();
        }

        public void RefreshItems()
        {
            SetAllButtonsInactive();
            VisibleItemCount = 0;
            if (database == null || manager == null || itemButtonPrefab == null || itemsContent == null) return;
            var items = database.GetBySlot(currentCategory);
            for (var i = 0; i < items.Count; i++)
            {
                var button = AcquireButton(VisibleItemCount++);
                button.gameObject.SetActive(true);
                button.Setup(items[i], manager);
            }
        }

        public static string CategoryLabel(ClothingSlot slot) => slot switch
        {
            ClothingSlot.Hair => "CABELO",
            ClothingSlot.Hat => "CHAPEU",
            ClothingSlot.Face => "ROSTO",
            ClothingSlot.Shirt => "PARTE DE CIMA",
            ClothingSlot.Pants => "CALCA",
            ClothingSlot.Shoes => "CALCADOS",
            _ => slot.ToString().ToUpperInvariant()
        };

        private ClothingItemButton AcquireButton(int index)
        {
            if (index < itemButtons.Count) return itemButtons[index];
            var created = Instantiate(itemButtonPrefab, itemsContent);
            created.name = $"Wardrobe Item {itemButtons.Count + 1}";
            itemButtons.Add(created);
            return created;
        }

        private void SetAllButtonsInactive()
        {
            for (var i = 0; i < itemButtons.Count; i++)
                if (itemButtons[i] != null) itemButtons[i].gameObject.SetActive(false);
        }

        private void BindCategoryButtons()
        {
            for (var i = 0; i < categoryButtons.Length; i++)
            {
                var category = categoryButtons[i];
                if (category?.Button == null) continue;
                var slot = category.Slot;
                category.Button.onClick.RemoveAllListeners();
                category.Button.onClick.AddListener(() => ShowCategory(slot));
            }
        }

        private void Subscribe()
        {
            if (manager == null || subscribed) return;
            manager.OnItemEquipped += HandleAppearanceChanged;
            manager.OnItemUnequipped += HandleItemUnequipped;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (manager != null && subscribed)
            {
                manager.OnItemEquipped -= HandleAppearanceChanged;
                manager.OnItemUnequipped -= HandleItemUnequipped;
            }
            subscribed = false;
        }

        private void HandleAppearanceChanged(ClothingItemData _) => RefreshVisibleStates();
        private void HandleItemUnequipped(ClothingSlot _) => RefreshVisibleStates();

        private void RefreshVisibleStates()
        {
            for (var i = 0; i < itemButtons.Count; i++)
                if (itemButtons[i] != null && itemButtons[i].gameObject.activeSelf) itemButtons[i].RefreshState();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
    }
}
