using UnityEngine;

namespace BattleRoyale
{
    public enum LobbyIconId
    {
        Store,
        Missions,
        Events,
        Coin,
        Diamond,
        Arsenal,
        Operator,
        Hud,
        Settings,
        Invite,
        Map,
        Luck,
        Pass,
        Collection,
        Close
    }

    public static class LobbyIconCatalog
    {
        public const int Count = 15;
        private static readonly Sprite[] Cache = new Sprite[Count];

        public static string ResourceName(LobbyIconId id) => id switch
        {
            LobbyIconId.Store => "store",
            LobbyIconId.Missions => "missions",
            LobbyIconId.Events => "events",
            LobbyIconId.Coin => "coin",
            LobbyIconId.Diamond => "diamond",
            LobbyIconId.Arsenal => "arsenal",
            LobbyIconId.Operator => "operator",
            LobbyIconId.Hud => "hud",
            LobbyIconId.Settings => "settings",
            LobbyIconId.Invite => "invite",
            LobbyIconId.Map => "map",
            LobbyIconId.Luck => "luck",
            LobbyIconId.Pass => "pass",
            LobbyIconId.Collection => "collection",
            LobbyIconId.Close => "close",
            _ => string.Empty
        };

        public static string ResourcePath(LobbyIconId id) => $"UI/LobbyIcons/{ResourceName(id)}";

        public static Sprite Load(LobbyIconId id)
        {
            var index = (int)id;
            if (index < 0 || index >= Cache.Length) return null;
            return Cache[index] != null ? Cache[index] : Cache[index] = Resources.Load<Sprite>(ResourcePath(id));
        }

        public static bool AllAvailable()
        {
            for (var i = 0; i < Count; i++)
                if (Load((LobbyIconId)i) == null) return false;
            return true;
        }
    }
}
