using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class VehicleCollisionDamage : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float vehicleDamageThreshold = 7f;
        [SerializeField, Min(0f)] private float vehicleDamageScale = 3.5f;
        [SerializeField, Min(0f)] private float minimumRamSpeedKmh = 16f;
        [SerializeField, Min(0f)] private float maximumRamDamage = 95f;
        [SerializeField, Min(0.05f)] private float targetCooldown = 0.8f;

        private readonly Dictionary<HealthArmorSystem, float> lastHitAt = new();
        private VehicleController controller;
        private VehicleHealth vehicleHealth;

        private void Awake()
        {
            controller = GetComponent<VehicleController>();
            vehicleHealth = GetComponent<VehicleHealth>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            var impact = collision.relativeVelocity.magnitude;
            if (impact >= vehicleDamageThreshold)
                vehicleHealth?.ApplyDamage((impact - vehicleDamageThreshold) * vehicleDamageScale,
                    DamageType.Vehicle, collision.gameObject);

            var target = collision.collider.GetComponentInParent<HealthArmorSystem>();
            if (target == null || controller == null || controller.CurrentSpeedKmh < minimumRamSpeedKmh) return;
            if (lastHitAt.TryGetValue(target, out var previous) && Time.time - previous < targetCooldown) return;
            lastHitAt[target] = Time.time;
            var t = Mathf.InverseLerp(minimumRamSpeedKmh, 100f, controller.CurrentSpeedKmh);
            target.ApplyDamage(Mathf.Lerp(10f, maximumRamDamage, t), false, gameObject);
        }
    }
}
