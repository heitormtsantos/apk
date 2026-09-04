using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(fileName = "MatchEconomyConfig", menuName = "Battle Royale/Match Economy Config")]
    public sealed class MatchEconomyConfig : ScriptableObject
    {
        [Min(0)] public int startingCurrency = 600;
        [Min(0)] public int killReward = 200;
        [Min(0)] public int assistReward = 100;
        [Min(0)] public int objectiveReward = 150;
        [Min(0)] public int reviveReward = 100;
        [Min(0)] public int currencyPickupMin = 60;
        [Min(0)] public int currencyPickupMax = 180;
        [Min(0f)] public float purchaseRequestCooldown = 0.12f;
        public bool disableFireWhileShopping = true;
        public bool disableAimWhileShopping = true;
        public bool disableCameraWhileShopping = true;
        public bool disableMovementWhileShopping;
        public AudioClip shopOpenSound;
        public AudioClip shopCloseSound;
        public AudioClip purchaseSuccessSound;
        public AudioClip purchaseFailSound;

        public void Normalize()
        {
            startingCurrency = Mathf.Max(0, startingCurrency);
            killReward = Mathf.Max(0, killReward);
            assistReward = Mathf.Max(0, assistReward);
            objectiveReward = Mathf.Max(0, objectiveReward);
            reviveReward = Mathf.Max(0, reviveReward);
            currencyPickupMin = Mathf.Max(0, currencyPickupMin);
            currencyPickupMax = Mathf.Max(currencyPickupMin, currencyPickupMax);
            purchaseRequestCooldown = Mathf.Clamp(purchaseRequestCooldown, 0f, 2f);
        }
    }
}
