using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public interface ITimeProvider { long UnixNow { get; } }
    public sealed class SystemTimeProvider : ITimeProvider
    {
        public long UnixNow => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    [Serializable]
    public sealed class SeasonRankReward
    {
        public RankedTier minimumTier;
        public List<RewardDefinition> rewards = new();
    }

    [CreateAssetMenu(menuName = "Battle Royale/Account/Season", fileName = "AccountSeason")]
    public sealed class AccountSeasonData : ScriptableObject
    {
        [SerializeField] private string seasonId = "season_2026_01";
        [SerializeField] private long startsAtUnix;
        [SerializeField] private long endsAtUnix = 1893456000;
        [SerializeField, Range(0f, 1f)] private float rankedResetFactor = 0.65f;
        [SerializeField] private List<SeasonRankReward> rankedRewards = new();
        public string SeasonId => string.IsNullOrWhiteSpace(seasonId) ? "season_local" : seasonId;
        public long StartsAtUnix => startsAtUnix;
        public long EndsAtUnix => endsAtUnix;
        public float RankedResetFactor => Mathf.Clamp01(rankedResetFactor);
        public IReadOnlyList<SeasonRankReward> RankedRewards => rankedRewards;
        public bool IsActive(long now) => now >= startsAtUnix && (endsAtUnix <= 0L || now < endsAtUnix);
        public static AccountSeasonData CreateRuntimeDefault()
        {
            var value = CreateInstance<AccountSeasonData>();
            value.hideFlags = HideFlags.DontSave;
            return value;
        }
    }

    public sealed class SeasonService
    {
        private readonly PlayerProfileService profile;
        private readonly ITimeProvider time;
        internal SeasonService(PlayerProfileService owner, ITimeProvider provider)
        {
            profile = owner;
            time = provider ?? new SystemTimeProvider();
        }
        public bool IsActive => profile.SeasonConfig != null && profile.SeasonConfig.IsActive(time.UnixNow);

        public AccountCommandResult Synchronize(string operationId)
        {
            var season = profile.SeasonConfig;
            if (season == null) return AccountCommandResult.Fail("season_missing", "Season config not found.");
            return profile.Transact(operationId, data =>
            {
                if (data.season.activeSeasonId == season.SeasonId) return AccountCommandResult.Ok("unchanged");
                if (string.IsNullOrWhiteSpace(data.season.activeSeasonId))
                {
                    data.season.activeSeasonId = season.SeasonId;
                    foreach (var ranked in data.rankedModes)
                        if (ranked != null) ranked.seasonId = season.SeasonId;
                    if (profile.BattlePassSeason != null)
                        BattlePassService.EnsureSeason(data, profile.BattlePassSeason);
                    return AccountCommandResult.Ok("initialized");
                }
                foreach (var ranked in data.rankedModes)
                {
                    if (ranked == null) continue;
                    GrantSeasonReward(data, ranked, operationId, season);
                    ranked.rating = Mathf.Max(0, Mathf.FloorToInt(ranked.rating * season.RankedResetFactor));
                    ranked.highestRating = ranked.rating;
                    ranked.matches = 0;
                    ranked.victories = 0;
                    ranked.kills = 0;
                    ranked.damage = 0;
                    ranked.revives = 0;
                    ranked.bestPlacement = 0;
                    ranked.seasonId = season.SeasonId;
                }
                data.season.activeSeasonId = season.SeasonId;
                if (profile.BattlePassSeason != null)
                    BattlePassService.EnsureSeason(data, profile.BattlePassSeason);
                data.missions.Clear();
                return AccountCommandResult.Ok();
            }, true);
        }

        private void GrantSeasonReward(PlayerProgressionData data, RankedModeProgressData ranked,
            string operationId, AccountSeasonData season)
        {
            var tier = RankedProgression.TierForRating(ranked.highestRating);
            SeasonRankReward selected = null;
            foreach (var reward in season.RankedRewards)
                if (reward != null && (int)tier >= (int)reward.minimumTier
                    && (selected == null || (int)reward.minimumTier > (int)selected.minimumTier)) selected = reward;
            if (selected == null) return;
            var claimId = $"{ranked.mode}_{ranked.seasonId}_{selected.minimumTier}";
            if (data.season.claimedSeasonRewardIds.Contains(claimId)) return;
            var result = RewardService.ApplyRewards(data, selected.rewards,
                operationId + ":" + claimId, CurrencyTransactionReason.RankedReward, claimId, true,
                profile.BattlePassSeason, profile.LevelConfig);
            if (result.Success) data.season.claimedSeasonRewardIds.Add(claimId);
        }
    }

    [CreateAssetMenu(menuName = "Battle Royale/Account/Premium Currency Product", fileName = "PremiumCurrencyProduct")]
    public sealed class PremiumCurrencyProductData : ScriptableObject
    {
        [SerializeField] private string productId;
        [SerializeField, Min(1)] private long diamondAmount = 100;
        public string ProductId => productId;
        public long DiamondAmount => Math.Max(1L, diamondAmount);
    }

    public readonly struct PurchaseProviderResult
    {
        public PurchaseProviderResult(bool success, string transactionId, string productId,
            string receipt, string error)
        {
            Success = success;
            TransactionId = transactionId ?? string.Empty;
            ProductId = productId ?? string.Empty;
            Receipt = receipt ?? string.Empty;
            Error = error ?? string.Empty;
        }
        public bool Success { get; }
        public string TransactionId { get; }
        public string ProductId { get; }
        public string Receipt { get; }
        public string Error { get; }
    }

    public interface IPurchaseProvider
    {
        bool Available { get; }
        void Purchase(string productId, Action<PurchaseProviderResult> completed);
        void Restore(Action<bool> completed);
    }

    public interface IPurchaseReceiptValidator
    {
        bool Validate(PurchaseProviderResult purchase, out string authorizationId);
    }

    public sealed class PurchaseService
    {
        private readonly PlayerProfileService profile;
        private readonly IPurchaseProvider provider;
        private readonly IPurchaseReceiptValidator validator;
        private bool purchasePending;
        public AccountPurchaseState State { get; private set; } = AccountPurchaseState.Idle;
        public event Action<AccountPurchaseResult> PurchaseCompleted;

        public PurchaseService(PlayerProfileService owner, IPurchaseProvider purchaseProvider,
            IPurchaseReceiptValidator receiptValidator)
        {
            profile = owner;
            provider = purchaseProvider;
            validator = receiptValidator;
        }

        public AccountPurchaseResult RequestPurchaseCurrency(PremiumCurrencyProductData product)
        {
            if (purchasePending) return AccountPurchaseResult.Pending;
            if (product == null || string.IsNullOrWhiteSpace(product.ProductId)) return AccountPurchaseResult.InvalidProduct;
            if (provider == null || !provider.Available) return AccountPurchaseResult.NetworkError;
            purchasePending = true;
            State = AccountPurchaseState.Purchasing;
            provider.Purchase(product.ProductId, result => Complete(product, result));
            return AccountPurchaseResult.Pending;
        }

        private void Complete(PremiumCurrencyProductData product, PurchaseProviderResult result)
        {
            purchasePending = false;
            if (!result.Success || validator == null || !validator.Validate(result, out var authorizationId))
            {
                State = AccountPurchaseState.Failed;
                PurchaseCompleted?.Invoke(AccountPurchaseResult.ServerRejected);
                return;
            }
            var grant = profile.Wallet.GrantPremiumAuthorized("iap:" + authorizationId,
                product.DiamondAmount, CurrencyTransactionReason.IAPPurchase,
                authorizationId, product.ProductId);
            State = grant.Success ? AccountPurchaseState.Success : AccountPurchaseState.Failed;
            PurchaseCompleted?.Invoke(grant.Success ? AccountPurchaseResult.Success
                : grant.Code == "duplicate" ? AccountPurchaseResult.AlreadyOwned : AccountPurchaseResult.Failed);
        }
    }

    public sealed class BackendProfileRepository : IProfileRepository
    {
        public ProfileLoadResult Load() => new(false, new PlayerProgressionData(), false,
            "Backend repository is not configured.");
        public bool Save(PlayerProgressionData profile, out string error)
        {
            error = "Backend repository is not configured.";
            return false;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed class MockPurchaseProvider : IPurchaseProvider, IPurchaseReceiptValidator
    {
        public bool Available => true;
        public void Purchase(string productId, Action<PurchaseProviderResult> completed)
        {
            var id = Guid.NewGuid().ToString("N");
            completed?.Invoke(new PurchaseProviderResult(true, id, productId, "mock:" + id, string.Empty));
        }
        public void Restore(Action<bool> completed) => completed?.Invoke(true);
        public bool Validate(PurchaseProviderResult purchase, out string authorizationId)
        {
            authorizationId = purchase.TransactionId;
            return purchase.Success && purchase.Receipt.StartsWith("mock:", StringComparison.Ordinal);
        }
    }
#endif
}
