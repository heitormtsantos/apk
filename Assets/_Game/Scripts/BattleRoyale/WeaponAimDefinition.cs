using System;
using UnityEngine;

namespace BattleRoyale
{
    [Serializable]
    public sealed class AimAssistProfile
    {
        [Range(0f, 2f)] public float strengthMultiplier = 1f;
        [Range(0f, 2f)] public float horizontalMultiplier = 1f;
        [Range(0f, 2f)] public float verticalMultiplier = 1f;
        [Range(0.1f, 2f)] public float distanceMultiplier = 1f;
        [Range(0.1f, 2f)] public float fovMultiplier = 1f;
        [Range(0.1f, 2f)] public float slowdownMultiplier = 1f;
        [Range(1f, 4f)] public float retainedConeMultiplier = 1.45f;
        [Range(0.25f, 3f)] public float breakThresholdMultiplier = 1f;
        [Range(0.08f, 1f)] public float headZoneSensitivityMultiplier = 0.34f;
    }

    [Serializable]
    public sealed class WeaponAimDefinition
    {
        public AimPresentationMode presentation = AimPresentationMode.FirstPerson;
        public AimAssistMode aimAssistMode = AimAssistMode.Standard;
        public AimAssistProfile hipProfile = new();
        public AimAssistProfile adsProfile = new()
        {
            strengthMultiplier = 1.08f,
            retainedConeMultiplier = 1.65f,
            headZoneSensitivityMultiplier = 0.42f
        };
        [Range(45f, 80f)] public float hipFieldOfView = 65f;
        [Range(35f, 70f)] public float shoulderFieldOfView = 58f;
        public bool hideLocalBodyInAds = true;
        public bool hideLocalWeaponInAds = true;

        public AimAssistProfile ProfileFor(bool aiming) => aiming ? adsProfile : hipProfile;

        public float ModeStrength => aimAssistMode switch
        {
            AimAssistMode.None => 0f,
            AimAssistMode.Strong => 1.22f,
            _ => 1f
        };
    }
}
