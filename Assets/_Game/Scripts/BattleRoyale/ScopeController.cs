using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BattleRoyale
{
    [RequireComponent(typeof(Camera))]
    public sealed class ScopeController : MonoBehaviour
    {
        private ScopeDefinition definition;
        private float aimProgress;
        private Canvas overlayCanvas;
        private CanvasGroup overlayGroup;
        private Image overlayImage;
        private ScopeType loadedType = ScopeType.IronSight;

        public bool ScopeActive => definition != null && HasOptic(definition.scopeType) && aimProgress >= 0.55f;
        public bool UsingRenderTexture => false;
        public RenderTexture ScopeTexture => null;
        public float AimProgress => aimProgress;
        public bool CanvasOverlayReady => overlayCanvas != null && overlayImage != null
            && overlayImage.sprite != null && overlayCanvas.enabled;
        public Sprite ActiveOverlay => overlayImage != null ? overlayImage.sprite : null;
        public static bool RenderTexturesAvailable =>
            CanUseRenderTexture(SystemInfo.supportsRenderTextures, SystemInfo.graphicsDeviceType);

        public static bool CanUseRenderTexture(bool supported, GraphicsDeviceType deviceType) =>
            supported && deviceType != GraphicsDeviceType.Null;

        public void Tick(ScopeDefinition nextDefinition, float progress)
        {
            definition = nextDefinition;
            aimProgress = Mathf.Clamp01(progress);
            if (!ScopeActive)
            {
                if (overlayCanvas != null) overlayCanvas.enabled = false;
                return;
            }

            EnsureCanvas();
            ApplyOverlay(definition.scopeType);
            overlayCanvas.enabled = true;
            overlayGroup.alpha = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.55f, 0.86f, aimProgress));
            var size = Mathf.Min(Screen.height * OverlayHeightFraction(definition.scopeType), Screen.width * 0.96f);
            overlayImage.rectTransform.sizeDelta = new Vector2(size, size);
        }

        private void EnsureCanvas()
        {
            if (overlayCanvas != null) return;
            var root = new GameObject("Weapon Scope Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(CanvasGroup));
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.SetParent(transform, false);
            overlayCanvas = root.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = -100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            overlayGroup = root.GetComponent<CanvasGroup>();
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = false;

            var imageObject = new GameObject("Scope PNG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.hideFlags = HideFlags.HideAndDontSave;
            imageObject.transform.SetParent(root.transform, false);
            overlayImage = imageObject.GetComponent<Image>();
            overlayImage.raycastTarget = false;
            overlayImage.preserveAspect = true;
            var rect = overlayImage.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private void ApplyOverlay(ScopeType type)
        {
            if (loadedType == type && overlayImage.sprite != null) return;
            loadedType = type;
            overlayImage.sprite = Resources.Load<Sprite>(OverlayResourcePath(type));
            overlayImage.color = Color.white;
        }

        public static string OverlayResourcePath(ScopeType type) => type switch
        {
            ScopeType.RedDot or ScopeType.Holographic => "UI/ScopeOverlays/red-dot",
            ScopeType.Scope2X => "UI/ScopeOverlays/scope-2x",
            ScopeType.Scope4X => "UI/ScopeOverlays/scope-4x",
            ScopeType.Sniper => "UI/ScopeOverlays/sniper",
            _ => string.Empty
        };

        public static float MainCameraAdsFieldOfView(ScopeType type) => type switch
        {
            ScopeType.Sniper => 17f,
            ScopeType.Scope4X => 28f,
            ScopeType.Scope2X => 40f,
            ScopeType.Holographic => 50f,
            ScopeType.RedDot => 52f,
            _ => 58f
        };

        public static float OverlayHeightFraction(ScopeType type) => type switch
        {
            ScopeType.Sniper => 0.90f,
            ScopeType.Scope4X => 0.76f,
            ScopeType.Scope2X => 0.60f,
            ScopeType.Holographic => 0.46f,
            ScopeType.RedDot => 0.42f,
            _ => 0f
        };

        public static float LensHeightFraction(ScopeType type) => type switch
        {
            ScopeType.Sniper => 0.82f,
            ScopeType.Scope4X => 0.66f,
            ScopeType.Scope2X => 0.50f,
            ScopeType.Holographic => 0.34f,
            ScopeType.RedDot => 0.30f,
            _ => 0.24f
        };

        public static bool UsesOutsideDarkening(ScopeType type) => false;
        public static bool HasOptic(ScopeType type) => type is ScopeType.RedDot or ScopeType.Holographic
            or ScopeType.Scope2X or ScopeType.Scope4X or ScopeType.Sniper;

        private void OnDestroy()
        {
            if (overlayCanvas != null) Destroy(overlayCanvas.gameObject);
        }
    }
}
