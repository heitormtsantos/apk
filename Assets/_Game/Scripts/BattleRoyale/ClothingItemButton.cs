using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleRoyale
{
    public sealed class ClothingItemButton : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text itemName;
        [SerializeField] private Button button;
        [SerializeField] private GameObject equippedIndicator;
        [SerializeField] private GameObject lockedIndicator;

        private ClothingItemData data;
        private CharacterClothingManager manager;

        public ClothingItemData Data => data;

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
        }

        public void Setup(ClothingItemData item, CharacterClothingManager clothingManager)
        {
            data = item;
            manager = clothingManager;
            if (icon != null)
            {
                icon.sprite = item != null ? item.Icon : null;
                icon.enabled = icon.sprite != null;
            }
            if (itemName != null) itemName.text = item != null ? item.DisplayName : string.Empty;
            if (button != null)
            {
                button.onClick.RemoveListener(EquipCurrent);
                button.onClick.AddListener(EquipCurrent);
                button.interactable = item != null && item.Owned;
            }
            if (lockedIndicator != null) lockedIndicator.SetActive(item != null && !item.Owned);
            RefreshState();
        }

        public void RefreshState()
        {
            if (equippedIndicator != null)
                equippedIndicator.SetActive(data != null && manager != null && manager.IsEquipped(data));
        }

        private void EquipCurrent()
        {
            if (data != null && manager != null) manager.Equip(data);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(EquipCurrent);
        }
    }
}
