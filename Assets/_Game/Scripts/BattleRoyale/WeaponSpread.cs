using UnityEngine;

namespace BattleRoyale
{
    public sealed class WeaponSpread
    {
        public float ShotKick { get; private set; }
        public int ShotsInSequence { get; private set; }
        private float recoveryDelay;

        public void RegisterShot(WeaponDefinition weapon)
        {
            if (weapon == null) return;
            ShotsInSequence++;
            var increase = weapon.continuousFireSpreadIncrease > 0f
                ? weapon.continuousFireSpreadIncrease : weapon.shotSpreadKick;
            ShotKick = Mathf.Min(Mathf.Max(0f, weapon.maxHipFireSpread),
                ShotKick + Mathf.Max(0f, increase));
            recoveryDelay = Mathf.Max(0.08f, weapon.fireInterval * 1.65f);
        }

        public void Tick(WeaponDefinition weapon, float deltaTime)
        {
            var recovery = weapon != null ? weapon.spreadRecoverySpeed : 5f;
            recoveryDelay = Mathf.Max(0f, recoveryDelay - Mathf.Max(0f, deltaTime));
            if (recoveryDelay > 0f) return;
            ShotKick = Mathf.MoveTowards(ShotKick, 0f, Mathf.Max(0f, recovery * deltaTime));
            if (ShotKick <= 0.0001f) ShotsInSequence = 0;
        }

        public float Evaluate(WeaponDefinition weapon, bool aiming, bool moving, bool sprinting,
            bool airborne, bool crouching, float attachmentMultiplier = 1f)
        {
            if (weapon == null) return 0f;
            var baseSpread = airborne ? weapon.airborneSpreadDegrees
                : sprinting ? weapon.sprintingSpreadDegrees
                : crouching ? weapon.crouchedSpreadDegrees
                : moving ? weapon.movingSpreadDegrees
                : weapon.baseHipFireSpread > 0f ? weapon.baseHipFireSpread : weapon.standingSpreadDegrees;
            if (aiming) baseSpread *= weapon.aimSpreadMultiplier;
            else if (ShotsInSequence == 0) baseSpread *= weapon.firstShotSpreadMultiplier;
            var spread = baseSpread + ShotKick;
            if (!aiming) spread = Mathf.Min(spread, Mathf.Max(baseSpread, weapon.maxHipFireSpread));
            return Mathf.Max(0f, spread * Mathf.Max(0f, attachmentMultiplier));
        }
    }
}
