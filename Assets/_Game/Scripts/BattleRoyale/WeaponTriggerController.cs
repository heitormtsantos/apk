using UnityEngine;

namespace BattleRoyale
{
    public sealed class WeaponTriggerController
    {
        private WeaponDefinition activeWeapon;
        private int burstRemaining;
        private float nextTriggerTime;

        public int BurstRemaining => burstRemaining;

        public bool ShouldFire(WeaponDefinition weapon, bool held, bool pressed, float now)
        {
            if (weapon == null) return false;
            if (weapon != activeWeapon) Reset(weapon);

            var interval = Mathf.Max(0.01f, weapon.fireInterval);
            switch (weapon.ResolvedFireMode)
            {
                case WeaponFireMode.Automatic:
                    if (!held || now + 0.0001f < nextTriggerTime) return false;
                    nextTriggerTime = now + interval;
                    return true;
                case WeaponFireMode.Burst:
                    if (pressed && burstRemaining == 0)
                    {
                        burstRemaining = Mathf.Max(2, weapon.burstCount);
                        nextTriggerTime = now;
                    }
                    if (burstRemaining <= 0 || now + 0.0001f < nextTriggerTime) return false;
                    burstRemaining--;
                    nextTriggerTime = now + Mathf.Max(0.02f, weapon.burstInterval);
                    return true;
                default:
                    if (!pressed || now + 0.0001f < nextTriggerTime) return false;
                    nextTriggerTime = now + interval;
                    return true;
            }
        }

        public void Reset(WeaponDefinition weapon = null)
        {
            activeWeapon = weapon;
            burstRemaining = 0;
            nextTriggerTime = 0f;
        }
    }
}
