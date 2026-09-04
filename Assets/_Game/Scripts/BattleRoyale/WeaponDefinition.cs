using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(menuName = "Battle Royale/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public string displayName = "Vesper-9";
        public WeaponClass weaponClass = WeaponClass.Rifle;
        public AmmoKind ammoKind = AmmoKind.Medium;
        public float damage = 23f;
        public float headshotMultiplier = 1.85f;
        public float fireInterval = 0.11f;
        public int magazineSize = 30;
        public float reloadSeconds = 1.7f;
        public float range = 92f;
        [Range(0.1f, 1f)] public float falloffStartRatio = 0.55f;
        [Range(0.1f, 1f)] public float minimumDamageMultiplier = 0.62f;
        public float movingSpreadDegrees = 3.5f;
        public float standingSpreadDegrees = 1.2f;
        public float aimSpreadMultiplier = 0.45f;
        public float recoilPitch = 1.1f;
        public float recoilYaw = 0.45f;
        public bool automatic = true;
        [Header("Fire simulation")]
        public WeaponFireMode fireMode = WeaponFireMode.Legacy;
        public WeaponSimulationType simulationType = WeaponSimulationType.Hitscan;
        [Range(2, 6)] public int burstCount = 3;
        [Min(0.02f)] public float burstInterval = 0.075f;
        [Min(1f)] public float projectileVelocity = 120f;
        public bool projectileUsesGravity;
        [Range(1, 20)] public int pelletCount = 7;

        [Header("Spread")]
        public float sprintingSpreadDegrees = 5.2f;
        public float airborneSpreadDegrees = 6.5f;
        public float crouchedSpreadDegrees = 0.85f;
        public float shotSpreadKick = 0.32f;
        public float maximumSpreadKick = 3f;
        public float spreadRecoverySpeed = 4.5f;
        public float baseHipFireSpread;
        [Range(0.05f, 1f)] public float firstShotSpreadMultiplier = 0.38f;
        public float continuousFireSpreadIncrease = 0.34f;
        public float maxHipFireSpread = 4.8f;

        [Header("Recoil")]
        public float recoilRecoverySpeed = 12f;
        public float maximumRecoil = 4f;
        public float weaponKick = 0.035f;

        [Header("Aim and scope")]
        public ScopeDefinition scope;
        public WeaponAimDefinition aimDefinition = new();
        public AimPointPreference aimPointPreference = AimPointPreference.Chest;
        [Range(0f, 1f)] public float aimAssistStrength = 0.62f;
        [Range(0f, 1f)] public float horizontalAimAssistStrength = 0.70f;
        [Range(0f, 1f)] public float verticalAimAssistStrength = 0.30f;
        [Min(1f)] public float aimAssistDistance = 65f;
        [Range(1f, 35f)] public float aimAssistFov = 14f;
        [Range(0.1f, 1f)] public float aimSlowdownMultiplier = 0.58f;
        [Min(1f)] public float aimAssistRotationSpeed = 92f;
        [Min(0.01f)] public float aimAssistSmoothTime = 0.11f;
        [Min(0.1f)] public float aimAssistBreakThreshold = 4.25f;
        [Min(0.0001f)] public float dragBreakThresholdTouch = 0.0045f;
        [Min(0.0001f)] public float dragBreakThresholdMouse = 0.008f;
        [Min(0.02f)] public float verticalUnlockDuration = 0.28f;
        [Range(0.1f, 3f)] public float verticalDragMultiplier = 1.15f;
        [Min(0.05f)] public float upperBodyFreeAimDuration = 0.75f;
        [Range(0.01f, 0.4f)] public float bodyAssistRadiusNormalized = 0.14f;
        [Range(0.005f, 0.2f)] public float headSlowdownRadiusNormalized = 0.055f;
        [Range(0.1f, 1f)] public float chestSensitivityMultiplier = 0.85f;
        [Range(0.1f, 1f)] public float headSensitivityMultiplier = 0.70f;
        [Range(0f, 1f)] public float mouseAimAssistMultiplier = 0.35f;
        [Range(0f, 1.5f)] public float gamepadAimAssistMultiplier = 0.72f;
        [Range(0f, 2f)] public float touchAimAssistMultiplier = 1f;
        public string modelResource;

        public WeaponFireMode ResolvedFireMode => fireMode == WeaponFireMode.Legacy
            ? automatic ? WeaponFireMode.Automatic : WeaponFireMode.SemiAutomatic
            : fireMode;

        public float AdsFieldOfView => scope != null ? scope.mainCameraAdsFieldOfView : 54f;
        public float AdsSensitivity => scope != null ? scope.sensitivityMultiplier : 0.72f;
        public WeaponAimDefinition ResolvedAimDefinition => aimDefinition ??= new WeaponAimDefinition();
        public AimPresentationMode AimPresentation => ResolvedAimDefinition.presentation;
        public AimAssistMode ResolvedAimAssistMode => ResolvedAimDefinition.aimAssistMode;
        public AimAssistProfile AimProfile(bool aiming) => ResolvedAimDefinition.ProfileFor(aiming);
        public bool AimAssistEnabled => ResolvedAimAssistMode != BattleRoyale.AimAssistMode.None;
    }
}
