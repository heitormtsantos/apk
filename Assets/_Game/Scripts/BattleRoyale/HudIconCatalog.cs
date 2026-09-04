using UnityEngine;

namespace BattleRoyale
{
    public enum HudIconId
    {
        Move,
        Fire,
        Aim,
        Jump,
        Interact,
        Reload,
        Heal,
        Swap,
        Pause
    }

    public static class HudIconCatalog
    {
        public const int Count = 9;
        private static readonly Sprite[] Cache = new Sprite[Count];

        public static string ResourceName(HudIconId id) => id.ToString().ToLowerInvariant();
        public static string ResourcePath(HudIconId id) => $"UI/HudIcons/{ResourceName(id)}";

        public static Sprite Load(HudIconId id)
        {
            var index = (int)id;
            if (index < 0 || index >= Cache.Length) return null;
            return Cache[index] != null ? Cache[index] : Cache[index] = Resources.Load<Sprite>(ResourcePath(id));
        }

        public static bool AllAvailable()
        {
            for (var i = 0; i < Count; i++)
                if (Load((HudIconId)i) == null) return false;
            return true;
        }
    }
}
