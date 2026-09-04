using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class MatchShopTerminal : MonoBehaviour
    {
        private static readonly List<MatchShopTerminal> ActiveTerminals = new();
        private readonly Dictionary<string, int> globalStock = new();
        private readonly Dictionary<string, int> playerPurchases = new();
        private float nextRestockTime;

        public string shopId = "shop-basic";
        public string displayName = "ESTACAO DE COMPRA";
        public MatchShopDatabase shopDatabase;
        public Transform interactionPoint;
        [Min(0.5f)] public float interactionDistance = 2.8f;
        public bool shopEnabled = true;
        public bool closeWhenPlayerMovesAway = true;
        [Min(0.5f)] public float closeDistance = 4f;
        public GameObject interactionPrompt;
        public bool usableOutsideSafeZone = true;
        public bool restockEnabled;
        [Min(1f)] public float restockInterval = 60f;
        public bool restockToMaximum = true;

        public Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : transform.position;
        public bool Available => shopEnabled && isActiveAndEnabled && shopDatabase != null;

        private void OnEnable()
        {
            if (!ActiveTerminals.Contains(this)) ActiveTerminals.Add(this);
            ResetRuntimeStock();
        }

        private void OnDisable() => ActiveTerminals.Remove(this);

        private void Update()
        {
            if (!restockEnabled || Time.time < nextRestockTime) return;
            ResetRuntimeStock();
        }

        public bool IsPlayerInRange(BRParticipant player, float extraDistance = 0f)
            => player != null && Vector3.Distance(player.transform.position, InteractionPosition)
                <= Mathf.Max(0.5f, interactionDistance + extraDistance);

        public int GetRemainingStock(MatchShopItemData item, BRParticipant player)
        {
            if (item == null || item.stockMode == MatchShopStockMode.Unlimited) return int.MaxValue;
            if (item.stockMode == MatchShopStockMode.Global)
                return globalStock.TryGetValue(item.id, out var stock) ? stock : Mathf.Max(0, item.maximumStock);
            var purchased = GetPlayerPurchases(item, player);
            return Mathf.Max(0, item.maximumStock - purchased);
        }

        public int GetPlayerPurchases(MatchShopItemData item, BRParticipant player)
        {
            if (item == null || player == null) return 0;
            return playerPurchases.TryGetValue(PlayerItemKey(item, player), out var count) ? count : 0;
        }

        public bool HasStock(MatchShopItemData item, BRParticipant player)
        {
            if (item == null) return false;
            if (!item.allowMultiplePurchases && GetPlayerPurchases(item, player) > 0) return false;
            if (item.maximumPurchasesPerPlayer > 0
                && GetPlayerPurchases(item, player) >= item.maximumPurchasesPerPlayer) return false;
            return GetRemainingStock(item, player) > 0;
        }

        public void CommitPurchase(MatchShopItemData item, BRParticipant player)
        {
            if (item == null || player == null) return;
            var key = PlayerItemKey(item, player);
            playerPurchases[key] = GetPlayerPurchases(item, player) + 1;
            if (item.stockMode == MatchShopStockMode.Global)
                globalStock[item.id] = Mathf.Max(0, GetRemainingStock(item, player) - 1);
        }

        public void ResetRuntimeStock()
        {
            globalStock.Clear();
            playerPurchases.Clear();
            if (shopDatabase != null && shopDatabase.items != null)
                foreach (var item in shopDatabase.items)
                    if (item != null && item.stockMode == MatchShopStockMode.Global)
                        globalStock[item.id] = Mathf.Max(0, item.maximumStock);
            nextRestockTime = Time.time + Mathf.Max(1f, restockInterval);
        }

        public static MatchShopTerminal FindNearest(Vector3 position, float radius)
        {
            MatchShopTerminal nearest = null;
            var best = radius * radius;
            for (var i = ActiveTerminals.Count - 1; i >= 0; i--)
            {
                var terminal = ActiveTerminals[i];
                if (terminal == null)
                {
                    ActiveTerminals.RemoveAt(i);
                    continue;
                }
                if (!terminal.Available) continue;
                var sqr = (terminal.InteractionPosition - position).sqrMagnitude;
                if (sqr > best) continue;
                best = sqr;
                nearest = terminal;
            }
            return nearest;
        }

        public static void ResetAllRuntimeStock()
        {
            foreach (var terminal in ActiveTerminals)
                if (terminal != null) terminal.ResetRuntimeStock();
        }

        private static string PlayerItemKey(MatchShopItemData item, BRParticipant player)
            => $"{player.GetEntityId()}:{item.id}";
    }
}
