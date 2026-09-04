using UnityEngine;

namespace BattleRoyale
{
    public sealed class MatchRewardSystem : MonoBehaviour
    {
        private MatchEconomyConfig config;

        public void Configure(MatchEconomyConfig settings)
        {
            config = settings;
            config?.Normalize();
        }

        public int RewardElimination(BRParticipant participant)
            => Reward(participant, config != null ? config.killReward : 0);

        public int RewardRevive(BRParticipant participant)
            => Reward(participant, config != null ? config.reviveReward : 0);

        private static int Reward(BRParticipant participant, int amount)
        {
            if (participant == null || amount <= 0) return 0;
            var wallet = participant.GetComponent<MatchCurrencyWallet>();
            if (wallet == null) return 0;
            wallet.AddCurrency(amount);
            return amount;
        }
    }
}
