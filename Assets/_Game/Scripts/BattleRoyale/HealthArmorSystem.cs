using System;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class HealthArmorSystem : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private int vestLevel;
        [SerializeField] private int helmetLevel;

        private float health;
        private bool initialized;
        private float invulnerableUntil;
        private float bleedoutDuration = 24f;
        private float reviveHealth = 35f;
        private System.Func<bool> shouldDownOnLethal;
        private bool diedEmitted;
        private bool reduceRepeatedBleedout;
        private float bleedoutReductionPerKnockdown;
        private float minimumBleedoutDuration = 8f;

        public event Action<HealthArmorSystem, DamageReport> Damaged;
        public event Action<HealthArmorSystem> Downed;
        public event Action<HealthArmorSystem> Revived;
        public event Action<HealthArmorSystem> Died;
        public event Action<HealthArmorSystem> Changed;

        public float Health => initialized ? health : Mathf.Max(1f, maxHealth);
        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public int VestLevel => vestLevel;
        public int HelmetLevel => helmetLevel;
        public bool IsDowned { get; private set; }
        public bool IsDead { get; private set; }
        public PlayerLifeState LifeState => IsDead ? PlayerLifeState.Dead
            : IsDowned ? PlayerLifeState.Knocked : PlayerLifeState.Alive;
        public int KnockdownCount { get; private set; }
        public float BleedoutRemaining { get; private set; }
        public float BleedoutRatio => IsDowned ? Mathf.Clamp01(BleedoutRemaining / Mathf.Max(0.01f, bleedoutDuration)) : 0f;
        public int DamageSequence { get; private set; }
        public GameObject LastDamageSource { get; private set; }

        private void Awake()
        {
            InitializeFull();
        }

        private void OnEnable()
        {
            if (!initialized) InitializeFull();
        }

        public void InitializeFull()
        {
            initialized = true;
            maxHealth = Mathf.Max(1f, maxHealth);
            health = maxHealth;
            IsDowned = false;
            IsDead = false;
            BleedoutRemaining = 0f;
            diedEmitted = false;
            DamageSequence = 0;
            invulnerableUntil = 0f;
            LastDamageSource = null;
            KnockdownCount = 0;
            Changed?.Invoke(this);
        }

        public void ConfigureLifeCycle(float bleedoutSeconds, float healthAfterRevive,
            System.Func<bool> lethalDownPolicy)
        {
            bleedoutDuration = Mathf.Clamp(bleedoutSeconds, 1f, 300f);
            reviveHealth = Mathf.Clamp(healthAfterRevive, 1f, MaxHealth);
            shouldDownOnLethal = lethalDownPolicy;
        }

        public void ConfigureKnockdownRules(BattleRoyaleLifeConfig config)
        {
            if (config == null) return;
            reduceRepeatedBleedout = config.ReduceBleedoutOnRepeatedKnockdowns;
            bleedoutReductionPerKnockdown = config.BleedoutReductionPerKnockdown;
            minimumBleedoutDuration = config.MinimumBleedoutTime;
        }

        public DamageReport ApplyDamage(float rawDamage, bool headshot, GameObject source)
        {
            var armorMultiplier = headshot ? HelmetMultiplier(helmetLevel) : VestMultiplier(vestLevel);
            return CommitDamage(rawDamage, rawDamage * armorMultiplier, headshot, source);
        }

        public DamageReport ApplyCompositeDamage(float bodyDamage, float headDamage, GameObject source)
        {
            bodyDamage = Mathf.Max(0f, bodyDamage);
            headDamage = Mathf.Max(0f, headDamage);
            var rawDamage = bodyDamage + headDamage;
            var finalDamage = bodyDamage * VestMultiplier(vestLevel) + headDamage * HelmetMultiplier(helmetLevel);
            return CommitDamage(rawDamage, finalDamage, headDamage > 0f, source);
        }

        private DamageReport CommitDamage(float rawDamage, float finalDamage, bool headshot, GameObject source)
        {
            if (!initialized) InitializeFull();
            if (IsDead || rawDamage <= 0f || Time.time < invulnerableUntil) return default;
            if (IsDowned)
            {
                LastDamageSource = source;
                DamageSequence++;
                var finishingReport = new DamageReport(rawDamage, Mathf.Max(0f, finalDamage), 0f, headshot, source);
                Damaged?.Invoke(this, finishingReport);
                Eliminate();
                return finishingReport;
            }
            var healthBeforeDamage = health;
            health = Mathf.Max(0f, health - finalDamage);
            LastDamageSource = source;
            DamageSequence++;
            var report = new DamageReport(rawDamage, finalDamage, healthBeforeDamage - health, headshot, source);
            Damaged?.Invoke(this, report);
            if (health <= 0f)
            {
                if (shouldDownOnLethal != null && shouldDownOnLethal()) EnterDowned();
                else Eliminate();
            }
            else Changed?.Invoke(this);
            return report;
        }

        private void EnterDowned()
        {
            if (IsDowned || IsDead) return;
            health = 0f;
            IsDowned = true;
            KnockdownCount++;
            var repeatedPenalty = reduceRepeatedBleedout
                ? Mathf.Max(0, KnockdownCount - 1) * bleedoutReductionPerKnockdown : 0f;
            BleedoutRemaining = Mathf.Max(minimumBleedoutDuration, bleedoutDuration - repeatedPenalty);
            Downed?.Invoke(this);
            Changed?.Invoke(this);
        }

        public void TickBleedout(float deltaTime)
        {
            if (!IsDowned || IsDead || deltaTime <= 0f) return;
            BleedoutRemaining = Mathf.Max(0f, BleedoutRemaining - deltaTime);
            Changed?.Invoke(this);
            if (BleedoutRemaining <= 0f) Eliminate();
        }

        public void Eliminate()
        {
            if (IsDead) return;
            health = 0f;
            IsDowned = false;
            IsDead = true;
            BleedoutRemaining = 0f;
            Changed?.Invoke(this);
            if (diedEmitted) return;
            diedEmitted = true;
            Died?.Invoke(this);
        }

        public void SetInvulnerable(float seconds)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + Mathf.Max(0f, seconds));
        }

        public void Heal(float amount)
        {
            if (!initialized) InitializeFull();
            if (IsDead || IsDowned || amount <= 0f) return;
            health = Mathf.Min(maxHealth, health + amount);
            Changed?.Invoke(this);
        }

        public void EquipVest(int level)
        {
            if (IsDead || IsDowned) return;
            vestLevel = Mathf.Clamp(level, 0, 4);
            Changed?.Invoke(this);
        }

        public void EquipHelmet(int level)
        {
            if (IsDead || IsDowned) return;
            helmetLevel = Mathf.Clamp(level, 0, 4);
            Changed?.Invoke(this);
        }

        public void Revive()
        {
            if (!IsDowned || IsDead) return;
            health = Mathf.Min(MaxHealth, reviveHealth);
            IsDowned = false;
            BleedoutRemaining = 0f;
            SetInvulnerable(1f);
            Revived?.Invoke(this);
            Changed?.Invoke(this);
        }

        public static float VestMultiplier(int level) => level switch
        {
            1 => 0.78f,
            2 => 0.64f,
            3 => 0.50f,
            4 => 0.38f,
            _ => 1f
        };

        public static float HelmetMultiplier(int level) => level switch
        {
            1 => 0.72f,
            2 => 0.58f,
            3 => 0.44f,
            4 => 0.32f,
            _ => 1f
        };
    }

    public readonly struct DamageReport
    {
        public DamageReport(float rawDamage, float finalDamage, float appliedDamage, bool headshot, GameObject source)
        {
            RawDamage = rawDamage;
            FinalDamage = finalDamage;
            AppliedDamage = appliedDamage;
            Headshot = headshot;
            Source = source;
        }

        public float RawDamage { get; }
        public float FinalDamage { get; }
        public float AppliedDamage { get; }
        public bool Headshot { get; }
        public GameObject Source { get; }
    }
}
