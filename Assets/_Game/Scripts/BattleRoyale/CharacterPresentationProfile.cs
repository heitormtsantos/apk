using UnityEngine;

namespace BattleRoyale
{
    public static class CharacterPresentationProfile
    {
        public const float VisualHeight = 0.84f;
        public const float AnimatedNormalizationHeight = 0.87f;
        public const float ControllerHeight = 0.90f;
        public const float ControllerRadius = 0.18f;
        public const float ControllerCenterY = 0.45f;
        public const float StepOffset = 0.17f;
        public const float HeadHeight = 0.75f;
        public const float HeadRadius = 0.105f;
        public const float CameraPivotHeight = 0.78f;
        public const float TraversalCameraPivotHeight = 0.92f;
        public const float MuzzleHeight = 0.68f;
        public const float MuzzleForward = 0.30f;

        public static Vector3 CameraOffset(bool aiming, bool indoors)
        {
            if (indoors)
                return aiming ? new Vector3(0.30f, 0.14f, -1.04f) : new Vector3(0.25f, 0.18f, -1.34f);

            return aiming ? new Vector3(0.34f, 0.16f, -1.30f) : new Vector3(0.34f, 0.18f, -1.42f);
        }

        public static void Apply(CharacterController controller)
        {
            if (controller == null) return;

            controller.radius = ControllerRadius;
            controller.height = ControllerHeight;
            controller.center = Vector3.up * ControllerCenterY;
            controller.stepOffset = StepOffset;
        }

        public static Vector3 AimPoint(Transform root)
        {
            return root != null ? root.position + Vector3.up * HeadHeight : Vector3.zero;
        }

        public static Vector3 FallbackMuzzle(Transform root)
        {
            return root != null
                ? root.position + Vector3.up * MuzzleHeight + root.forward * MuzzleForward
                : Vector3.zero;
        }

        public static float WeaponLength(WeaponClass weaponClass)
        {
            return weaponClass == WeaponClass.Pistol ? 0.23f : 0.42f;
        }

        public static float OpeningOccupancy(float openingHeight) =>
            openingHeight > 0f ? VisualHeight / openingHeight : float.PositiveInfinity;

        public static float CameraPivot(bool traversing) =>
            traversing ? TraversalCameraPivotHeight : CameraPivotHeight;
    }
}
