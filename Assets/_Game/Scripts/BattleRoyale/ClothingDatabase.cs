using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(menuName = "Raven Drop/Wardrobe/Clothing Database", fileName = "ClothingDatabase")]
    public sealed class ClothingDatabase : ScriptableObject
    {
        [SerializeField] private List<ClothingItemData> allItems = new();

        private readonly Dictionary<string, ClothingItemData> byId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ClothingSlot, List<ClothingItemData>> bySlot = new();
        private bool indexed;

        public IReadOnlyList<ClothingItemData> AllItems => allItems;

        private void OnEnable() => RebuildIndex();
        private void OnValidate() => indexed = false;

        public ClothingItemData GetById(string itemId)
        {
            EnsureIndex();
            return !string.IsNullOrWhiteSpace(itemId) && byId.TryGetValue(itemId, out var item) ? item : null;
        }

        public IReadOnlyList<ClothingItemData> GetBySlot(ClothingSlot slot)
        {
            EnsureIndex();
            return bySlot.TryGetValue(slot, out var items) ? items : Array.Empty<ClothingItemData>();
        }

        public ClothingItemData GetDefault(ClothingSlot slot)
        {
            var items = GetBySlot(slot);
            for (var i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].IsDefault) return items[i];
            return null;
        }

        public void RebuildIndex()
        {
            byId.Clear();
            bySlot.Clear();
            foreach (ClothingSlot slot in Enum.GetValues(typeof(ClothingSlot)))
                bySlot[slot] = new List<ClothingItemData>();
            for (var i = 0; i < allItems.Count; i++)
            {
                var item = allItems[i];
                if (item == null || string.IsNullOrWhiteSpace(item.Id)) continue;
                if (byId.ContainsKey(item.Id))
                {
                    Debug.LogWarning($"ClothingDatabase '{name}' ignored duplicate id '{item.Id}'.", this);
                    continue;
                }
                byId.Add(item.Id, item);
                bySlot[item.Slot].Add(item);
            }
            indexed = true;
        }

        private void EnsureIndex()
        {
            if (!indexed) RebuildIndex();
        }

#if UNITY_EDITOR
        public void ConfigureEditor(IEnumerable<ClothingItemData> items)
        {
            allItems.Clear();
            if (items != null) allItems.AddRange(items);
            RebuildIndex();
        }
#endif
    }
}
