using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BattleRoyale.Tests
{
    public sealed class AccountProgressionTests
    {
        private GameObject root;
        private MemoryProfileRepository repository;
        private PlayerProfileService service;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Account Progression Test");
            repository = new MemoryProfileRepository();
            repository.Profile.legacyRankMigrated = true;
            service = root.AddComponent<PlayerProfileService>();
            service.Configure(repository);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            PlayerPrefs.DeleteKey(RankedProgression.KeyForMode(BRMatchMode.Solo));
            PlayerPrefs.DeleteKey(RankedProgression.KeyForMode(BRMatchMode.Duo));
            PlayerPrefs.DeleteKey(RankedProgression.KeyForMode(BRMatchMode.Squad));
        }

        [Test]
        public void WalletNeverGoesNegativeAndRejectsDuplicateOperation()
        {
            Assert.That(service.Wallet.GrantSoftCurrency("grant_1", 500,
                CurrencyTransactionReason.MatchReward).Success, Is.True);
            Assert.That(service.Wallet.TrySpend("spend_1", CurrencyType.SoftCurrency,
                200, CurrencyTransactionReason.ShopPurchase).Success, Is.True);
            Assert.That(service.Wallet.TrySpend("spend_2", CurrencyType.SoftCurrency,
                400, CurrencyTransactionReason.ShopPurchase).Success, Is.False);
            Assert.That(service.Wallet.GrantSoftCurrency("grant_1", 500,
                CurrencyTransactionReason.MatchReward).Code, Is.EqualTo("duplicate"));
            Assert.That(service.Wallet.GetBalance(CurrencyType.SoftCurrency), Is.EqualTo(300));
            Assert.That(service.Wallet.GetBalance(CurrencyType.PremiumCurrency), Is.Zero);
        }

        [Test]
        public void AccountXPProcessesMultipleLevelsWithoutLosingOverflow()
        {
            var result = service.Levels.GrantAccountXP("xp_1", 5000, AccountXPSource.Match);

            Assert.That(result.Success, Is.True);
            Assert.That(service.Levels.Level, Is.GreaterThan(2));
            Assert.That(service.Levels.TotalXP, Is.EqualTo(5000));
            Assert.That(service.Data.account.claimedLevelRewards.Count, Is.EqualTo(service.Levels.Level - 1));
        }

        [Test]
        public void BattlePassXPProcessesMultipleTiers()
        {
            var result = service.BattlePass.GrantXP("bp_xp_1", 3500);

            Assert.That(result.Success, Is.True);
            Assert.That(service.BattlePass.Progress.tier, Is.EqualTo(4));
            Assert.That(service.BattlePass.Progress.xpIntoTier, Is.EqualTo(500));
        }

        [Test]
        public void BattlePassRewardUsesActiveSeasonTierThresholds()
        {
            var rewards = new List<RewardDefinition>
            {
                new() { rewardId = "mission_bp_xp", type = RewardType.BattlePassXP, amount = 2500 }
            };

            var result = service.Rewards.Grant("reward_bp_1", rewards,
                CurrencyTransactionReason.MissionReward, "mission_test");

            Assert.That(result.Success, Is.True);
            Assert.That(service.Data.battlePass.tier, Is.EqualTo(3));
            Assert.That(service.Data.battlePass.xpIntoTier, Is.EqualTo(500));
        }

        [Test]
        public void GenericRewardCanGrantAccountXPThroughLevelSystem()
        {
            var rewards = new List<RewardDefinition>
            {
                new() { rewardId = "mission_account_xp", type = RewardType.AccountXP, amount = 1200 }
            };

            var result = service.Rewards.Grant("reward_account_xp_1", rewards,
                CurrencyTransactionReason.MissionReward, "mission_test");

            Assert.That(result.Success, Is.True);
            Assert.That(service.Levels.TotalXP, Is.EqualTo(1200));
            Assert.That(service.Levels.Level, Is.GreaterThan(1));
        }

        [Test]
        public void PremiumPassPurchaseIsAtomicAndCannotBePurchasedTwice()
        {
            repository.Profile.wallet.premiumCurrency = 800;
            UnityEngine.Object.DestroyImmediate(root);
            root = new GameObject("Premium Purchase Test");
            service = root.AddComponent<PlayerProfileService>();
            service.Configure(repository);

            Assert.That(service.BattlePass.BuyPremium("buy_bp_1"), Is.EqualTo(AccountPurchaseResult.Success));
            Assert.That(service.Wallet.GetBalance(CurrencyType.PremiumCurrency), Is.EqualTo(200));
            Assert.That(service.BattlePass.Progress.premiumUnlocked, Is.True);
            Assert.That(service.BattlePass.BuyPremium("buy_bp_2"), Is.EqualTo(AccountPurchaseResult.AlreadyOwned));
            Assert.That(service.Wallet.GetBalance(CurrencyType.PremiumCurrency), Is.EqualTo(200));
        }

        [Test]
        public void ValidatedPurchaseReceiptCanOnlyGrantPremiumCurrencyOnce()
        {
            var product = ScriptableObject.CreateInstance<PremiumCurrencyProductData>();
            SetPrivateField(product, "productId", "diamonds_120");
            SetPrivateField(product, "diamondAmount", 120L);
            var provider = new FixedPurchaseProvider();
            var purchases = new PurchaseService(service, provider, provider);

            Assert.That(purchases.RequestPurchaseCurrency(product), Is.EqualTo(AccountPurchaseResult.Pending));
            Assert.That(service.Wallet.GetBalance(CurrencyType.PremiumCurrency), Is.EqualTo(120));
            Assert.That(purchases.RequestPurchaseCurrency(product), Is.EqualTo(AccountPurchaseResult.Pending));
            Assert.That(service.Wallet.GetBalance(CurrencyType.PremiumCurrency), Is.EqualTo(120));
            Assert.That(purchases.State, Is.EqualTo(AccountPurchaseState.Failed));
            UnityEngine.Object.DestroyImmediate(product);
        }

        [Test]
        public void NewSeasonResetsEachRankedModeUsingConfiguredFactor()
        {
            UnityEngine.Object.DestroyImmediate(root);
            repository.Profile.season.activeSeasonId = "old_season";
            repository.Profile.rankedModes.Add(new RankedModeProgressData
            {
                mode = BRMatchMode.Solo,
                seasonId = "old_season",
                rating = 1000,
                highestRating = 1200,
                matches = 20,
                victories = 3
            });
            var season = ScriptableObject.CreateInstance<AccountSeasonData>();
            SetPrivateField(season, "seasonId", "new_season");
            SetPrivateField(season, "rankedResetFactor", 0.5f);
            root = new GameObject("Season Reset Test");
            service = root.AddComponent<PlayerProfileService>();

            service.Configure(repository, seasonConfig: season);

            var ranked = service.Ranked.Get(BRMatchMode.Solo);
            Assert.That(ranked.rating, Is.EqualTo(500));
            Assert.That(ranked.highestRating, Is.EqualTo(500));
            Assert.That(ranked.matches, Is.Zero);
            Assert.That(ranked.victories, Is.Zero);
            Assert.That(ranked.seasonId, Is.EqualTo("new_season"));
            UnityEngine.Object.DestroyImmediate(season);
        }

        [Test]
        public void MatchCompletionRewardsOnceAndCasualDoesNotChangeRank()
        {
            var request = Request("match_once", BRPlaylist.Casual);
            var first = service.Progression.CompleteMatch(request);
            var coins = service.Wallet.GetBalance(CurrencyType.SoftCurrency);
            var duplicate = service.Progression.CompleteMatch(request);

            Assert.That(first.Success, Is.True);
            Assert.That(first.AccountXPGained, Is.GreaterThan(0));
            Assert.That(first.BattlePassXPGained, Is.GreaterThan(0));
            Assert.That(first.RankDelta, Is.Zero);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(service.Wallet.GetBalance(CurrencyType.SoftCurrency), Is.EqualTo(coins));
            Assert.That(service.Ranked.Get(BRMatchMode.Solo).rating, Is.Zero);
        }

        [Test]
        public void RankedProgressIsSeparatedForSoloDuoAndSquad()
        {
            service.Progression.CompleteMatch(Request("rank_solo", BRPlaylist.Ranked, BRMatchMode.Solo));
            service.Progression.CompleteMatch(Request("rank_duo", BRPlaylist.Ranked, BRMatchMode.Duo));

            Assert.That(service.Ranked.Get(BRMatchMode.Solo).matches, Is.EqualTo(1));
            Assert.That(service.Ranked.Get(BRMatchMode.Duo).matches, Is.EqualTo(1));
            Assert.That(service.Ranked.Get(BRMatchMode.Squad).matches, Is.Zero);
            Assert.That(AccountRankedService.NormalizeMode(BRMatchMode.Trio), Is.EqualTo(BRMatchMode.Solo));
        }

        [Test]
        public void LocalRepositoryRecoversLastGoodBackup()
        {
            var folder = Path.Combine(Path.GetTempPath(), "raven-profile-tests-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(folder, "profile.json");
            try
            {
                var local = new LocalProfileRepository(path);
                var profile = new PlayerProgressionData();
                profile.wallet.softCurrency = 100;
                Assert.That(local.Save(profile, out _), Is.True);
                profile.wallet.softCurrency = 200;
                Assert.That(local.Save(profile, out _), Is.True);
                File.WriteAllText(path, "corrupt");

                var loaded = local.Load();
                Assert.That(loaded.Success, Is.True);
                Assert.That(loaded.RecoveredBackup, Is.True);
                Assert.That(loaded.Profile.wallet.softCurrency, Is.EqualTo(100));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        private static MatchCompletionRequest Request(string id, BRPlaylist playlist,
            BRMatchMode mode = BRMatchMode.Solo) => new()
        {
            operationId = id,
            matchId = id,
            playerId = "local_player",
            mode = mode,
            playlist = playlist,
            placement = 2,
            teamCount = 20,
            kills = 6,
            assists = 2,
            damage = 2200,
            revives = 1,
            survivalSeconds = 1080
        };

        private static void SetPrivateField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);

        private sealed class FixedPurchaseProvider : IPurchaseProvider, IPurchaseReceiptValidator
        {
            public bool Available => true;
            public void Purchase(string productId, Action<PurchaseProviderResult> completed) =>
                completed?.Invoke(new PurchaseProviderResult(true, "fixed_transaction", productId,
                    "valid_receipt", string.Empty));
            public void Restore(Action<bool> completed) => completed?.Invoke(true);
            public bool Validate(PurchaseProviderResult purchase, out string authorizationId)
            {
                authorizationId = purchase.TransactionId;
                return purchase.Success && purchase.Receipt == "valid_receipt";
            }
        }

        private sealed class MemoryProfileRepository : IProfileRepository
        {
            public PlayerProgressionData Profile { get; private set; } = new();
            public ProfileLoadResult Load() => new(true, Profile.Clone(), false, string.Empty);
            public bool Save(PlayerProgressionData profile, out string error)
            {
                Profile = profile.Clone();
                error = string.Empty;
                return true;
            }
        }
    }
}
