using UnityEngine;

namespace BattleRoyale
{
    public sealed class BallisticProjectile : MonoBehaviour
    {
        private static Material sharedMaterial;
        private Vector3 velocity;
        private WeaponDefinition weapon;
        private GameObject owner;
        private LayerMask hitMask;
        private float travelled;
        private WeaponSystem source;

        public static BallisticProjectile Launch(Vector3 origin, Vector3 direction,
            WeaponDefinition weapon, GameObject owner, LayerMask hitMask, WeaponSystem source)
        {
            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = $"{weapon.displayName} Projectile";
            projectile.transform.position = origin;
            projectile.transform.localScale = Vector3.one * 0.025f;
            var collider = projectile.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            sharedMaterial ??= BRMaterialFactory.Create("Ballistic Projectile", new Color(1f, 0.72f, 0.22f));
            projectile.GetComponent<Renderer>().sharedMaterial = sharedMaterial;
            var component = projectile.AddComponent<BallisticProjectile>();
            component.weapon = weapon;
            component.owner = owner;
            component.hitMask = hitMask;
            component.source = source;
            component.velocity = direction.normalized * Mathf.Max(1f, weapon.projectileVelocity);
            return component;
        }

        private void Update()
        {
            if (weapon == null)
            {
                Destroy(gameObject);
                return;
            }
            var step = velocity * Time.deltaTime;
            var distance = step.magnitude;
            if (distance > 0.0001f && TryFirstHit(transform.position, step / distance, distance, out var hit))
            {
                ResolveImpact(hit);
                return;
            }
            transform.position += step;
            travelled += distance;
            if (weapon.projectileUsesGravity) velocity += Physics.gravity * Time.deltaTime;
            if (travelled >= weapon.range) Destroy(gameObject);
        }

        private bool TryFirstHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit first)
        {
            var hits = Physics.RaycastAll(origin, direction, distance, hitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, RaycastHitDistanceComparer.Instance);
            foreach (var hit in hits)
            {
                var collider = hit.collider;
                if (collider == null || owner != null && collider.transform.IsChildOf(owner.transform)) continue;
                if (collider.GetComponentInParent<LootPickup>() != null
                    || collider.GetComponentInParent<BallisticProjectile>() != null) continue;
                first = hit;
                return true;
            }
            first = default;
            return false;
        }

        private void ResolveImpact(RaycastHit hit)
        {
            var health = hit.collider.GetComponentInParent<HealthArmorSystem>();
            var vehicleHealth = hit.collider.GetComponentInParent<VehicleHealth>();
            var characterHit = health != null && health.gameObject != owner && WeaponSystem.CanDamage(owner, health);
            var headshot = characterHit && hit.collider.name.Contains("Head");
            var color = new Color(0.9f, 0.92f, 1f);
            if (characterHit)
            {
                var damage = WeaponSystem.DamageAtDistance(weapon, travelled + hit.distance)
                    * (headshot ? weapon.headshotMultiplier : 1f);
                var report = health.ApplyDamage(damage, headshot, owner);
                color = headshot ? DamageFeedbackSystem.HeadshotColor : DamageFeedbackSystem.BodyColor;
                var shooter = owner != null ? owner.GetComponent<BRParticipant>() : null;
                if (report.FinalDamage > 0f && shooter != null)
                {
                    shooter.DamageDealt += Mathf.RoundToInt(report.AppliedDamage);
                    if (shooter.IsPlayer) DamageFeedbackSystem.Report(hit.point, report.FinalDamage, headshot);
                }
                source?.ReportProjectileHit(hit.point, report.FinalDamage, headshot);
            }
            else if (vehicleHealth != null && vehicleHealth.gameObject != owner)
            {
                var damage = WeaponSystem.DamageAtDistance(weapon, travelled + hit.distance);
                var applied = vehicleHealth.ApplyDamage(damage, DamageType.Weapon, owner);
                color = applied > 0f ? DamageFeedbackSystem.BodyColor : color;
                if (applied > 0f && owner != null && owner.GetComponent<BRParticipant>() is { IsPlayer: true })
                    DamageFeedbackSystem.Report(hit.point, applied, false);
                source?.ReportProjectileHit(hit.point, applied, false);
            }
            CombatEffectPool.SpawnImpact(hit.point, hit.normal, color);
            CombatWorldAudio.PlayImpact(hit.point, characterHit);
            Destroy(gameObject);
        }
    }
}
