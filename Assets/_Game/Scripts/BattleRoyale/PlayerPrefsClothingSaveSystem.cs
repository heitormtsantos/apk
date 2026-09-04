using UnityEngine;

namespace BattleRoyale
{
    public sealed class PlayerPrefsClothingSaveSystem : IClothingSaveSystem
    {
        private readonly string keyPrefix;

        public PlayerPrefsClothingSaveSystem(string prefix = "Wardrobe.Equipped.") =>
            keyPrefix = string.IsNullOrWhiteSpace(prefix) ? "Wardrobe.Equipped." : prefix;

        public bool TryLoad(ClothingSlot slot, out string itemId)
        {
            var key = Key(slot);
            itemId = PlayerPrefs.GetString(key, string.Empty);
            return PlayerPrefs.HasKey(key) && !string.IsNullOrWhiteSpace(itemId);
        }

        public void Save(ClothingSlot slot, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Delete(slot);
                return;
            }
            PlayerPrefs.SetString(Key(slot), itemId);
            PlayerPrefs.Save();
        }

        public void Delete(ClothingSlot slot)
        {
            PlayerPrefs.DeleteKey(Key(slot));
            PlayerPrefs.Save();
        }

        public string Key(ClothingSlot slot) => keyPrefix + slot;
    }
}
