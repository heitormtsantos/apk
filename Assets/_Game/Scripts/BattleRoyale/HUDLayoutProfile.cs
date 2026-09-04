using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public enum HUDElementId
    {
        Vitals,
        MatchStatus,
        Compass,
        Minimap,
        Weapon,
        LootPrompt,
        PickupFeedback,
        Pause,
        Move,
        Fire,
        Aim,
        Jump,
        Interact,
        Reload,
        Heal,
        Swap
    }

    [Serializable]
    public sealed class HUDLayoutEntry
    {
        public HUDElementId id;
        public Rect normalizedRect;
        [Range(0.2f, 1f)] public float opacity = 1f;
        public bool visible = true;

        public HUDLayoutEntry Clone() => new()
        {
            id = id,
            normalizedRect = normalizedRect,
            opacity = opacity,
            visible = visible
        };
    }

    [Serializable]
    public sealed class HUDLayoutProfile
    {
        public const int CurrentVersion = 2;
        private const string DesktopKey = "RavenDrop.HUD.Desktop.v1";
        private const string MobileKey = "RavenDrop.HUD.Mobile.v1";
        private static readonly HUDElementId[] ElementIds =
            (HUDElementId[])Enum.GetValues(typeof(HUDElementId));

        public int version = CurrentVersion;
        public bool mobile;
        public List<HUDLayoutEntry> elements = new();

        public static HUDLayoutProfile Load(bool mobile, Rect safeArea, Func<HUDElementId, Rect> defaults)
        {
            HUDLayoutProfile profile = null;
            var json = PlayerPrefs.GetString(mobile ? MobileKey : DesktopKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { profile = JsonUtility.FromJson<HUDLayoutProfile>(json); }
                catch (Exception) { profile = null; }
            }
            profile ??= new HUDLayoutProfile { mobile = mobile };
            profile.mobile = mobile;
            var migrated = profile.Migrate(safeArea, defaults);
            profile.Repair(safeArea, defaults);
            if (migrated) profile.Save();
            return profile;
        }

        public void Save()
        {
            version = CurrentVersion;
            PlayerPrefs.SetString(mobile ? MobileKey : DesktopKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public HUDLayoutProfile Clone()
        {
            var copy = new HUDLayoutProfile { version = version, mobile = mobile };
            foreach (var entry in elements) copy.elements.Add(entry.Clone());
            return copy;
        }

        public HUDLayoutEntry Get(HUDElementId id)
        {
            for (var i = 0; i < elements.Count; i++)
                if (elements[i] != null && elements[i].id == id) return elements[i];
            return null;
        }

        public Rect Resolve(HUDElementId id, Rect safeArea, Rect fallback)
        {
            var entry = Get(id);
            return entry == null ? ClampToSafeArea(fallback, safeArea) : DenormalizeFor(id, entry.normalizedRect, safeArea);
        }

        public void SetRect(HUDElementId id, Rect rect, Rect safeArea)
        {
            var entry = Get(id);
            if (entry == null) return;
            entry.normalizedRect = NormalizeFor(id, ClampToSafeArea(rect, safeArea), safeArea);
        }

        public void ResetElement(HUDElementId id, Rect safeArea, Rect fallback)
        {
            var entry = Get(id);
            if (entry == null) return;
            entry.normalizedRect = NormalizeFor(id, ClampToSafeArea(fallback, safeArea), safeArea);
            entry.opacity = 1f;
            entry.visible = true;
        }

        public void ResetAll(Rect safeArea, Func<HUDElementId, Rect> defaults)
        {
            elements.Clear();
            Repair(safeArea, defaults);
        }

        public void Repair(Rect safeArea, Func<HUDElementId, Rect> defaults)
        {
            elements ??= new List<HUDLayoutEntry>();
            foreach (var id in ElementIds)
            {
                if (!mobile && IsMobileControl(id)) continue;
                var entry = Get(id);
                if (entry == null)
                {
                    entry = new HUDLayoutEntry { id = id };
                    elements.Add(entry);
                    entry.normalizedRect = NormalizeFor(id, ClampToSafeArea(defaults(id), safeArea), safeArea);
                }
                if (!IsFinite(entry.normalizedRect) || entry.normalizedRect.width <= 0f || entry.normalizedRect.height <= 0f)
                    entry.normalizedRect = NormalizeFor(id, ClampToSafeArea(defaults(id), safeArea), safeArea);
                entry.opacity = float.IsFinite(entry.opacity) ? Mathf.Clamp(entry.opacity, 0.2f, 1f) : 1f;
                var resolved = ClampToSafeArea(DenormalizeFor(id, entry.normalizedRect, safeArea), safeArea);
                entry.normalizedRect = NormalizeFor(id, resolved, safeArea);
            }
            version = CurrentVersion;
        }

        public static bool IsMobileControl(HUDElementId id) => id is HUDElementId.Move or HUDElementId.Fire
            or HUDElementId.Aim or HUDElementId.Jump or HUDElementId.Interact or HUDElementId.Reload
            or HUDElementId.Heal or HUDElementId.Swap;

        public static Rect Normalize(Rect rect, Rect safeArea)
        {
            var width = Mathf.Max(1f, safeArea.width);
            var height = Mathf.Max(1f, safeArea.height);
            return new Rect((rect.x - safeArea.x) / width, (rect.y - safeArea.y) / height,
                rect.width / width, rect.height / height);
        }

        public static Rect Denormalize(Rect rect, Rect safeArea) => new(
            safeArea.x + rect.x * safeArea.width,
            safeArea.y + rect.y * safeArea.height,
            rect.width * safeArea.width,
            rect.height * safeArea.height);

        public static Rect NormalizeMobileControl(Rect rect, Rect safeArea)
        {
            var width = Mathf.Max(1f, safeArea.width);
            var height = Mathf.Max(1f, safeArea.height);
            var sizeScale = Mathf.Max(1f, Mathf.Min(width, height));
            return new Rect((rect.x - safeArea.x) / width, (rect.y - safeArea.y) / height,
                rect.width / sizeScale, rect.height / sizeScale);
        }

        public static Rect DenormalizeMobileControl(Rect rect, Rect safeArea)
        {
            var sizeScale = Mathf.Max(1f, Mathf.Min(safeArea.width, safeArea.height));
            return new Rect(safeArea.x + rect.x * safeArea.width, safeArea.y + rect.y * safeArea.height,
                rect.width * sizeScale, rect.height * sizeScale);
        }

        public static Rect ClampToSafeArea(Rect rect, Rect safeArea)
        {
            var minWidth = Mathf.Min(34f, safeArea.width);
            var minHeight = Mathf.Min(24f, safeArea.height);
            rect.width = Mathf.Clamp(rect.width, minWidth, Mathf.Max(minWidth, safeArea.width));
            rect.height = Mathf.Clamp(rect.height, minHeight, Mathf.Max(minHeight, safeArea.height));
            rect.x = Mathf.Clamp(rect.x, safeArea.xMin, safeArea.xMax - rect.width);
            rect.y = Mathf.Clamp(rect.y, safeArea.yMin, safeArea.yMax - rect.height);
            return rect;
        }

        public static float UniformScale(Rect rect, Rect reference)
        {
            if (reference.width <= 0f || reference.height <= 0f) return 1f;
            return Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.01f,
                rect.width * rect.height / (reference.width * reference.height))), 0.5f, 2f);
        }

        public static Rect ScaleAroundCenter(Rect rect, Rect reference, float scale)
        {
            var center = rect.center;
            var width = Mathf.Max(1f, reference.width * Mathf.Clamp(scale, 0.5f, 2f));
            var height = Mathf.Max(1f, reference.height * Mathf.Clamp(scale, 0.5f, 2f));
            return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        }

        private static bool IsFinite(Rect rect) => float.IsFinite(rect.x) && float.IsFinite(rect.y)
            && float.IsFinite(rect.width) && float.IsFinite(rect.height);

        private Rect NormalizeFor(HUDElementId id, Rect rect, Rect safeArea) =>
            mobile && IsMobileControl(id) ? NormalizeMobileControl(rect, safeArea) : Normalize(rect, safeArea);

        private Rect DenormalizeFor(HUDElementId id, Rect rect, Rect safeArea) =>
            mobile && IsMobileControl(id) ? DenormalizeMobileControl(rect, safeArea) : Denormalize(rect, safeArea);

        private bool Migrate(Rect safeArea, Func<HUDElementId, Rect> defaults)
        {
            if (version >= CurrentVersion) return false;
            if (mobile && elements != null)
            {
                foreach (var entry in elements)
                {
                    if (entry == null || !IsMobileControl(entry.id)) continue;
                    var defaultRect = ClampToSafeArea(defaults(entry.id), safeArea);
                    entry.normalizedRect = NormalizeMobileControl(defaultRect, safeArea);
                }
            }
            version = CurrentVersion;
            return true;
        }
    }
}
