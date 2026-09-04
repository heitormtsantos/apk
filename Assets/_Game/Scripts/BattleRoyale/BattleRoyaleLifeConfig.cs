using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(menuName = "Battle Royale/Life Config", fileName = "BattleRoyaleLifeConfig")]
    public sealed class BattleRoyaleLifeConfig : ScriptableObject
    {
        [Header("Knockdown")]
        [SerializeField] private bool enableKnockdown = true;
        [SerializeField, Min(1f)] private float assistWindow = 10f;
        [SerializeField] private bool reduceBleedoutOnRepeatedKnockdowns = true;
        [SerializeField, Min(0f)] private float bleedoutReductionPerKnockdown = 5f;
        [SerializeField, Min(1f)] private float minimumBleedoutTime = 8f;

        [Header("Death loot")]
        [SerializeField, Min(10f)] private float deathBoxLifetime = 180f;
        [SerializeField] private bool destroyWhenEmpty = true;
        [SerializeField] private bool dropMatchCurrency;

        public bool EnableKnockdown => enableKnockdown;
        public float AssistWindow => Mathf.Max(1f, assistWindow);
        public bool ReduceBleedoutOnRepeatedKnockdowns => reduceBleedoutOnRepeatedKnockdowns;
        public float BleedoutReductionPerKnockdown => Mathf.Max(0f, bleedoutReductionPerKnockdown);
        public float MinimumBleedoutTime => Mathf.Max(1f, minimumBleedoutTime);
        public float DeathBoxLifetime => Mathf.Max(10f, deathBoxLifetime);
        public bool DestroyWhenEmpty => destroyWhenEmpty;
        public bool DropMatchCurrency => dropMatchCurrency;

        public static BattleRoyaleLifeConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<BattleRoyaleLifeConfig>();
            config.hideFlags = HideFlags.DontSave;
            return config;
        }
    }
}
