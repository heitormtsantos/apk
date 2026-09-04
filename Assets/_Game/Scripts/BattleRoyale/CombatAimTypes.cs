using UnityEngine;

namespace BattleRoyale
{
    public enum WeaponFireMode
    {
        Legacy,
        SemiAutomatic,
        Automatic,
        Burst
    }

    public enum WeaponSimulationType
    {
        Hitscan,
        Projectile
    }

    public enum ScopeType
    {
        None,
        IronSight,
        RedDot,
        Holographic,
        Scope2X,
        Scope4X,
        Sniper
    }

    public enum AimPointPreference
    {
        Chest,
        Pelvis,
        Head
    }

    public enum AimInputDevice
    {
        Mouse,
        Gamepad,
        Touch
    }

    public enum VerticalAimState
    {
        ChestAcquire,
        VerticalOverride,
        UpperBodyFreeAim
    }

    public enum AimPresentationMode
    {
        HipFire,
        SoftShoulder,
        FirstPerson
    }

    public enum AimAssistMode
    {
        None,
        Standard,
        Strong,
        Custom
    }

    public enum ScopeRenderMode
    {
        SingleCameraOverlay,
        RenderTextureLens
    }

    public readonly struct AimSolution
    {
        public AimSolution(Ray cameraRay, Ray muzzleRay, Vector3 aimPoint, Vector3 endPoint,
            float distance, BRParticipant assistedTarget, bool muzzleObstructed)
        {
            CameraRay = cameraRay;
            MuzzleRay = muzzleRay;
            AimPoint = aimPoint;
            EndPoint = endPoint;
            Distance = Mathf.Max(0f, distance);
            AssistedTarget = assistedTarget;
            MuzzleObstructed = muzzleObstructed;
        }

        public Ray CameraRay { get; }
        public Ray MuzzleRay { get; }
        public Vector3 AimPoint { get; }
        public Vector3 EndPoint { get; }
        public float Distance { get; }
        public BRParticipant AssistedTarget { get; }
        public bool MuzzleObstructed { get; }
        public bool IsFinite => IsFiniteVector(AimPoint) && IsFiniteVector(EndPoint)
            && IsFiniteVector(CameraRay.origin) && IsFiniteVector(CameraRay.direction)
            && IsFiniteVector(MuzzleRay.origin) && IsFiniteVector(MuzzleRay.direction);

        private static bool IsFiniteVector(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
