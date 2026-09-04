using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(fileName = "SafeZoneConfig", menuName = "Battle Royale/Safe Zone Config")]
    public sealed class SafeZoneConfig : ScriptableObject
    {
        [Header("Initial Zone")]
        [Min(1f)] public float initialRadius = 86f;
        public Vector3 initialCenter;
        public bool useCustomInitialCenter;
        [Min(0f)] public float startDelay;

        [Header("Damage")]
        [Min(0.05f)] public float damageTickInterval = 0.5f;
        [Min(0f)] public float boundaryTolerance = 0.1f;

        [Header("Generation")]
        public bool generateRandomNextZone = true;
        public bool keepNextZoneFullyInsideCurrent = true;
        [Min(0f)] public float minimumFinalRadius = 2f;
        public LayerMask playableGroundLayers = ~0;
        public bool allowZoneOverWater = true;
        public bool validateNextZoneGround;
        [Min(1)] public int zoneGenerationAttempts = 24;
        public SafeZoneFinalBehavior finalBehavior = SafeZoneFinalBehavior.Finish;

        [Header("Phases")]
        public List<SafeZonePhaseData> phases = new();

        [Header("World Visual")]
        public GameObject worldVisualPrefab;
        public Material zoneMaterial;
        [Min(3f)] public float wallHeight = 260f;
        public float wallBottomOffset = -40f;
        [Range(16, 128)] public int wallSegments = 64;
        public Color wallColor = new(0.05f, 0.72f, 1f, 0.28f);
        [Min(0f)] public float pulseIntensity = 0.16f;
        [Min(0f)] public float scrollSpeed = 0.2f;

        [Header("Audio")]
        public AudioClip warningSound;
        public AudioClip shrinkingStartSound;
        public AudioClip outsideZoneLoopSound;
        public AudioClip enteredSafeZoneSound;
        public AudioClip exitedSafeZoneSound;

        [Header("Debug")]
        public bool enableDebug;
        public bool showDebugGizmos = true;
        [Range(0.1f, 20f)] public float debugTimeMultiplier = 1f;

        public void Normalize()
        {
            initialRadius = Mathf.Max(1f, initialRadius);
            damageTickInterval = Mathf.Clamp(damageTickInterval, 0.05f, 10f);
            boundaryTolerance = Mathf.Clamp(boundaryTolerance, 0f, 2f);
            minimumFinalRadius = Mathf.Max(0f, minimumFinalRadius);
            zoneGenerationAttempts = Mathf.Clamp(zoneGenerationAttempts, 1, 256);
            wallHeight = Mathf.Max(3f, wallHeight);
            wallSegments = Mathf.Clamp(wallSegments, 16, 128);
            debugTimeMultiplier = Mathf.Clamp(debugTimeMultiplier, 0.1f, 20f);
            phases ??= new List<SafeZonePhaseData>();
            var maximum = initialRadius;
            for (var i = 0; i < phases.Count; i++)
            {
                phases[i] ??= new SafeZonePhaseData();
                phases[i].Normalize(maximum);
                maximum = phases[i].targetRadius;
            }
        }

        public void AdaptToPlayableArea(Bounds area)
        {
            Normalize();
            var targetInitialRadius = Mathf.Max(1f, Mathf.Min(area.size.x, area.size.z) * 0.48f);
            var scale = targetInitialRadius / Mathf.Max(1f, initialRadius);
            initialRadius = targetInitialRadius;
            initialCenter = new Vector3(area.center.x, 0f, area.center.z);
            useCustomInitialCenter = true;
            minimumFinalRadius = Mathf.Max(2f, minimumFinalRadius * scale);
            wallHeight = Mathf.Max(wallHeight, area.size.y + 120f);
            wallBottomOffset = Mathf.Min(wallBottomOffset, area.min.y - 40f);
            allowZoneOverWater = false;
            validateNextZoneGround = true;
            zoneGenerationAttempts = Mathf.Max(zoneGenerationAttempts, 96);
            foreach (var phase in phases)
                if (phase != null) phase.targetRadius *= scale;
            Normalize();
        }

        public static SafeZoneConfig FromLegacy(BRGameConfig legacy)
        {
            var result = CreateInstance<SafeZoneConfig>();
            result.hideFlags = HideFlags.DontSave;
            var size = legacy != null ? legacy.mapSize : new Vector2(180f, 180f);
            result.initialRadius = Mathf.Min(size.x, size.y) * 0.48f;
            result.initialCenter = PlayableArea.Center;
            result.useCustomInitialCenter = true;
            result.phases.Clear();
            var source = legacy?.safeZonePhases;
            if (source != null)
            {
                for (var i = 0; i < source.Length; i++)
                {
                    var phase = source[i];
                    if (phase == null) continue;
                    result.phases.Add(new SafeZonePhaseData($"Phase {i + 1}", phase.waitSeconds,
                        phase.shrinkSeconds, phase.radius, phase.damagePerSecond));
                }
            }
            result.Normalize();
            return result;
        }
    }
}
