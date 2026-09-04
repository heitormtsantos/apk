using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class AccountWallet
    {
        private readonly PlayerProfileService profile;
        internal AccountWallet(PlayerProfileService owner) => profile = owner;

        public WalletSnapshot Snapshot => profile?.Data?.wallet == null ? default
            : new WalletSnapshot(profile.Data.wallet.softCurrency,
                profile.Data.wallet.premiumCurrency, profile.Data.wallet.version);

        public long GetBalance(CurrencyType type) => type == CurrencyType.SoftCurrency
            ? Snapshot.SoftCurrency : Snapshot.PremiumCurrency;
        public bool CanAfford(CurrencyType type, long amount) => amount >= 0L && GetBalance(type) >= amount;

        public AccountCommandResult TrySpend(string transactionId, CurrencyType type, long amount,
            CurrencyTransactionReason reason, string sourceId = "", string productId = "") =>
            profile.Transact(transactionId, data => ApplyDebit(data, transactionId, type, amount,
                reason, sourceId, productId), true);

        public AccountCommandResult GrantSoftCurrency(string transactionId, long amount,
            CurrencyTransactionReason reason, string sourceId = "") =>
            profile.Transact(transactionId, data => ApplyCredit(data, transactionId,
                CurrencyType.SoftCurrency, amount, reason, sourceId, string.Empty));

        internal AccountCommandResult GrantPremiumAuthorized(string transactionId, long amount,
            CurrencyTransactionReason reason, string sourceId, string productId) =>
            profile.Transact(transactionId, data => ApplyCredit(data, transactionId,
                CurrencyType.PremiumCurrency, amount, reason, sourceId, productId), true);

        internal static AccountCommandResult ApplyDebit(PlayerProgressionData data, string transactionId,
            CurrencyType type, long amount, CurrencyTransactionReason reason, string sourceId, string productId)
        {
            if (amount <= 0L) return AccountCommandResult.Fail("invalid_amount", "Amount must be positive.");
            var balance = Balance(data.wallet, type);
            if (balance < amount) return AccountCommandResult.Fail("insufficient_currency", "Saldo insuficiente.");
            SetBalance(data.wallet, type, balance - amount);
            data.wallet.version++;
            Log(data, transactionId, type, amount, CurrencyTransactionDirection.Debit, reason, sourceId, productId);
            return AccountCommandResult.Ok();
        }

        internal static AccountCommandResult ApplyCredit(PlayerProgressionData data, string transactionId,
            CurrencyType type, long amount, CurrencyTransactionReason reason, string sourceId, string productId)
        {
            if (amount <= 0L) return AccountCommandResult.Fail("invalid_amount", "Amount must be positive.");
            var balance = Balance(data.wallet, type);
            if (long.MaxValue - balance < amount)
                return AccountCommandResult.Fail("currency_overflow", "Currency limit exceeded.");
            SetBalance(data.wallet, type, balance + amount);
            data.wallet.version++;
            Log(data, transactionId, type, amount, CurrencyTransactionDirection.Credit, reason, sourceId, productId);
            return AccountCommandResult.Ok();
        }

        private static long Balance(WalletData wallet, CurrencyType type) =>
            type == CurrencyType.SoftCurrency ? wallet.softCurrency : wallet.premiumCurrency;
        private static void SetBalance(WalletData wallet, CurrencyType type, long value)
        {
            if (type == CurrencyType.SoftCurrency) wallet.softCurrency = value;
            else wallet.premiumCurrency = value;
        }
        private static void Log(PlayerProgressionData data, string id, CurrencyType type, long amount,
            CurrencyTransactionDirection direction, CurrencyTransactionReason reason,
            string sourceId, string productId) => data.transactionLog.Add(new CurrencyTransaction
        {
            transactionId = id,
            currencyType = type,
            amount = amount,
            direction = direction,
            reason = reason,
            unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            sourceId = sourceId ?? string.Empty,
            productId = productId ?? string.Empty
        });
    }

    public sealed class RewardService
    {
        private readonly PlayerProfileService profile;
        internal RewardService(PlayerProfileService owner) => profile = owner;

        public bool Owns(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || profile?.Data?.inventory?.items == null) return false;
            foreach (var item in profile.Data.inventory.items)
                if (item != null && item.itemId == itemId && item.quantity > 0) return true;
            return false;
        }

        public AccountCommandResult Grant(string operationId, IReadOnlyList<RewardDefinition> rewards,
            CurrencyTransactionReason reason, string sourceId, bool premiumAuthorized = false) =>
            profile.Transact(operationId, data => ApplyRewards(data, rewards, operationId,
                reason, sourceId, premiumAuthorized, profile.BattlePassSeason,
                profile.LevelConfig), true);

        internal static AccountCommandResult ApplyRewards(PlayerProgressionData data,
            IReadOnlyList<RewardDefinition> rewards, string operationId, CurrencyTransactionReason reason,
            string sourceId, bool premiumAuthorized, BattlePassSeasonData battlePassSeason = null,
            AccountLevelConfig accountLevelConfig = null)
        {
            if (rewards == null) return AccountCommandResult.Fail("invalid_rewards", "Reward list is null.");
            foreach (var reward in rewards)
            {
                if (reward == null || string.IsNullOrWhiteSpace(reward.rewardId) || reward.amount <= 0L)
                    return AccountCommandResult.Fail("invalid_reward", "Reward definition is invalid.");
                if (reward.type == RewardType.AccountItem && string.IsNullOrWhiteSpace(reward.itemId))
                    return AccountCommandResult.Fail("invalid_item", "Reward item ID is invalid.");
                if (reward.type == RewardType.PremiumCurrency && !premiumAuthorized)
                    return AccountCommandResult.Fail("premium_not_authorized", "Premium grant is not authorized.");
            }
            foreach (var reward in rewards)
            {
                AccountCommandResult result;
                switch (reward.type)
                {
                    case RewardType.SoftCurrency:
                        result = AccountWallet.ApplyCredit(data, operationId + ":" + reward.rewardId,
                            CurrencyType.SoftCurrency, reward.amount, reason, sourceId, string.Empty);
                        break;
                    case RewardType.PremiumCurrency:
                        result = AccountWallet.ApplyCredit(data, operationId + ":" + reward.rewardId,
                            CurrencyType.PremiumCurrency, reward.amount, reason, sourceId, string.Empty);
                        break;
                    case RewardType.AccountXP:
                        if (accountLevelConfig == null)
                            return AccountCommandResult.Fail("level_config_missing",
                                "Account level config is not available.");
                        result = AccountLevelSystem.ApplyXP(data, accountLevelConfig, reward.amount,
                            operationId + ":" + reward.rewardId, AccountXPSource.Mission,
                            battlePassSeason, out _, out _);
                        break;
                    case RewardType.AccountItem:
                        result = GrantItem(data.inventory, reward.itemId, reward.amount);
                        break;
                    case RewardType.BattlePassXP:
                        result = BattlePassService.ApplyXP(data, reward.amount,
                            battlePassSeason, out _, out _);
                        break;
                    default:
                        result = AccountCommandResult.Fail("unsupported_reward", "Unsupported reward type.");
                        break;
                }
                if (!result.Success) return result;
            }
            return AccountCommandResult.Ok();
        }

        private static AccountCommandResult GrantItem(RewardInventoryData inventory, string itemId, long amount)
        {
            if (amount > int.MaxValue) return AccountCommandResult.Fail("inventory_overflow", "Item amount too large.");
            foreach (var item in inventory.items)
            {
                if (item == null || item.itemId != itemId) continue;
                if ((long)item.quantity + amount > int.MaxValue)
                    return AccountCommandResult.Fail("inventory_overflow", "Item quantity limit exceeded.");
                item.quantity += (int)amount;
                return AccountCommandResult.Ok();
            }
            inventory.items.Add(new OwnedAccountItem { itemId = itemId, quantity = (int)amount });
            return AccountCommandResult.Ok();
        }
    }

    public sealed class AccountLevelSystem
    {
        private readonly PlayerProfileService profile;
        internal AccountLevelSystem(PlayerProfileService owner) => profile = owner;
        public int Level => profile.Data?.account?.level ?? 1;
        public long TotalXP => profile.Data?.account?.totalXP ?? 0L;
        public long XPRequiredForNextLevel => profile.LevelConfig.RequiredTotalXP(Math.Min(
            profile.LevelConfig.MaxAccountLevel, Level + 1));

        public AccountCommandResult GrantAccountXP(string operationId, long amount, AccountXPSource source) =>
            profile.Transact(operationId, data => ApplyXP(data, profile.LevelConfig, amount,
                operationId, source, profile.BattlePassSeason, out _, out _));

        internal static AccountCommandResult ApplyXP(PlayerProgressionData data, AccountLevelConfig config,
            long amount, string operationId, AccountXPSource source,
            BattlePassSeasonData battlePassSeason, out int previousLevel, out int level)
        {
            previousLevel = data.account.level;
            level = previousLevel;
            if (amount <= 0L) return AccountCommandResult.Fail("invalid_xp", "XP must be positive.");
            if (long.MaxValue - data.account.totalXP < amount)
                return AccountCommandResult.Fail("xp_overflow", "Account XP limit exceeded.");
            data.account.totalXP += amount;
            var maximum = config.MaxAccountLevel;
            while (data.account.level < maximum
                && data.account.totalXP >= config.RequiredTotalXP(data.account.level + 1))
            {
                data.account.level++;
                var claimId = $"account_level_{data.account.level:D3}";
                if (data.account.claimedLevelRewards.Contains(claimId)) continue;
                var result = RewardService.ApplyRewards(data, config.RewardsFor(data.account.level),
                    operationId + ":" + claimId, CurrencyTransactionReason.AccountLevelReward,
                    claimId, true, battlePassSeason, config);
                if (!result.Success) return result;
                data.account.claimedLevelRewards.Add(claimId);
            }
            if (data.account.level >= maximum && !config.AccumulateXPAtMaximum)
                data.account.totalXP = Math.Min(data.account.totalXP, config.RequiredTotalXP(maximum));
            level = data.account.level;
            return AccountCommandResult.Ok(source.ToString());
        }
    }

    public sealed class BattlePassService
    {
        private readonly PlayerProfileService profile;
        internal BattlePassService(PlayerProfileService owner) => profile = owner;
        public BattlePassProgressData Progress => profile.Data?.battlePass;

        public AccountCommandResult GrantXP(string operationId, long amount) =>
            profile.Transact(operationId, data => ApplyXP(data, amount, profile.BattlePassSeason,
                out _, out _));

        public AccountPurchaseResult BuyPremium(string operationId)
        {
            var season = profile.BattlePassSeason;
            if (season == null) return AccountPurchaseResult.InvalidProduct;
            if (!season.IsActive(DateTimeOffset.UtcNow.ToUnixTimeSeconds())) return AccountPurchaseResult.SeasonEnded;
            var result = profile.Transact(operationId, data =>
            {
                EnsureSeason(data, season);
                if (data.battlePass.premiumUnlocked)
                    return AccountCommandResult.Fail("already_owned", "Premium pass already owned.");
                var debit = AccountWallet.ApplyDebit(data, operationId + ":debit",
                    CurrencyType.PremiumCurrency, season.PremiumPrice,
                    CurrencyTransactionReason.BattlePassPurchase, season.SeasonId, "battle_pass_premium");
                if (!debit.Success) return debit;
                data.battlePass.premiumUnlocked = true;
                return AccountCommandResult.Ok();
            }, true);
            return result.Success ? AccountPurchaseResult.Success : result.Code switch
            {
                "duplicate" or "already_owned" => AccountPurchaseResult.AlreadyOwned,
                "insufficient_currency" => AccountPurchaseResult.InsufficientCurrency,
                _ => AccountPurchaseResult.Failed
            };
        }

        public AccountCommandResult Claim(string operationId, int tier, BattlePassTrack track)
        {
            var season = profile.BattlePassSeason;
            return profile.Transact(operationId, data =>
            {
                EnsureSeason(data, season);
                if (season == null || !season.IsActive(DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
                    return AccountCommandResult.Fail("season_ended", "Battle Pass season ended.");
                if (tier < 1 || tier > data.battlePass.tier)
                    return AccountCommandResult.Fail("tier_locked", "Battle Pass tier is locked.");
                if (track == BattlePassTrack.Premium && !data.battlePass.premiumUnlocked)
                    return AccountCommandResult.Fail("premium_required", "Premium Battle Pass required.");
                var claimId = $"bp_{season.SeasonId}_tier_{tier:D3}_{track.ToString().ToLowerInvariant()}";
                if (data.battlePass.claimedRewardIds.Contains(claimId))
                    return AccountCommandResult.Fail("duplicate", "Reward already claimed.");
                var tierData = season.Tier(tier);
                if (tierData == null) return AccountCommandResult.Fail("invalid_tier", "Tier definition not found.");
                var rewards = track == BattlePassTrack.Free ? tierData.freeRewards : tierData.premiumRewards;
                var grant = RewardService.ApplyRewards(data, rewards, operationId,
                    CurrencyTransactionReason.BattlePassReward, claimId, true, season,
                    profile.LevelConfig);
                if (!grant.Success) return grant;
                data.battlePass.claimedRewardIds.Add(claimId);
                return AccountCommandResult.Ok();
            }, true);
        }

        internal static AccountCommandResult ApplyXP(PlayerProgressionData data, long amount,
            BattlePassSeasonData profileSeason, out int previousTier, out int tier)
        {
            previousTier = data.battlePass.tier;
            tier = previousTier;
            if (amount <= 0L) return AccountCommandResult.Fail("invalid_bp_xp", "Battle Pass XP must be positive.");
            var season = profileSeason;
            if (season == null)
            {
                if (long.MaxValue - data.battlePass.xpIntoTier < amount)
                    return AccountCommandResult.Fail("bp_xp_overflow", "Battle Pass XP limit exceeded.");
                data.battlePass.xpIntoTier += amount;
                return AccountCommandResult.Ok();
            }
            EnsureSeason(data, season);
            data.battlePass.xpIntoTier += amount;
            while (data.battlePass.tier < season.Tiers.Count)
            {
                var current = season.Tier(data.battlePass.tier);
                var required = Math.Max(1L, current?.xpRequired ?? 1000L);
                if (data.battlePass.xpIntoTier < required) break;
                data.battlePass.xpIntoTier -= required;
                data.battlePass.tier++;
            }
            if (data.battlePass.tier >= Math.Max(1, season.Tiers.Count)) data.battlePass.xpIntoTier = 0L;
            tier = data.battlePass.tier;
            return AccountCommandResult.Ok();
        }

        internal static void EnsureSeason(PlayerProgressionData data, BattlePassSeasonData season)
        {
            if (season == null || data.battlePass.seasonId == season.SeasonId) return;
            data.battlePass = new BattlePassProgressData { seasonId = season.SeasonId, tier = 1 };
        }
    }

    public sealed class MissionService
    {
        private readonly PlayerProfileService profile;
        internal MissionService(PlayerProfileService owner) => profile = owner;

        internal List<string> ApplyMatch(PlayerProgressionData data, MatchCompletionRequest request,
            string operationId)
        {
            var completed = new List<string>();
            var now = DateTimeOffset.UtcNow;
            foreach (var definition in profile.MissionCatalog.Missions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.missionId)) continue;
                var progress = FindOrCreate(data, definition, now,
                    profile.SeasonConfig != null ? profile.SeasonConfig.StartsAtUnix : 0L);
                var increment = ObjectiveValue(definition.objective, request);
                progress.progress = Math.Min(Math.Max(1L, definition.target), progress.progress + increment);
                if (progress.completed || progress.progress < Math.Max(1L, definition.target)) continue;
                progress.completed = true;
                completed.Add(definition.missionId);
                var reward = RewardService.ApplyRewards(data, definition.rewards,
                    operationId + ":" + definition.missionId,
                    CurrencyTransactionReason.MissionReward, definition.missionId, true,
                    profile.BattlePassSeason, profile.LevelConfig);
                if (reward.Success) progress.rewardClaimed = true;
            }
            return completed;
        }

        public AccountCommandResult Claim(string operationId, string missionId) =>
            profile.Transact(operationId, data =>
            {
                var definition = profile.MissionCatalog.Find(missionId);
                if (definition == null) return AccountCommandResult.Fail("invalid_mission", "[MissionService] Mission ID invalido.");
                var progress = Find(data, missionId);
                if (progress == null || !progress.completed)
                    return AccountCommandResult.Fail("mission_incomplete", "Mission is not complete.");
                if (progress.rewardClaimed) return AccountCommandResult.Fail("duplicate", "Mission reward already claimed.");
                var reward = RewardService.ApplyRewards(data, definition.rewards, operationId,
                    CurrencyTransactionReason.MissionReward, missionId, true,
                    profile.BattlePassSeason, profile.LevelConfig);
                if (!reward.Success) return reward;
                progress.rewardClaimed = true;
                return AccountCommandResult.Ok();
            }, true);

        private static MissionProgressData FindOrCreate(PlayerProgressionData data, MissionData definition,
            DateTimeOffset now, long seasonWindow = 0L)
        {
            var window = definition.cadence switch
            {
                MissionCadence.Daily => now.ToUnixTimeSeconds() / 86400L,
                MissionCadence.Weekly => now.ToUnixTimeSeconds() / (86400L * 7L),
                _ => seasonWindow
            };
            var progress = Find(data, definition.missionId);
            if (progress == null)
            {
                progress = new MissionProgressData { missionId = definition.missionId, resetWindow = window };
                data.missions.Add(progress);
            }
            else if (progress.resetWindow != window)
            {
                progress.progress = 0L;
                progress.completed = false;
                progress.rewardClaimed = false;
                progress.resetWindow = window;
            }
            return progress;
        }

        private static MissionProgressData Find(PlayerProgressionData data, string id)
        {
            foreach (var value in data.missions) if (value != null && value.missionId == id) return value;
            return null;
        }

        private static long ObjectiveValue(MissionObjective objective, MatchCompletionRequest value) => objective switch
        {
            MissionObjective.PlayMatches => 1L,
            MissionObjective.WinMatches => value.placement == 1 ? 1L : 0L,
            MissionObjective.ReachTop10 => value.placement > 0 && value.placement <= 10 ? 1L : 0L,
            MissionObjective.Eliminations => Math.Max(0, value.kills),
            MissionObjective.Assists => Math.Max(0, value.assists),
            MissionObjective.Damage => Math.Max(0, value.damage),
            MissionObjective.Revives => Math.Max(0, value.revives),
            MissionObjective.SurvivalSeconds => Math.Max(0, value.survivalSeconds),
            _ => 0L
        };
    }

    public sealed class AccountRankedService
    {
        private readonly PlayerProfileService profile;
        internal AccountRankedService(PlayerProfileService owner) => profile = owner;
        public RankedModeProgressData Get(BRMatchMode mode)
        {
            mode = NormalizeMode(mode);
            foreach (var value in profile.Data.rankedModes)
                if (value != null && value.mode == mode) return value;
            return new RankedModeProgressData { mode = mode, seasonId = SeasonId };
        }
        private string SeasonId => profile.BattlePassSeason != null ? profile.BattlePassSeason.SeasonId : "season_local";

        internal RankedMatchResult Apply(PlayerProgressionData data, MatchCompletionRequest request)
        {
            if (request.playlist != BRPlaylist.Ranked) return null;
            var mode = NormalizeMode(request.mode);
            var ranked = FindOrCreate(data, mode, SeasonId);
            var previousRating = ranked.rating;
            var previousTier = RankedProgression.TierForRating(previousRating);
            var score = RankedProgression.CalculateScore(request.placement, request.teamCount,
                request.kills, request.damage, request.revives, previousTier);
            ranked.rating = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)ranked.rating + score.Delta));
            ranked.highestRating = Math.Max(ranked.highestRating, ranked.rating);
            ranked.matches++;
            if (request.placement == 1) ranked.victories++;
            ranked.kills += Math.Max(0, request.kills);
            ranked.damage += Math.Max(0, request.damage);
            ranked.revives += Math.Max(0, request.revives);
            if (ranked.bestPlacement <= 0 || request.placement < ranked.bestPlacement)
                ranked.bestPlacement = request.placement;
            return new RankedMatchResult
            {
                Mode = mode,
                Placement = request.placement,
                TeamCount = request.teamCount,
                Kills = request.kills,
                Damage = request.damage,
                Revives = request.revives,
                PreviousRating = previousRating,
                Rating = ranked.rating,
                PreviousTier = previousTier,
                Tier = RankedProgression.TierForRating(ranked.rating),
                Breakdown = score
            };
        }

        internal static RankedModeProgressData FindOrCreate(PlayerProgressionData data, BRMatchMode mode, string seasonId)
        {
            foreach (var value in data.rankedModes)
                if (value != null && value.mode == mode) return value;
            var created = new RankedModeProgressData { mode = mode, seasonId = seasonId };
            data.rankedModes.Add(created);
            return created;
        }

        public static BRMatchMode NormalizeMode(BRMatchMode mode) => mode switch
        {
            BRMatchMode.Duo => BRMatchMode.Duo,
            BRMatchMode.Squad => BRMatchMode.Squad,
            _ => BRMatchMode.Solo
        };
    }

    public sealed class AccountProgressionGateway
    {
        private readonly PlayerProfileService profile;
        internal AccountProgressionGateway(PlayerProfileService owner) => profile = owner;

        public MatchProgressionResult CompleteMatch(MatchCompletionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.operationId))
                return new MatchProgressionResult { Message = "Invalid match completion request." };
            var output = new MatchProgressionResult();
            var result = profile.Transact(request.operationId, data =>
            {
                output.PreviousLevel = data.account.level;
                output.PreviousBattlePassTier = data.battlePass.tier;
                var rankedBefore = AccountRankedService.FindOrCreate(data,
                    AccountRankedService.NormalizeMode(request.mode), profile.BattlePassSeason.SeasonId);
                output.PreviousRank = RankedProgression.TierForRating(rankedBefore.rating);
                output.AccountXPGained = profile.MatchProgressionConfig.AccountXP(request);
                output.CoinsGained = profile.MatchProgressionConfig.Coins(request);
                output.BattlePassXPGained = profile.MatchProgressionConfig.BattlePassXP(request);

                var coins = AccountWallet.ApplyCredit(data, request.operationId + ":coins",
                    CurrencyType.SoftCurrency, output.CoinsGained, CurrencyTransactionReason.MatchReward,
                    request.matchId, string.Empty);
                if (!coins.Success) return coins;
                var account = AccountLevelSystem.ApplyXP(data, profile.LevelConfig,
                    output.AccountXPGained, request.operationId + ":account_xp", AccountXPSource.Match,
                    profile.BattlePassSeason, out _, out var resultingLevel);
                if (!account.Success) return account;
                output.Level = resultingLevel;
                var pass = BattlePassService.ApplyXP(data, output.BattlePassXPGained,
                    profile.BattlePassSeason, out _, out var resultingTier);
                if (!pass.Success) return pass;
                output.BattlePassTier = resultingTier;
                output.CompletedMissionIds.AddRange(profile.Missions.ApplyMatch(data, request, request.operationId));
                var ranked = profile.Ranked.Apply(data, request);
                if (ranked != null)
                {
                    output.RankedResult = ranked;
                    output.RankDelta = ranked.Delta;
                    output.Rank = ranked.Tier;
                }
                else output.Rank = output.PreviousRank;
                return AccountCommandResult.Ok();
            }, true);
            output.Success = result.Success;
            output.Duplicate = result.Code == "duplicate";
            output.Message = result.Message;
            return output;
        }
    }
}
