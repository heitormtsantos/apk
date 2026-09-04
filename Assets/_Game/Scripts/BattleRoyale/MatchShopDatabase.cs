using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(fileName = "MatchShopDatabase", menuName = "Battle Royale/Match Shop Database")]
    public sealed class MatchShopDatabase : ScriptableObject
    {
        public List<MatchShopItemData> items = new();

        public MatchShopItemData GetById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || items == null) return null;
            foreach (var item in items)
                if (item != null && string.Equals(item.id, itemId, System.StringComparison.Ordinal)) return item;
            return null;
        }

        public List<MatchShopItemData> GetByCategory(MatchShopCategory category)
        {
            var result = new List<MatchShopItemData>();
            if (items == null) return result;
            foreach (var item in items)
                if (item != null && item.available && item.category == category) result.Add(item);
            result.Sort((a, b) => a.sortOrder != b.sortOrder
                ? a.sortOrder.CompareTo(b.sortOrder)
                : string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal));
            return result;
        }

        public List<MatchShopItemData> GetAvailableItems()
        {
            var result = new List<MatchShopItemData>();
            if (items == null) return result;
            foreach (var item in items)
                if (item != null && item.IsValid) result.Add(item);
            result.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
            return result;
        }

        public bool Contains(MatchShopItemData item) => item != null && GetById(item.id) == item;
    }
}
