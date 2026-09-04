using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    [RequireComponent(typeof(HealthArmorSystem), typeof(BRParticipant))]
    public sealed class DamageContributorTracker : MonoBehaviour
    {
        private readonly Dictionary<BRParticipant, Contribution> contributions = new();
        private HealthArmorSystem health;
        private BRParticipant victim;
        private BRParticipant knockedBy;
        private float knockTimestamp;
        private string knockWeaponId;
        private bool victimWasKnocked;

        private void Awake()
        {
            health = GetComponent<HealthArmorSystem>();
            victim = GetComponent<BRParticipant>();
            health.Damaged += OnDamaged;
            health.Downed += OnDowned;
            health.Revived += OnRevived;
        }

        public EliminationInfo Resolve(float assistWindow)
        {
            var now = Time.time;
            var finalSource = health.LastDamageSource != null
                ? health.LastDamageSource.GetComponent<BRParticipant>() : null;
            var killer = IsEnemy(finalSource) ? finalSource : null;
            var finalType = ResolveDamageType(health.LastDamageSource);
            if (killer == null && knockedBy != null && now - knockTimestamp <= Mathf.Max(1f, assistWindow))
                killer = knockedBy;

            BRParticipant assistant = null;
            var bestDamage = 0f;
            foreach (var pair in contributions)
            {
                if (pair.Key == null || pair.Key == killer || !IsEnemy(pair.Key)
                    || now - pair.Value.LastTimestamp > Mathf.Max(1f, assistWindow)
                    || pair.Value.Damage <= bestDamage) continue;
                bestDamage = pair.Value.Damage;
                assistant = pair.Key;
            }

            var weaponId = string.Empty;
            var headshot = false;
            if (killer != null && contributions.TryGetValue(killer, out var killingContribution))
            {
                weaponId = killingContribution.WeaponId;
                headshot = killingContribution.LastHeadshot;
            }
            if (string.IsNullOrEmpty(weaponId) && killer == knockedBy) weaponId = knockWeaponId;
            var distance = killer != null ? Vector3.Distance(killer.transform.position, transform.position) : 0f;
            return new EliminationInfo(victim, killer, assistant, finalType, weaponId, headshot,
                transform.position, distance, victimWasKnocked, victimWasKnocked && finalSource != null,
                knockedBy != null ? knockedBy.PlayerId : string.Empty,
                finalSource != null ? finalSource.PlayerId : string.Empty, now);
        }

        public void ResetTracker()
        {
            contributions.Clear();
            knockedBy = null;
            knockTimestamp = 0f;
            knockWeaponId = string.Empty;
            victimWasKnocked = false;
        }

        private void OnDamaged(HealthArmorSystem owner, DamageReport report)
        {
            var attacker = report.Source != null ? report.Source.GetComponent<BRParticipant>() : null;
            if (!IsEnemy(attacker) || report.AppliedDamage <= 0f) return;
            contributions.TryGetValue(attacker, out var contribution);
            contribution.Damage += report.AppliedDamage;
            contribution.LastTimestamp = Time.time;
            contribution.LastHeadshot = report.Headshot;
            contribution.WeaponId = attacker.Inventory?.ActiveWeapon != null
                ? attacker.Inventory.ActiveWeapon.name : string.Empty;
            contributions[attacker] = contribution;
        }

        private void OnDowned(HealthArmorSystem owner)
        {
            victimWasKnocked = true;
            knockedBy = owner.LastDamageSource != null
                ? owner.LastDamageSource.GetComponent<BRParticipant>() : null;
            knockTimestamp = Time.time;
            knockWeaponId = knockedBy?.Inventory?.ActiveWeapon != null
                ? knockedBy.Inventory.ActiveWeapon.name : string.Empty;
        }

        private void OnRevived(HealthArmorSystem owner)
        {
            knockedBy = null;
            knockTimestamp = 0f;
            knockWeaponId = string.Empty;
            victimWasKnocked = false;
        }

        private bool IsEnemy(BRParticipant attacker) => attacker != null && victim != null
            && attacker != victim && BRMatchRules.AreEnemies(attacker, victim);

        private static DamageType ResolveDamageType(GameObject source)
        {
            if (source == null) return DamageType.BleedOut;
            if (source.GetComponent<SafeZoneController>() != null) return DamageType.SafeZone;
            return source.GetComponent<BRParticipant>() != null ? DamageType.Weapon : DamageType.Unknown;
        }

        private void OnDestroy()
        {
            if (health == null) return;
            health.Damaged -= OnDamaged;
            health.Downed -= OnDowned;
            health.Revived -= OnRevived;
        }

        private struct Contribution
        {
            public float Damage;
            public float LastTimestamp;
            public bool LastHeadshot;
            public string WeaponId;
        }
    }
}
