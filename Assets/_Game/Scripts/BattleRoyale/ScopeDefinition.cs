using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(menuName = "Battle Royale/Scope Definition")]
    public sealed class ScopeDefinition : ScriptableObject
    {
        public ScopeType scopeType = ScopeType.IronSight;
        [Min(1f)] public float magnification = 1f;
        [Range(12f, 70f)] public float scopeFieldOfView = 45f;
        [Range(12f, 70f)] public float mainCameraAdsFieldOfView = 54f;
        [Range(0.1f, 1f)] public float sensitivityMultiplier = 0.72f;
        [Min(1f)] public float transitionSpeed = 10f;
        [Range(0f, 2f)] public float aimAssistStrengthMultiplier = 1f;
        [Range(0.25f, 2f)] public float aimAssistDistanceMultiplier = 1f;
        [Range(0.25f, 2f)] public float aimAssistFovMultiplier = 1f;
        public bool useRenderTexture;
        public ScopeRenderMode renderMode = ScopeRenderMode.SingleCameraOverlay;
        public bool useFullScreenOverlay;
        [Range(128, 1024)] public int lowQualityResolution = 256;
        [Range(128, 1024)] public int highQualityResolution = 512;
        public Sprite reticle;
        public Sprite scopeMask;
        public Material lensMaterial;
        public Vector3 cameraOffset = new(0.43f, 0.12f, -2.05f);
        public Vector3 weaponPositionOffset;
        public Vector3 weaponRotationOffset;

        public bool IsMagnified => scopeType is ScopeType.Scope2X or ScopeType.Scope4X or ScopeType.Sniper;
        public bool UsesRenderTexture => renderMode == ScopeRenderMode.RenderTextureLens || useRenderTexture;
    }
}
