using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public enum CurrencyType { SoftCurrency, PremiumCurrency }
    public enum CurrencyTransactionDirection { Credit, Debit }
    public enum CurrencyTransactionReason
    {
        MatchReward, AccountLevelReward, BattlePassReward, MissionReward, RankedReward,
        EventReward, ShopPurchase, BattlePassPurchase, IAPPurchase, AdminGrant, Refund, Other
    }
    public enum AccountXPSource { Match, Mission, Event, Achievement, Tutorial }
    public enum ProfileLoadState { Loading, Loaded, Failed }
    public enum RewardType { SoftCurrency, PremiumCurrency, AccountXP, AccountItem, BattlePassXP }
    public enum BattlePassTrack { Free, Premium }
    public enum MissionCadence { Daily, Weekly, Season }
    public enum MissionObjective
    {
        PlayMatches, WinMatches, ReachTop10, Eliminations, Assists, Damage, Revives, SurvivalSeconds
    }
    public enum AccountPurchaseState { Idle, Purchasing, Success, Failed }
    public enum AccountPurchaseResult
    {
        Success, InsufficientCurrency, AlreadyOwned, InvalidProduct, ProductUnavailable,
        SeasonEnded, InventoryFull, NetworkError, ServerRejected, Pending, Failed
    }

    [Serializable]
    public sealed class CurrencyTransaction
    {
        public string transactionId;
        public CurrencyType currencyType;
        public long amount;
        public CurrencyTransactionDirection direction;
        public CurrencyTransactionReason reason;
        public long unixTimestamp;
        public string sourceId;
        public string productId;
    }

    [Serializable]
    public sealed class WalletData
    {
        public long softCurrency;
        public long premiumCurrency;
        public long version;
    }

    public readonly struct WalletSnapshot
    {
        public WalletSnapshot(long softCurrency, long premiumCurrency, long version)
        {
            SoftCurrency = Math.Max(0L, softCurrency);
            PremiumCurrency = Math.Max(0L, premiumCurrency);
            Version = Math.Max(0L, version);
        }
        public long SoftCurrency { get; }
        public long PremiumCurrency { get; }
        public long Version { get; }
    }

    [Serializable]
    public sealed class AccountProgressData
    {
        public int level = 1;
        public long totalXP;
        public List<string> claimedLevelRewards = new();
    }

    [Serializable]
    public sealed class OwnedAccountItem
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public sealed class RewardInventoryData
    {
        public List<OwnedAccountItem> items = new();
    }

    [Serializable]
    public sealed class BattlePassProgressData
    {
        public string seasonId;
        public int tier = 1;
        public long xpIntoTier;
        public bool premiumUnlocked;
        public List<string> claimedRewardIds = new();
    }

    [Serializable]
    public sealed class MissionProgressData
    {
        public string missionId;
        public long progress;
        public bool completed;
        public bool rewardClaimed;
        public long resetWindow;
    }

    [Serializable]
    public sealed class RankedModeProgressData
    {
        public BRMatchMode mode;
        public string seasonId;
        public int rating;
        public int highestRating;
        public int matches;
        public int victories;
        public int kills;
        public int damage;
        public int revives;
        public int bestPlacement;
    }

    [Serializable]
    public sealed class SeasonProgressData
    {
        public string activeSeasonId;
        public List<string> claimedSeasonRewardIds = new();
    }

    [Serializable]
    public sealed class PlayerProgressionData
    {
        public const int CurrentSaveVersion = 1;
        public int saveVersion = CurrentSaveVersion;
        public long revision;
        public string playerId = "local_player";
        public string displayName = "Raven Unit";
        public AccountProgressData account = new();
        public WalletData wallet = new();
        public RewardInventoryData inventory = new();
        public BattlePassProgressData battlePass = new();
        public List<MissionProgressData> missions = new();
        public List<RankedModeProgressData> rankedModes = new();
        public SeasonProgressData season = new();
        public List<string> processedOperationIds = new();
        public List<CurrencyTransaction> transactionLog = new();
        public bool legacyRankMigrated;

        public PlayerProgressionData Clone() => JsonUtility.FromJson<PlayerProgressionData>(JsonUtility.ToJson(this));

        public void Normalize()
        {
            saveVersion = CurrentSaveVersion;
            revision = Math.Max(0L, revision);
            playerId = string.IsNullOrWhiteSpace(playerId) ? "local_player" : playerId;
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Raven Unit" : displayName;
            account ??= new AccountProgressData();
            wallet ??= new WalletData();
            inventory ??= new RewardInventoryData();
            battlePass ??= new BattlePassProgressData();
            missions ??= new List<MissionProgressData>();
            rankedModes ??= new List<RankedModeProgressData>();
            season ??= new SeasonProgressData();
            processedOperationIds ??= new List<string>();
            transactionLog ??= new List<CurrencyTransaction>();
            account.level = Math.Max(1, account.level);
            account.totalXP = Math.Max(0L, account.totalXP);
            account.claimedLevelRewards ??= new List<string>();
            wallet.softCurrency = Math.Max(0L, wallet.softCurrency);
            wallet.premiumCurrency = Math.Max(0L, wallet.premiumCurrency);
            wallet.version = Math.Max(0L, wallet.version);
            inventory.items ??= new List<OwnedAccountItem>();
            battlePass.tier = Math.Max(1, battlePass.tier);
            battlePass.xpIntoTier = Math.Max(0L, battlePass.xpIntoTier);
            battlePass.claimedRewardIds ??= new List<string>();
            foreach (var mission in missions)
            {
                if (mission == null) continue;
                mission.progress = Math.Max(0L, mission.progress);
            }
            foreach (var ranked in rankedModes)
            {
                if (ranked == null) continue;
                ranked.rating = Math.Max(0, ranked.rating);
                ranked.highestRating = Math.Max(ranked.rating, ranked.highestRating);
                ranked.matches = Math.Max(0, ranked.matches);
                ranked.victories = Math.Max(0, Math.Min(ranked.matches, ranked.victories));
                ranked.kills = Math.Max(0, ranked.kills);
                ranked.damage = Math.Max(0, ranked.damage);
                ranked.revives = Math.Max(0, ranked.revives);
                ranked.bestPlacement = Math.Max(0, ranked.bestPlacement);
            }
            season.claimedSeasonRewardIds ??= new List<string>();
        }
    }

    public readonly struct AccountCommandResult
    {
        public AccountCommandResult(bool success, string code, string message = "")
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
        public bool Success { get; }
        public string Code { get; }
        public string Message { get; }
        public static AccountCommandResult Ok(string code = "success") => new(true, code);
        public static AccountCommandResult Fail(string code, string message) => new(false, code, message);
    }

    [Serializable]
    public sealed class MatchCompletionRequest
    {
        public string operationId;
        public string matchId;
        public string playerId;
        public BRMatchMode mode;
        public BRPlaylist playlist;
        public int placement;
        public int teamCount;
        public int kills;
        public int assists;
        public int damage;
        public int revives;
        public int survivalSeconds;
    }

    public sealed class MatchProgressionResult
    {
        public bool Success { get; internal set; }
        public bool Duplicate { get; internal set; }
        public string Message { get; internal set; }
        public long AccountXPGained { get; internal set; }
        public long CoinsGained { get; internal set; }
        public long BattlePassXPGained { get; internal set; }
        public int RankDelta { get; internal set; }
        public int PreviousLevel { get; internal set; }
        public int Level { get; internal set; }
        public int PreviousBattlePassTier { get; internal set; }
        public int BattlePassTier { get; internal set; }
        public RankedTier PreviousRank { get; internal set; }
        public RankedTier Rank { get; internal set; }
        public RankedMatchResult RankedResult { get; internal set; }
        public List<string> CompletedMissionIds { get; } = new();
        public List<string> GrantedRewardIds { get; } = new();
    }
}
