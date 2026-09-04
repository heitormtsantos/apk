using UnityEngine;

namespace BattleRoyale
{
    public sealed class AimStateController
    {
        public float Progress { get; private set; }
        public ScopeType Mode { get; private set; }
        public AimPresentationMode PresentationMode { get; private set; }

        public float Tick(bool aiming, WeaponDefinition weapon, float deltaTime)
        {
            Mode = weapon != null && weapon.scope != null ? weapon.scope.scopeType : ScopeType.IronSight;
            var speed = weapon != null && weapon.scope != null ? weapon.scope.transitionSpeed : 10f;
            Progress = Mathf.MoveTowards(Progress, aiming ? 1f : 0f,
                Mathf.Max(0f, speed * deltaTime));
            PresentationMode = Progress > 0f ? PresentationFor(weapon) : AimPresentationMode.HipFire;
            return Progress;
        }

        public void Reset()
        {
            Progress = 0f;
            Mode = ScopeType.None;
            PresentationMode = AimPresentationMode.HipFire;
        }

        public static AimPresentationMode PresentationFor(WeaponDefinition weapon)
        {
            if (weapon == null) return AimPresentationMode.HipFire;
            return weapon.AimPresentation;
        }
    }
}
