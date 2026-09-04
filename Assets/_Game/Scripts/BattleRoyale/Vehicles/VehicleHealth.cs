using System;
using System.Collections;
using UnityEngine;

namespace BattleRoyale
{
    public enum VehicleDamageState { Healthy, Damaged, Critical, Burning, Destroyed }

    public sealed class VehicleHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 850f;
        [SerializeField] private bool explodeImmediately;
        [SerializeField, Min(0f)] private float explosionDelay = 2f;
        [SerializeField, Min(0f)] private float explosionRadius = 5f;
        [SerializeField, Min(0f)] private float explosionDamage = 90f;
        [SerializeField] private ParticleSystem lightSmoke;
        [SerializeField] private ParticleSystem criticalSmoke;
        [SerializeField] private ParticleSystem explosionEffect;

        private float currentHealth;
        private bool initialized;
        private bool destructionStarted;
        private VehicleController controller;

        public event Action<VehicleHealth, float, DamageType, GameObject> Damaged;
        public event Action<VehicleDamageState> StateChanged;
        public event Action<VehicleHealth> Destroyed;
        public float CurrentHealth => initialized ? currentHealth : MaxHealth;
        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public float NormalizedHealth => CurrentHealth / MaxHealth;
        public VehicleDamageState State { get; private set; } = VehicleDamageState.Healthy;
        public bool IsDestroyed => State == VehicleDamageState.Destroyed;

        private void Awake()
        {
            controller = GetComponent<VehicleController>();
            InitializeFull();
        }

        public void Configure(float health)
        {
            maxHealth = Mathf.Max(1f, health);
            InitializeFull();
        }

        public void InitializeFull()
        {
            initialized = true;
            currentHealth = MaxHealth;
            destructionStarted = false;
            SetState(VehicleDamageState.Healthy);
            SetEffect(lightSmoke, false);
            SetEffect(criticalSmoke, false);
        }

        public float ApplyDamage(float amount, DamageType type, GameObject source)
        {
            if (!initialized) InitializeFull();
            if (IsDestroyed || amount <= 0f) return 0f;
            var applied = Mathf.Min(currentHealth, amount);
            currentHealth -= applied;
            Damaged?.Invoke(this, applied, type, source);
            if (currentHealth <= 0f) BeginDestruction();
            else RefreshState();
            return applied;
        }

        private void RefreshState()
        {
            var next = NormalizedHealth <= 0.25f ? VehicleDamageState.Critical
                : NormalizedHealth <= 0.6f ? VehicleDamageState.Damaged : VehicleDamageState.Healthy;
            SetState(next);
            SetEffect(lightSmoke, next is VehicleDamageState.Damaged or VehicleDamageState.Critical);
            SetEffect(criticalSmoke, next == VehicleDamageState.Critical);
        }

        private void BeginDestruction()
        {
            if (destructionStarted) return;
            destructionStarted = true;
            controller?.SetControlsEnabled(false);
            var seats = GetComponent<VehicleSeatManager>();
            seats?.ForceExitAll();
            if (explodeImmediately || explosionDelay <= 0f) Explode();
            else
            {
                SetState(VehicleDamageState.Burning);
                SetEffect(criticalSmoke, true);
                StartCoroutine(ExplosionRoutine());
            }
        }

        private IEnumerator ExplosionRoutine()
        {
            yield return new WaitForSeconds(explosionDelay);
            Explode();
        }

        private void Explode()
        {
            if (IsDestroyed) return;
            SetState(VehicleDamageState.Destroyed);
            SetEffect(lightSmoke, false);
            SetEffect(criticalSmoke, false);
            if (explosionEffect != null) explosionEffect.Play(true);
            if (explosionRadius > 0f && explosionDamage > 0f)
            {
                foreach (var hit in Physics.OverlapSphere(transform.position, explosionRadius,
                             ~0, QueryTriggerInteraction.Ignore))
                {
                    var health = hit.GetComponentInParent<HealthArmorSystem>();
                    if (health == null) continue;
                    var distance = Vector3.Distance(transform.position, hit.transform.position);
                    var damage = explosionDamage * (1f - Mathf.Clamp01(distance / explosionRadius));
                    health.ApplyDamage(damage, false, gameObject);
                }
            }
            Destroyed?.Invoke(this);
        }

        private void SetState(VehicleDamageState value)
        {
            if (State == value) return;
            State = value;
            StateChanged?.Invoke(value);
        }

        private static void SetEffect(ParticleSystem effect, bool active)
        {
            if (effect == null) return;
            if (active && !effect.isPlaying) effect.Play(true);
            else if (!active && effect.isPlaying) effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
