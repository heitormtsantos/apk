using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class PlayerProfileService : MonoBehaviour
    {
        private const int MaxProcessedOperations = 512;
        private const int MaxTransactions = 128;
        private const float SaveDebounceSeconds = 2f;
        private IProfileRepository repository;
        private float saveAt;
        private bool dirty;

        public static PlayerProfileService Current { get; private set; }
        public ProfileLoadState State { get; private set; } = ProfileLoadState.Loading;
        public string LastError { get; private set; }
        public PlayerProgressionData Data { get; private set; }
        public AccountLevelConfig LevelConfig { get; private set; }
        public BattlePassSeasonData BattlePassSeason { get; private set; }
        public MissionCatalog MissionCatalog { get; private set; }
        public MatchProgressionConfig MatchProgressionConfig { get; private set; }
        public AccountSeasonData SeasonConfig { get; private set; }
        public AccountWallet Wallet { get; private set; }
        public RewardService Rewards { get; private set; }
        public AccountLevelSystem Levels { get; private set; }
        public BattlePassService BattlePass { get; private set; }
        public MissionService Missions { get; private set; }
        public AccountRankedService Ranked { get; private set; }
        public AccountProgressionGateway Progression { get; private set; }
        public SeasonService Seasons { get; private set; }
        public PurchaseService Purchases { get; private set; }

        public event Action ProfileLoaded;
        public event Action ProfileChanged;
        public event Action<WalletSnapshot> SoftCurrencyChanged;
        public event Action<WalletSnapshot> PremiumCurrencyChanged;
        public event Action<long, long> AccountXPChanged;
        public event Action<int, int> AccountLevelUp;
        public event Action<long, long> BattlePassXPChanged;
        public event Action<int, int> BattlePassTierUp;
        public event Action<BRMatchMode, int, int> RankPointsChanged;
        public event Action<string> MissionCompleted;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(this);
                return;
            }
            Current = this;
        }

        public void Configure(IProfileRepository profileRepository = null,
            AccountLevelConfig levelConfig = null, BattlePassSeasonData battlePass = null,
            MissionCatalog missionCatalog = null, MatchProgressionConfig matchProgression = null,
            AccountSeasonData seasonConfig = null)
        {
            repository = profileRepository ?? new LocalProfileRepository();
            LevelConfig = levelConfig != null ? levelConfig : AccountLevelConfig.CreateRuntimeDefault();
            BattlePassSeason = battlePass != null ? battlePass : BattlePassSeasonData.CreateRuntimeDefault();
            MissionCatalog = missionCatalog != null ? missionCatalog : BattleRoyale.MissionCatalog.CreateRuntimeDefault();
            MatchProgressionConfig = matchProgression != null ? matchProgression : BattleRoyale.MatchProgressionConfig.CreateRuntimeDefault();
            SeasonConfig = seasonConfig != null ? seasonConfig : AccountSeasonData.CreateRuntimeDefault();
            State = ProfileLoadState.Loading;
            var loaded = repository.Load();
            Data = loaded.Profile ?? new PlayerProgressionData();
            Data.Normalize();
            State = loaded.Success ? ProfileLoadState.Loaded : ProfileLoadState.Failed;
            LastError = loaded.Error;
            BuildServices();
            if (State == ProfileLoadState.Loaded)
            {
                MigrateLegacyRankedData();
                Seasons.Synchronize("season_sync:" + SeasonConfig.SeasonId);
                ProfileLoaded?.Invoke();
            }
            else Debug.LogError($"[PlayerProfileService] Profile load failed: {LastError}");
        }

        private void BuildServices()
        {
            Wallet = new AccountWallet(this);
            Rewards = new RewardService(this);
            Levels = new AccountLevelSystem(this);
            BattlePass = new BattlePassService(this);
            Missions = new MissionService(this);
            Ranked = new AccountRankedService(this);
            Progression = new AccountProgressionGateway(this);
            Seasons = new SeasonService(this, new SystemTimeProvider());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var mockPurchases = new MockPurchaseProvider();
            Purchases = new PurchaseService(this, mockPurchases, mockPurchases);
#else
            Purchases = new PurchaseService(this, null, null);
#endif
        }

        internal AccountCommandResult Transact(string operationId,
            Func<PlayerProgressionData, AccountCommandResult> operation, bool flush = false)
        {
            if (State != ProfileLoadState.Loaded || Data == null)
                return AccountCommandResult.Fail("profile_not_loaded", "[PlayerProfileService] Profile nao carregado.");
            if (string.IsNullOrWhiteSpace(operationId))
                return AccountCommandResult.Fail("invalid_operation", "Operation ID obrigatorio.");
            if (Data.processedOperationIds.Contains(operationId))
                return AccountCommandResult.Fail("duplicate", "Operacao ja processada.");

            var before = Data;
            var candidate = before.Clone();
            var result = operation(candidate);
            if (!result.Success) return result;
            candidate.Normalize();
            candidate.revision = before.revision + 1L;
            candidate.processedOperationIds.Add(operationId);
            Trim(candidate.processedOperationIds, MaxProcessedOperations);
            Trim(candidate.transactionLog, MaxTransactions);
            Data = candidate;
            dirty = true;
            saveAt = Time.unscaledTime + SaveDebounceSeconds;
            EmitChanges(before, candidate);
            ProfileChanged?.Invoke();
            if (flush) Flush();
            return result;
        }

        public PlayerProgressionData Snapshot() => Data?.Clone();

        public bool Flush()
        {
            if (!dirty || repository == null || Data == null) return true;
            if (!repository.Save(Data, out var error))
            {
                LastError = error;
                Debug.LogError($"[PlayerProfileService] Save failed: {error}");
                return false;
            }
            dirty = false;
            return true;
        }

        private void Update()
        {
            if (dirty && Time.unscaledTime >= saveAt) Flush();
        }

        private void OnApplicationPause(bool paused) { if (paused) Flush(); }
        private void OnApplicationQuit() => Flush();
        private void OnDestroy()
        {
            Flush();
            if (Current == this) Current = null;
        }

        private void EmitChanges(PlayerProgressionData before, PlayerProgressionData after)
        {
            var snapshot = new WalletSnapshot(after.wallet.softCurrency,
                after.wallet.premiumCurrency, after.wallet.version);
            if (before.wallet.softCurrency != after.wallet.softCurrency) SoftCurrencyChanged?.Invoke(snapshot);
            if (before.wallet.premiumCurrency != after.wallet.premiumCurrency) PremiumCurrencyChanged?.Invoke(snapshot);
            if (before.account.totalXP != after.account.totalXP)
                AccountXPChanged?.Invoke(before.account.totalXP, after.account.totalXP);
            if (before.account.level != after.account.level)
                AccountLevelUp?.Invoke(before.account.level, after.account.level);
            if (before.battlePass.xpIntoTier != after.battlePass.xpIntoTier)
                BattlePassXPChanged?.Invoke(before.battlePass.xpIntoTier, after.battlePass.xpIntoTier);
            if (before.battlePass.tier != after.battlePass.tier)
                BattlePassTierUp?.Invoke(before.battlePass.tier, after.battlePass.tier);
            foreach (var ranked in after.rankedModes)
            {
                var old = FindRanked(before, ranked.mode);
                if (old == null || old.rating != ranked.rating)
                    RankPointsChanged?.Invoke(ranked.mode, old?.rating ?? 0, ranked.rating);
            }
            foreach (var mission in after.missions)
            {
                if (!mission.completed) continue;
                var old = FindMission(before, mission.missionId);
                if (old == null || !old.completed) MissionCompleted?.Invoke(mission.missionId);
            }
        }

        private void MigrateLegacyRankedData()
        {
            if (Data.legacyRankMigrated) return;
            var candidate = Data.Clone();
            foreach (var mode in new[] { BRMatchMode.Solo, BRMatchMode.Duo, BRMatchMode.Squad })
            {
                var legacy = RankedProgression.Load(mode);
                var ranked = AccountRankedService.FindOrCreate(candidate, mode,
                    BattlePassSeason != null ? BattlePassSeason.SeasonId : "season_local");
                ranked.rating = legacy.rating;
                ranked.highestRating = legacy.rating;
                ranked.matches = legacy.matches;
                ranked.victories = legacy.victories;
                ranked.kills = legacy.kills;
                ranked.damage = legacy.damage;
                ranked.revives = legacy.revives;
                ranked.bestPlacement = legacy.bestPlacement;
            }
            candidate.legacyRankMigrated = true;
            candidate.revision = Data.revision + 1L;
            Data = candidate;
            dirty = true;
            saveAt = Time.unscaledTime + SaveDebounceSeconds;
        }

        private static MissionProgressData FindMission(PlayerProgressionData profile, string id)
        {
            foreach (var value in profile.missions) if (value != null && value.missionId == id) return value;
            return null;
        }

        private static RankedModeProgressData FindRanked(PlayerProgressionData profile, BRMatchMode mode)
        {
            foreach (var value in profile.rankedModes) if (value != null && value.mode == mode) return value;
            return null;
        }

        private static void Trim<T>(List<T> list, int maximum)
        {
            if (list.Count > maximum) list.RemoveRange(0, list.Count - maximum);
        }
    }
}
