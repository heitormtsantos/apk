using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    [Serializable]
    public sealed class RewardDefinition
    {
        public string rewardId;
        public RewardType type;
        public CurrencyType currencyType;
        [Min(1)] public long amount = 1;
        public string itemId;
    }

    [Serializable]
    public sealed class AccountLevelData
    {
        [Min(1)] public int level = 1;
        [Min(0)] public long requiredTotalXP;
        public List<RewardDefinition> rewards = new();
    }

    [CreateAssetMenu(menuName = "Battle Royale/Account/Level Config", fileName = "AccountLevelConfig")]
    public sealed class AccountLevelConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int maxAccountLevel = 100;
        [SerializeField] private bool accumulateXPAtMaximum;
        [SerializeField] private List<AccountLevelData> levels = new();

        public int MaxAccountLevel => Mathf.Max(1, maxAccountLevel);
        public bool AccumulateXPAtMaximum => accumulateXPAtMaximum;
        public IReadOnlyList<AccountLevelData> Levels => levels;

        public long RequiredTotalXP(int level)
        {
            if (level <= 1) return 0L;
            foreach (var data in levels)
                if (data != null && data.level == level) return Math.Max(0L, data.requiredTotalXP);
            var n = level - 1L;
            return 50L * n * n + 50L * n;
        }

        public IReadOnlyList<RewardDefinition> RewardsFor(int level)
        {
            foreach (var data in levels)
                if (data != null && data.level == level) return data.rewards;
            return Array.Empty<RewardDefinition>();
        }

        public static AccountLevelConfig CreateRuntimeDefault()
        {
            var value = CreateInstance<AccountLevelConfig>();
            value.hideFlags = HideFlags.DontSave;
            for (var level = 2; level <= 100; level++)
            {
                var entry = new AccountLevelData
                {
                    level = level,
                    requiredTotalXP = 50L * (level - 1L) * (level - 1L) + 50L * (level - 1L)
                };
                if (level % 5 == 0)
                    entry.rewards.Add(new RewardDefinition
                    {
                        rewardId = $"account_level_{level:D3}_coins",
                        type = RewardType.SoftCurrency,
                        amount = level * 100L
                    });
                value.levels.Add(entry);
            }
            return value;
        }
    }

    [Serializable]
    public sealed class BattlePassTierData
    {
        [Min(1)] public int tier = 1;
        [Min(1)] public long xpRequired = 1000;
        public List<RewardDefinition> freeRewards = new();
        public List<RewardDefinition> premiumRewards = new();
    }

    [CreateAssetMenu(menuName = "Battle Royale/Account/Battle Pass Season", fileName = "BattlePassSeason")]
    public sealed class BattlePassSeasonData : ScriptableObject
    {
        [SerializeField] private string seasonId = "season_2026_01";
        [SerializeField] private long startsAtUnix;
        [SerializeField] private long endsAtUnix = 1893456000;
        [SerializeField, Min(0)] private long premiumPrice = 600;
        [SerializeField] private List<BattlePassTierData> tiers = new();
        public string SeasonId => string.IsNullOrWhiteSpace(seasonId) ? "season_local" : seasonId;
        public long StartsAtUnix => startsAtUnix;
        public long EndsAtUnix => endsAtUnix;
        public long PremiumPrice => Math.Max(0L, premiumPrice);
        public IReadOnlyList<BattlePassTierData> Tiers => tiers;
        public bool IsActive(long unixNow) => unixNow >= startsAtUnix && (endsAtUnix <= 0L || unixNow < endsAtUnix);
        public BattlePassTierData Tier(int tier)
        {
            foreach (var data in tiers) if (data != null && data.tier == tier) return data;
            return null;
        }
        public static BattlePassSeasonData CreateRuntimeDefault()
        {
            var value = CreateInstance<BattlePassSeasonData>();
            value.hideFlags = HideFlags.DontSave;
            for (var tier = 1; tier <= 50; tier++)
            {
                var entry = new BattlePassTierData { tier = tier, xpRequired = 1000 };
                entry.freeRewards.Add(new RewardDefinition
                {
                    rewardId = $"bp_{value.SeasonId}_tier_{tier:D3}_free_coins",
                    type = RewardType.SoftCurrency,
                    amount = 100 + tier * 10L
                });
                entry.premiumRewards.Add(new RewardDefinition
                {
                    rewardId = $"bp_{value.SeasonId}_tier_{tier:D3}_premium_item",
                    type = RewardType.AccountItem,
                    amount = 1,
                    itemId = $"bp_{value.SeasonId}_cosmetic_{tier:D3}"
                });
                value.tiers.Add(entry);
            }
            return value;
        }
    }

    [Serializable]
    public sealed class MissionData
    {
        public string missionId;
        public MissionCadence cadence;
        public MissionObjective objective;
        [Min(1)] public long target = 1;
        public List<RewardDefinition> rewards = new();
    }

    [CreateAssetMenu(menuName = "Battle Royale/Account/Mission Catalog", fileName = "MissionCatalog")]
    public sealed class MissionCatalog : ScriptableObject
    {
        [SerializeField] private List<MissionData> missions = new();
        public IReadOnlyList<MissionData> Missions => missions;
        public MissionData Find(string id)
        {
            foreach (var mission in missions) if (mission != null && mission.missionId == id) return mission;
            return null;
        }
        public static MissionCatalog CreateRuntimeDefault()
        {
            var value = CreateInstance<MissionCatalog>();
            value.hideFlags = HideFlags.DontSave;
            value.missions.Add(Create("mission_daily_play_1", MissionCadence.Daily, MissionObjective.PlayMatches, 1, 150));
            value.missions.Add(Create("mission_daily_kills_5", MissionCadence.Daily, MissionObjective.Eliminations, 5, 250));
            value.missions.Add(Create("mission_week_damage_5000", MissionCadence.Weekly, MissionObjective.Damage, 5000, 700));
            return value;
        }
        private static MissionData Create(string id, MissionCadence cadence, MissionObjective objective,
            long target, long bpXp) => new()
        {
            missionId = id,
            cadence = cadence,
            objective = objective,
            target = target,
            rewards = new List<RewardDefinition>
            {
                new() { rewardId = id + "_reward", type = RewardType.BattlePassXP, amount = bpXp }
            }
        };
    }

    [CreateAssetMenu(menuName = "Battle Royale/Account/Match Progression", fileName = "MatchProgressionConfig")]
    public sealed class MatchProgressionConfig : ScriptableObject
    {
        [SerializeField, Min(0)] private int baseAccountXP = 100;
        [SerializeField, Min(0)] private int accountXPPerKill = 55;
        [SerializeField, Min(0)] private int accountXPPerMinute = 8;
        [SerializeField, Min(0)] private int baseCoins = 80;
        [SerializeField, Min(0)] private int coinsPerKill = 35;
        [SerializeField, Min(0)] private int baseBattlePassXP = 70;
        [SerializeField, Min(0)] private int battlePassXPPerKill = 24;
        public long AccountXP(MatchCompletionRequest request) => Math.Max(0L,
            baseAccountXP + request.kills * accountXPPerKill + request.survivalSeconds / 60 * accountXPPerMinute
            + Math.Max(0, request.teamCount - request.placement) * 5L);
        public long Coins(MatchCompletionRequest request) => Math.Max(0L,
            baseCoins + request.kills * coinsPerKill + (request.placement == 1 ? 200L : 0L));
        public long BattlePassXP(MatchCompletionRequest request) => Math.Max(0L,
            baseBattlePassXP + request.kills * battlePassXPPerKill + request.assists * 12L);
        public static MatchProgressionConfig CreateRuntimeDefault()
        {
            var value = CreateInstance<MatchProgressionConfig>();
            value.hideFlags = HideFlags.DontSave;
            return value;
        }
    }
}
