using System;
using UnityEngine;

namespace BattleRoyale
{
    public enum RankedTier
    {
        Bronze,
        Silver,
        Gold,
        Platinum,
        Diamond,
        Master
    }

    public readonly struct RankedScoreBreakdown
    {
        public RankedScoreBreakdown(int placementPoints, int killPoints, int damagePoints,
            int revivePoints, int victoryBonus, int entryCost)
        {
            PlacementPoints = placementPoints;
            KillPoints = killPoints;
            DamagePoints = damagePoints;
            RevivePoints = revivePoints;
            VictoryBonus = victoryBonus;
            EntryCost = entryCost;
            UnclampedDelta = placementPoints + killPoints + damagePoints + revivePoints
                + victoryBonus - entryCost;
            Delta = Math.Max(-30, Math.Min(70, UnclampedDelta));
        }

        public int PlacementPoints { get; }
        public int KillPoints { get; }
        public int KillsPoints => KillPoints;
        public int DamagePoints { get; }
        public int RevivePoints { get; }
        public int RevivesPoints => RevivePoints;
        public int VictoryBonus { get; }
        public int EntryCost { get; }
        public int UnclampedDelta { get; }
        public int Delta { get; }
        public int Total => Delta;

        public static RankedScoreBreakdown Calculate(int placement, int teamCount, int kills,
            int damage, int revives, RankedTier tier) =>
            RankedProgression.CalculateScore(placement, teamCount, kills, damage, revives, tier);
    }

    [Serializable]
    public sealed class RankedProfile
    {
        public int version = RankedProgression.CurrentVersion;
        public BRMatchMode mode;
        public int rating;
        public int matches;
        public int victories;
        public int kills;
        public int damage;
        public int revives;
        public int bestPlacement;

        public int Rating => rating;
        public int Matches => matches;
        public int GamesPlayed => matches;
        public int Victories => victories;
        public int Wins => victories;
        public int Kills => kills;
        public int Damage => damage;
        public int Revives => revives;
        public int BestPlacement => bestPlacement;
        public RankedTier Tier => RankedProgression.TierForRating(rating);

        public RankedProfile Clone() => new()
        {
            version = version,
            mode = mode,
            rating = rating,
            matches = matches,
            victories = victories,
            kills = kills,
            damage = damage,
            revives = revives,
            bestPlacement = bestPlacement
        };
    }

    public sealed class RankedMatchResult
    {
        public BRMatchMode Mode { get; internal set; }
        public int Placement { get; internal set; }
        public int TeamCount { get; internal set; }
        public int Kills { get; internal set; }
        public int Damage { get; internal set; }
        public int Revives { get; internal set; }
        public int PreviousRating { get; internal set; }
        public int Rating { get; internal set; }
        public int NewRating => Rating;
        public RankedTier PreviousTier { get; internal set; }
        public RankedTier Tier { get; internal set; }
        public RankedTier NewTier => Tier;
        public RankedScoreBreakdown Breakdown { get; internal set; }
        public int Delta => Breakdown.Delta;
    }

    public readonly struct RankedResultInput
    {
        public RankedResultInput(int placement, int teamCount, int kills, int damage, int revives)
        {
            TeamCount = Math.Max(1, teamCount);
            Placement = Math.Max(1, Math.Min(TeamCount, placement));
            Kills = Math.Max(0, kills);
            Damage = Math.Max(0, damage);
            Revives = Math.Max(0, revives);
        }

        public int Placement { get; }
        public int TeamCount { get; }
        public int Kills { get; }
        public int Damage { get; }
        public int Revives { get; }
    }

    public static class RankedProgression
    {
        public const int CurrentVersion = 1;
        private const string KeyPrefix = "RavenDrop.Ranked.v1.";
        private static readonly int[] Thresholds = { 0, 400, 800, 1300, 1900, 2600 };
        private static readonly int[] EntryCosts = { 0, 3, 6, 9, 12, 15 };

        public static int ThresholdForTier(RankedTier tier) => Thresholds[ClampTier(tier)];

        public static int GetThreshold(RankedTier tier) => ThresholdForTier(tier);

        public static int EntryCostForTier(RankedTier tier) => EntryCosts[ClampTier(tier)];

        public static int GetEntryCost(RankedTier tier) => EntryCostForTier(tier);

        public static RankedTier TierForRating(int rating)
        {
            var safeRating = Math.Max(0, rating);
            for (var i = Thresholds.Length - 1; i >= 0; i--)
                if (safeRating >= Thresholds[i]) return (RankedTier)i;
            return RankedTier.Bronze;
        }

        public static RankedTier GetTier(int rating) => TierForRating(rating);

        public static RankedScoreBreakdown CalculateScore(int placement, int teamCount, int kills,
            int damage, int revives, RankedTier tier)
        {
            var input = NormalizeResultInput(placement, teamCount, kills, damage, revives);
            return CalculateNormalizedScore(input, tier);
        }

        public static RankedResultInput NormalizeResultInput(int placement, int teamCount, int kills,
            int damage, int revives) => new(placement, teamCount, kills, damage, revives);

        private static RankedScoreBreakdown CalculateNormalizedScore(RankedResultInput input,
            RankedTier tier)
        {
            var placementPoints = input.Placement == 1 ? 30
                : input.Placement <= (int)Math.Ceiling(input.TeamCount * 0.25d) ? 18
                : input.Placement <= (int)Math.Ceiling(input.TeamCount * 0.5d) ? 8
                : -10;
            var killPoints = Math.Min(6, input.Kills) * 7;
            var damagePoints = Math.Min(10, input.Damage / 150);
            var revivePoints = Math.Min(3, input.Revives) * 6;
            var victoryBonus = input.Placement == 1 ? 12 : 0;
            return new RankedScoreBreakdown(placementPoints, killPoints, damagePoints,
                revivePoints, victoryBonus, EntryCostForTier(tier));
        }

        public static RankedScoreBreakdown Calculate(int placement, int teamCount, int kills,
            int damage, int revives, RankedTier tier) =>
            CalculateScore(placement, teamCount, kills, damage, revives, tier);

        public static RankedProfile Load(BRMatchMode mode)
        {
            RankedProfile profile = null;
            var key = KeyForMode(mode);
            var hadStoredValue = PlayerPrefs.HasKey(key);
            if (hadStoredValue)
            {
                try
                {
                    profile = JsonUtility.FromJson<RankedProfile>(PlayerPrefs.GetString(key, string.Empty));
                }
                catch (ArgumentException)
                {
                    profile = null;
                }
            }
            profile ??= NewProfile(mode);
            Normalize(profile, mode);
            if (hadStoredValue)
            {
                PlayerPrefs.SetString(key, JsonUtility.ToJson(profile));
                PlayerPrefs.Save();
            }
            return profile;
        }

        public static void Save(BRMatchMode mode, RankedProfile profile)
        {
            profile ??= NewProfile(mode);
            Normalize(profile, mode);
            PlayerPrefs.SetString(KeyForMode(mode), JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }

        public static RankedMatchResult ApplyResult(BRMatchMode mode, BRPlaylist playlist,
            int placement, int teamCount, int kills, int damage, int revives)
        {
            if (playlist != BRPlaylist.Ranked) return null;
            var input = NormalizeResultInput(placement, teamCount, kills, damage, revives);
            var profile = Load(mode);
            var previousRating = profile.rating;
            var previousTier = profile.Tier;
            var breakdown = CalculateNormalizedScore(input, previousTier);
            profile.rating = AddClamped(profile.rating, breakdown.Delta);
            profile.matches = AddClamped(profile.matches, 1);
            if (input.Placement == 1) profile.victories = AddClamped(profile.victories, 1);
            profile.kills = AddClamped(profile.kills, input.Kills);
            profile.damage = AddClamped(profile.damage, input.Damage);
            profile.revives = AddClamped(profile.revives, input.Revives);
            if (profile.bestPlacement <= 0 || input.Placement < profile.bestPlacement)
                profile.bestPlacement = input.Placement;
            Save(mode, profile);
            return new RankedMatchResult
            {
                Mode = mode,
                Placement = input.Placement,
                TeamCount = input.TeamCount,
                Kills = input.Kills,
                Damage = input.Damage,
                Revives = input.Revives,
                PreviousRating = previousRating,
                Rating = profile.rating,
                PreviousTier = previousTier,
                Tier = profile.Tier,
                Breakdown = breakdown
            };
        }

        public static string KeyForMode(BRMatchMode mode) => KeyPrefix + mode;

        public static void Delete(BRMatchMode mode)
        {
            PlayerPrefs.DeleteKey(KeyForMode(mode));
            PlayerPrefs.Save();
        }

        private static RankedProfile NewProfile(BRMatchMode mode) => new() { mode = mode };

        private static void Normalize(RankedProfile profile, BRMatchMode mode)
        {
            profile.version = CurrentVersion;
            profile.mode = mode;
            profile.rating = Math.Max(0, profile.rating);
            profile.matches = Math.Max(0, profile.matches);
            profile.victories = Math.Max(0, Math.Min(profile.matches, profile.victories));
            profile.kills = Math.Max(0, profile.kills);
            profile.damage = Math.Max(0, profile.damage);
            profile.revives = Math.Max(0, profile.revives);
            profile.bestPlacement = Math.Max(0, profile.bestPlacement);
        }

        private static int ClampTier(RankedTier tier) => Math.Max(0,
            Math.Min(Thresholds.Length - 1, (int)tier));

        private static int AddClamped(int value, int delta) =>
            (int)Math.Max(0L, Math.Min(int.MaxValue, (long)value + delta));
    }
}
