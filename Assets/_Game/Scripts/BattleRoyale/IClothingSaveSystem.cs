namespace BattleRoyale
{
    public interface IClothingSaveSystem
    {
        bool TryLoad(ClothingSlot slot, out string itemId);
        void Save(ClothingSlot slot, string itemId);
        void Delete(ClothingSlot slot);
    }
}
