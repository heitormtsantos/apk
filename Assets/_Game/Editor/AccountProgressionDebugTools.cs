using System;
using UnityEditor;
using UnityEngine;

namespace BattleRoyale.Editor
{
    public static class AccountProgressionDebugTools
    {
        [MenuItem("Raven Drop/Account/Grant Test Match Rewards", true)]
        private static bool CanGrantTestRewards() =>
            UnityEditor.EditorApplication.isPlaying
            && PlayerProfileService.Current != null
            && PlayerProfileService.Current.State == ProfileLoadState.Loaded;

        [MenuItem("Raven Drop/Account/Grant Test Match Rewards")]
        private static void GrantTestRewards()
        {
            var profile = PlayerProfileService.Current;
            var id = "editor_test_" + Guid.NewGuid().ToString("N");
            profile.Wallet.GrantSoftCurrency(id + "_coins", 500,
                CurrencyTransactionReason.AdminGrant, "unity_editor");
            profile.Levels.GrantAccountXP(id + "_account_xp", 1000, AccountXPSource.Tutorial);
            profile.BattlePass.GrantXP(id + "_battle_pass_xp", 1000);
            profile.Flush();
            Debug.Log("[AccountProgression] Test rewards granted: 500 Coins, 1000 Account XP, 1000 Battle Pass XP.");
        }

        [MenuItem("Raven Drop/Account/Print Profile Snapshot", true)]
        private static bool CanPrintProfile() => PlayerProfileService.Current != null;

        [MenuItem("Raven Drop/Account/Print Profile Snapshot")]
        private static void PrintProfile()
        {
            var profile = PlayerProfileService.Current;
            var snapshot = profile.Snapshot();
            Debug.Log(snapshot == null
                ? "[AccountProgression] Profile is not loaded."
                : $"[AccountProgression] Level {snapshot.account.level}, XP {snapshot.account.totalXP}, "
                  + $"Coins {snapshot.wallet.softCurrency}, Diamonds {snapshot.wallet.premiumCurrency}, "
                  + $"Battle Pass tier {snapshot.battlePass.tier}.");
        }
    }
}
