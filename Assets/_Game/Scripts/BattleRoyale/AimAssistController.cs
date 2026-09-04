using UnityEngine;

namespace BattleRoyale
{
    public sealed class AimAssistController
    {
        private BRParticipant target;
        private float verticalOverrideUntil;
        private float upperBodyUntil;

        public BRParticipant Target => target;
        public bool Locked => target != null;
        public VerticalAimState VerticalState { get; private set; }
        public bool VerticalOverride => VerticalState == VerticalAimState.VerticalOverride;
        public bool UpperBodyFreeAim => VerticalState == VerticalAimState.UpperBodyFreeAim;
        public float ChestScreenDistance { get; private set; } = float.PositiveInfinity;
        public float HeadScreenDistance { get; private set; } = float.PositiveInfinity;
        public Vector2 HeadScreenOffset { get; private set; }
        public Vector2 FireDragDelta { get; private set; }
        public float CurrentHorizontalStrength { get; private set; }
        public float CurrentVerticalStrength { get; private set; }

        public BRParticipant UpdateTarget(BRParticipant owner, Vector3 origin, Vector3 forward,
            WeaponDefinition weapon, float manualLookMagnitude, bool retainingHipFire = false,
            bool aiming = false)
        {
            if (owner == null || weapon == null || !weapon.AimAssistEnabled)
            {
                target = null;
                return null;
            }
            var profile = weapon.AimProfile(aiming);
            var breakThreshold = weapon.aimAssistBreakThreshold * profile.breakThresholdMultiplier
                * (retainingHipFire ? 2.2f : 1f);
            if (manualLookMagnitude >= breakThreshold)
            {
                target = null;
                return null;
            }
            var acquisitionMultiplier = retainingHipFire ? 1.28f : 1f;
            target = AimAssistTargeting.FindBest(owner.gameObject, origin, forward, target,
                weapon.aimAssistDistance * profile.distanceMultiplier,
                weapon.aimAssistFov * profile.fovMultiplier * acquisitionMultiplier, AimPointPreference.Chest,
                retainingHipFire ? profile.retainedConeMultiplier : 1.45f);
            return target;
        }

        public void UpdateHipFireState(Camera camera, WeaponDefinition weapon, bool firing,
            bool hipFire, Vector2 fireDragDelta, AimInputDevice device, float now)
        {
            FireDragDelta = fireDragDelta;
            UpdateScreenDistances(camera, weapon);
            if (target == null || weapon == null)
            {
                ResetVerticalState();
                UpdateStrengths(weapon, device, !hipFire);
                return;
            }

            if (!hipFire || !firing)
            {
                ResetVerticalState();
                UpdateStrengths(weapon, device, !hipFire);
                return;
            }

            var threshold = device == AimInputDevice.Touch
                ? weapon.dragBreakThresholdTouch : weapon.dragBreakThresholdMouse;
            var upwardIntent = fireDragDelta.y * weapon.verticalDragMultiplier;
            if (upwardIntent >= threshold)
                verticalOverrideUntil = Mathf.Max(verticalOverrideUntil, now + weapon.verticalUnlockDuration);

            var inUpperBodyCorridor = IsInUpperBodyCorridor(camera, weapon);
            if (inUpperBodyCorridor && now >= verticalOverrideUntil
                && VerticalState != VerticalAimState.UpperBodyFreeAim)
                upperBodyUntil = now + weapon.upperBodyFreeAimDuration;

            if (now < verticalOverrideUntil) VerticalState = VerticalAimState.VerticalOverride;
            else if (inUpperBodyCorridor && now < upperBodyUntil) VerticalState = VerticalAimState.UpperBodyFreeAim;
            else VerticalState = VerticalAimState.ChestAcquire;
            UpdateStrengths(weapon, device, !hipFire);
        }

        public Vector2 SensitivityScaleByAxis(WeaponDefinition weapon, AimInputDevice device,
            bool aiming = false)
        {
            if (target == null || weapon == null || !weapon.AimAssistEnabled) return Vector2.one;
            var profile = weapon.AimProfile(aiming);
            var deviceStrength = DeviceMultiplier(weapon, device)
                * weapon.ResolvedAimDefinition.ModeStrength * profile.strengthMultiplier;
            var chestProximity = 1f - Mathf.Clamp01(ChestScreenDistance
                / Mathf.Max(0.001f, weapon.bodyAssistRadiusNormalized));
            var headProximity = HeadZoneProximity(HeadScreenOffset,
                weapon.bodyAssistRadiusNormalized, weapon.headSlowdownRadiusNormalized);
            var chestSensitivity = Mathf.Clamp(weapon.chestSensitivityMultiplier
                * profile.slowdownMultiplier, 0.08f, 1f);
            var horizontal = Mathf.Lerp(1f, chestSensitivity,
                chestProximity * deviceStrength);
            var vertical = VerticalState == VerticalAimState.ChestAcquire
                ? Mathf.Lerp(1f, chestSensitivity, chestProximity * deviceStrength)
                : 1f;
            if (VerticalState != VerticalAimState.VerticalOverride)
            {
                var headSensitivity = Mathf.Min(weapon.headSensitivityMultiplier,
                    profile.headZoneSensitivityMultiplier);
                vertical = Mathf.Lerp(vertical, headSensitivity,
                    headProximity * deviceStrength);
            }
            return new Vector2(Mathf.Clamp(horizontal, 0.08f, 1f), Mathf.Clamp(vertical, 0.08f, 1f));
        }

        public static float HeadZoneProximity(Vector2 headScreenOffset, float bodyRadius,
            float headRadius)
        {
            var horizontalRadius = Mathf.Max(0.01f, bodyRadius * 1.25f);
            var verticalRadius = Mathf.Max(0.01f, headRadius * 1.8f);
            var normalized = new Vector2(headScreenOffset.x / horizontalRadius,
                headScreenOffset.y / verticalRadius);
            return 1f - Mathf.Clamp01(normalized.magnitude);
        }

        public static float HeadZoneSensitivityScale(Vector2 headScreenOffset, float bodyRadius,
            float headRadius, float minimumSensitivity) => Mathf.Lerp(1f,
            Mathf.Clamp(minimumSensitivity, 0.08f, 1f),
            HeadZoneProximity(headScreenOffset, bodyRadius, headRadius));

        public float SensitivityScale(Vector3 origin, Vector3 forward, WeaponDefinition weapon,
            AimInputDevice device, bool aiming = false)
        {
            if (target == null || weapon == null || !weapon.AimAssistEnabled) return 1f;
            var direction = AimAssistTargeting.AimPoint(target, AimPointPreference.Chest) - origin;
            if (direction.sqrMagnitude < 0.001f) return 1f;
            var angle = Vector3.Angle(forward, direction);
            var proximity = 1f - Mathf.Clamp01(angle / Mathf.Max(1f, weapon.aimAssistFov));
            var profile = weapon.AimProfile(aiming);
            var deviceStrength = DeviceMultiplier(weapon, device)
                * weapon.ResolvedAimDefinition.ModeStrength * profile.strengthMultiplier;
            var slowdown = Mathf.Clamp(weapon.aimSlowdownMultiplier * profile.slowdownMultiplier,
                0.08f, 1f);
            return Mathf.Lerp(1f, slowdown, proximity * deviceStrength);
        }

        public Vector2 CorrectAngles(float yaw, float pitch, Vector3 origin, WeaponDefinition weapon,
            AimInputDevice device, bool active, float manualLookMagnitude, float deltaTime,
            float minimumPitch, float maximumPitch, bool aiming = false)
        {
            if (!active || target == null || weapon == null || !weapon.AimAssistEnabled)
                return new Vector2(yaw, pitch);
            var direction = AimAssistTargeting.AimPoint(target, AimPointPreference.Chest) - origin;
            if (direction.sqrMagnitude < 0.001f) return new Vector2(yaw, pitch);
            var desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var normalized = direction.normalized;
            var desiredPitch = -Mathf.Asin(Mathf.Clamp(normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            var inputScale = AimAssistTargeting.MagnetismStrength(manualLookMagnitude);
            var horizontalStep = weapon.aimAssistRotationSpeed * CurrentHorizontalStrength
                * inputScale * Mathf.Max(0f, deltaTime);
            var verticalStep = weapon.aimAssistRotationSpeed * CurrentVerticalStrength
                * inputScale * Mathf.Max(0f, deltaTime);
            return new Vector2(
                Mathf.MoveTowardsAngle(yaw, desiredYaw, horizontalStep),
                Mathf.Clamp(Mathf.MoveTowardsAngle(pitch, desiredPitch, verticalStep), minimumPitch, maximumPitch));
        }

        public void Clear()
        {
            target = null;
            ResetVerticalState();
            CurrentHorizontalStrength = 0f;
            CurrentVerticalStrength = 0f;
        }

        private void UpdateScreenDistances(Camera camera, WeaponDefinition weapon)
        {
            ChestScreenDistance = float.PositiveInfinity;
            HeadScreenDistance = float.PositiveInfinity;
            HeadScreenOffset = Vector2.zero;
            if (camera == null || target == null || weapon == null) return;
            var chest = camera.WorldToViewportPoint(AimAssistTargeting.AimPoint(target, AimPointPreference.Chest));
            var head = camera.WorldToViewportPoint(AimAssistTargeting.AimPoint(target, AimPointPreference.Head));
            if (chest.z <= 0f || head.z <= 0f) return;
            var center = new Vector2(0.5f, 0.5f);
            ChestScreenDistance = Vector2.Distance(center, new Vector2(chest.x, chest.y));
            HeadScreenOffset = new Vector2(head.x, head.y) - center;
            HeadScreenDistance = HeadScreenOffset.magnitude;
        }

        private bool IsInUpperBodyCorridor(Camera camera, WeaponDefinition weapon)
        {
            if (camera == null || target == null || weapon == null) return false;
            var chest = camera.WorldToViewportPoint(AimAssistTargeting.AimPoint(target, AimPointPreference.Chest));
            var head = camera.WorldToViewportPoint(AimAssistTargeting.AimPoint(target, AimPointPreference.Head));
            if (chest.z <= 0f || head.z <= 0f) return false;
            var margin = weapon.headSlowdownRadiusNormalized * 0.7f;
            var lower = Mathf.Min(chest.y, head.y) - margin;
            var upper = Mathf.Max(chest.y, head.y) + margin;
            return 0.5f >= lower && 0.5f <= upper
                && Mathf.Abs(0.5f - chest.x) <= weapon.bodyAssistRadiusNormalized;
        }

        private void UpdateStrengths(WeaponDefinition weapon, AimInputDevice device, bool aiming)
        {
            if (target == null || weapon == null || !weapon.AimAssistEnabled)
            {
                CurrentHorizontalStrength = 0f;
                CurrentVerticalStrength = 0f;
                return;
            }
            var profile = weapon.AimProfile(aiming);
            var common = weapon.aimAssistStrength * DeviceMultiplier(weapon, device)
                * weapon.ResolvedAimDefinition.ModeStrength * profile.strengthMultiplier;
            CurrentHorizontalStrength = common * weapon.horizontalAimAssistStrength
                * profile.horizontalMultiplier;
            CurrentVerticalStrength = VerticalState == VerticalAimState.ChestAcquire
                ? common * weapon.verticalAimAssistStrength * profile.verticalMultiplier : 0f;
        }

        private void ResetVerticalState()
        {
            VerticalState = VerticalAimState.ChestAcquire;
            verticalOverrideUntil = 0f;
            upperBodyUntil = 0f;
            FireDragDelta = Vector2.zero;
        }

        public static float DeviceMultiplier(WeaponDefinition weapon, AimInputDevice device) => device switch
        {
            AimInputDevice.Touch => weapon.touchAimAssistMultiplier,
            AimInputDevice.Gamepad => weapon.gamepadAimAssistMultiplier,
            _ => weapon.mouseAimAssistMultiplier
        };
    }
}
