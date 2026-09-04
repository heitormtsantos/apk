using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class SafeZoneDamageSystem : MonoBehaviour
    {
        private float accumulatedTime;

        public void ResetDamageClock() => accumulatedTime = 0f;

        public void Tick(IEnumerable<BRParticipant> participants, SafeZoneController zone, float deltaTime,
            float tickInterval, Func<bool> shouldContinue)
        {
            if (participants == null || zone == null || deltaTime <= 0f || zone.CurrentState == SafeZoneState.Inactive)
                return;

            var interval = Mathf.Clamp(tickInterval, 0.05f, 10f);
            accumulatedTime += deltaTime;
            var ticks = Mathf.FloorToInt(accumulatedTime / interval);
            if (ticks <= 0) return;
            accumulatedTime -= ticks * interval;
            var damage = zone.GetCurrentDamagePerSecond() * interval * ticks;
            if (damage <= 0f) return;

            foreach (var participant in participants)
            {
                if (shouldContinue != null && !shouldContinue()) break;
                if (participant == null || participant.Health == null || participant.Health.IsDead
                    || participant.Phase is not (ParticipantPhase.Grounded or ParticipantPhase.Downed)) continue;
                if (!zone.IsInsideSafeZone(participant.transform.position))
                    participant.Health.ApplyDamage(damage, false, zone.gameObject);
            }
        }
    }
}
