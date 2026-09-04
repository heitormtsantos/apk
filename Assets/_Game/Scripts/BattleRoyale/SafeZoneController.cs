using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public readonly struct SafeZoneGeometry
    {
        public SafeZoneGeometry(Vector3 center, float radius)
        {
            Center = center;
            Radius = Mathf.Max(0f, radius);
        }

        public Vector3 Center { get; }
        public float Radius { get; }
    }

    public sealed class SafeZoneController : MonoBehaviour
    {
        [SerializeField] private SafeZoneConfig config;
        [SerializeField] private int matchSeed = 9142;

        private SafeZoneConfig runtimeLegacyConfig;
        private SafeZoneDamageSystem damageSystem;
        private SafeZoneWorldVisual worldVisual;
        private float stateTimer;
        private float stateDuration;
        private double stateStartTime;
        private bool warningEmitted;

        public event Action<SafeZoneState> OnStateChanged;
        public event Action<int> OnPhaseChanged;
        public event Action OnWarningStarted;
        public event Action OnFinalZoneReached;
        public event Action OnZoneUpdated;

        public Vector3 CurrentCenter { get; private set; }
        public float CurrentRadius { get; private set; }
        public Vector3 NextCenter { get; private set; }
        public float NextRadius { get; private set; }
        public Vector3 ShrinkStartCenter { get; private set; }
        public float ShrinkStartRadius { get; private set; }
        public int CurrentPhaseIndex { get; private set; }
        public SafeZoneState CurrentState { get; private set; } = SafeZoneState.Inactive;
        public float StateTimeRemaining => Mathf.Max(0f, stateTimer);
        public float StateProgress => stateDuration <= 0f ? 1f : Mathf.Clamp01(1f - stateTimer / stateDuration);
        public bool HasNextZone { get; private set; }
        public SafeZonePhaseData CurrentPhase => config != null && config.phases != null
            && CurrentPhaseIndex >= 0 && CurrentPhaseIndex < config.phases.Count
                ? config.phases[CurrentPhaseIndex]
                : null;

        // Compatibility with the original prototype API.
        public int PhaseIndex => CurrentPhaseIndex;
        public float TimeRemaining => StateTimeRemaining;
        public bool Shrinking => CurrentState == SafeZoneState.Shrinking;
        public float DamagePerSecond => GetCurrentDamagePerSecond();
        public SafeZoneConfig Config => config;

        public void Configure(BRGameConfig legacyConfig)
        {
            if (legacyConfig == null)
            {
                Debug.LogError("[SafeZoneController] BRGameConfig is missing.", this);
                return;
            }
            legacyConfig.Normalize();
            if (runtimeLegacyConfig != null) Destroy(runtimeLegacyConfig);
            runtimeLegacyConfig = SafeZoneConfig.FromLegacy(legacyConfig);
            Configure(runtimeLegacyConfig, legacyConfig.lootSeed, true);
        }

        public void Configure(SafeZoneConfig settings, int seed, bool startImmediately = false)
        {
            if (settings == null)
            {
                Debug.LogError("[SafeZoneController] SafeZoneConfig is not configured.", this);
                return;
            }
            config = settings;
            config.Normalize();
            matchSeed = seed;
            damageSystem ??= GetComponent<SafeZoneDamageSystem>() ?? gameObject.AddComponent<SafeZoneDamageSystem>();
            EnsureWorldVisual();
            worldVisual.Configure(this, config);
            StopSafeZone();
            if (startImmediately) StartSafeZone();
        }

        [ContextMenu("Start Safe Zone")]
        public void StartSafeZone()
        {
            if (!ValidateConfig()) return;
            var initialCenter = config.useCustomInitialCenter ? config.initialCenter : PlayableArea.Center;
            initialCenter.y = 0f;
            CurrentCenter = initialCenter;
            CurrentRadius = Mathf.Max(config.minimumFinalRadius, config.initialRadius);
            ShrinkStartCenter = CurrentCenter;
            ShrinkStartRadius = CurrentRadius;
            CurrentPhaseIndex = 0;
            damageSystem.ResetDamageClock();
            GenerateNextZone();
            StartWaitingPhase(config.startDelay + CurrentPhase.waitingDuration);
            OnPhaseChanged?.Invoke(CurrentPhaseIndex);
            RefreshVisuals();
        }

        public void StopSafeZone()
        {
            HasNextZone = false;
            stateTimer = 0f;
            stateDuration = 0f;
            warningEmitted = false;
            SetState(SafeZoneState.Inactive);
            damageSystem?.ResetDamageClock();
            RefreshVisuals();
        }

        public void Tick(IEnumerable<BRParticipant> participants) => Tick(participants, Time.deltaTime, null);

        public void Tick(IEnumerable<BRParticipant> participants, float deltaTime, Func<bool> shouldContinue)
        {
            if (CurrentState == SafeZoneState.Inactive || config == null) return;
            var scaledDelta = Mathf.Max(0f, deltaTime) * (config.enableDebug ? config.debugTimeMultiplier : 1f);
            AdvanceClock(scaledDelta);
            damageSystem.Tick(participants, this, scaledDelta, config.damageTickInterval, shouldContinue);
            RefreshVisuals();
        }

        private void AdvanceClock(float deltaTime)
        {
            if (CurrentState == SafeZoneState.Finished || deltaTime <= 0f) return;
            var remainingDelta = deltaTime;
            var guard = 0;
            while (remainingDelta > 0f && CurrentState is SafeZoneState.Waiting or SafeZoneState.Shrinking && guard++ < 16)
            {
                var consumed = Mathf.Min(remainingDelta, stateTimer);
                stateTimer = Mathf.Max(0f, stateTimer - consumed);
                remainingDelta -= consumed;
                if (CurrentState == SafeZoneState.Shrinking) UpdateShrinkGeometry();
                CheckWarning();
                if (stateTimer > 0f) break;
                if (CurrentState == SafeZoneState.Waiting) StartShrinkingPhase();
                else FinishCurrentPhase();
            }
        }

        public void AdvancePhase()
        {
            if (CurrentState == SafeZoneState.Waiting) StartShrinkingPhase();
            else if (CurrentState == SafeZoneState.Shrinking) FinishCurrentPhase();
        }

        public void StartWaitingPhase() => StartWaitingPhase(CurrentPhase?.waitingDuration ?? 0f);

        private void StartWaitingPhase(float duration)
        {
            BeginState(SafeZoneState.Waiting, duration);
            CheckWarning();
        }

        public void StartShrinkingPhase()
        {
            if (!HasNextZone || CurrentPhase == null)
            {
                FinishSystem();
                return;
            }
            ShrinkStartCenter = CurrentCenter;
            ShrinkStartRadius = CurrentRadius;
            BeginState(SafeZoneState.Shrinking, CurrentPhase.shrinkingDuration);
        }

        public void FinishCurrentPhase()
        {
            if (HasNextZone)
            {
                CurrentCenter = NextCenter;
                CurrentRadius = Mathf.Max(0f, NextRadius);
            }
            if (CurrentPhaseIndex >= config.phases.Count - 1)
            {
                FinishSystem();
                return;
            }
            CurrentPhaseIndex++;
            OnPhaseChanged?.Invoke(CurrentPhaseIndex);
            GenerateNextZone();
            StartWaitingPhase();
        }

        [ContextMenu("Generate Next Zone")]
        public void GenerateNextZone()
        {
            var phase = CurrentPhase;
            if (phase == null)
            {
                HasNextZone = false;
                return;
            }
            var targetRadius = Mathf.Clamp(phase.targetRadius, config.minimumFinalRadius, CurrentRadius);
            if (!config.generateRandomNextZone || !phase.generateNewCenter)
            {
                NextCenter = CurrentCenter;
                NextRadius = targetRadius;
                HasNextZone = true;
                return;
            }
            var request = new SafeZoneGenerationRequest(CurrentCenter, CurrentRadius, targetRadius,
                config.keepNextZoneFullyInsideCurrent ? phase.maxCenterOffsetMultiplier : 1f,
                unchecked(matchSeed + CurrentPhaseIndex * 104729), config.zoneGenerationAttempts,
                PlayableArea.Bounds, true);
            var geometry = SafeZoneGenerator.GenerateNextZone(request,
                config.validateNextZoneGround ? IsValidGroundCenter : null);
            NextCenter = geometry.Center;
            NextRadius = geometry.Radius;
            HasNextZone = true;
        }

        public bool IsInsideSafeZone(Vector3 worldPosition)
        {
            var delta = worldPosition - CurrentCenter;
            delta.y = 0f;
            var radius = Mathf.Max(0f, CurrentRadius) + (config != null ? config.boundaryTolerance : 0.1f);
            return delta.sqrMagnitude <= radius * radius;
        }

        public float GetDistanceToSafeZone(Vector3 worldPosition)
        {
            var delta = worldPosition - CurrentCenter;
            delta.y = 0f;
            return Mathf.Max(0f, delta.magnitude - CurrentRadius);
        }

        public Vector3 GetDirectionToSafeZone(Vector3 worldPosition)
        {
            if (IsInsideSafeZone(worldPosition)) return Vector3.zero;
            var direction = CurrentCenter - worldPosition;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }

        public float GetCurrentDamagePerSecond() => CurrentPhase?.damagePerSecond ?? 0f;

        public SafeZoneSnapshot CreateSnapshot()
        {
            return new SafeZoneSnapshot
            {
                phaseIndex = CurrentPhaseIndex,
                state = CurrentState,
                startCenter = ShrinkStartCenter,
                startRadius = ShrinkStartRadius,
                currentCenter = CurrentCenter,
                currentRadius = CurrentRadius,
                targetCenter = NextCenter,
                targetRadius = NextRadius,
                stateStartTime = stateStartTime,
                stateDuration = stateDuration,
                damagePerSecond = GetCurrentDamagePerSecond(),
                hasNextZone = HasNextZone
            };
        }

        public void ApplySnapshot(SafeZoneSnapshot snapshot, double synchronizedTime)
        {
            if (!ValidateConfig()) return;
            CurrentPhaseIndex = Mathf.Clamp(snapshot.phaseIndex, 0, config.phases.Count - 1);
            ShrinkStartCenter = snapshot.startCenter;
            ShrinkStartRadius = Mathf.Max(0f, snapshot.startRadius);
            CurrentCenter = snapshot.currentCenter;
            CurrentRadius = Mathf.Max(0f, snapshot.currentRadius);
            NextCenter = snapshot.targetCenter;
            NextRadius = Mathf.Max(0f, snapshot.targetRadius);
            HasNextZone = snapshot.hasNextZone;
            stateStartTime = snapshot.stateStartTime;
            stateDuration = Mathf.Max(0f, snapshot.stateDuration);
            stateTimer = Mathf.Max(0f, stateDuration - (float)(synchronizedTime - stateStartTime));
            SetState(snapshot.state);
            if (CurrentState == SafeZoneState.Shrinking) UpdateShrinkGeometry();
            RefreshVisuals();
        }

        public static SafeZoneGeometry InterpolateShrink(Vector3 startCenter, float startRadius,
            Vector3 destinationCenter, float destinationRadius, float elapsed, float duration)
        {
            var progress = Mathf.Clamp01(Mathf.Max(0f, elapsed) / Mathf.Max(0.1f, duration));
            return new SafeZoneGeometry(Vector3.Lerp(startCenter, destinationCenter, progress),
                Mathf.Lerp(startRadius, destinationRadius, progress));
        }

        private void UpdateShrinkGeometry()
        {
            var elapsed = stateDuration - stateTimer;
            var geometry = InterpolateShrink(ShrinkStartCenter, ShrinkStartRadius, NextCenter, NextRadius,
                elapsed, stateDuration);
            CurrentCenter = geometry.Center;
            CurrentRadius = geometry.Radius;
        }

        private void FinishSystem()
        {
            HasNextZone = false;
            CurrentRadius = Mathf.Max(config.minimumFinalRadius, CurrentRadius);
            stateTimer = 0f;
            stateDuration = 0f;
            SetState(SafeZoneState.Finished);
            OnFinalZoneReached?.Invoke();
            RefreshVisuals();
        }

        private void BeginState(SafeZoneState state, float duration)
        {
            stateDuration = Mathf.Max(0f, duration);
            stateTimer = stateDuration;
            stateStartTime = Time.timeAsDouble;
            warningEmitted = false;
            SetState(state);
        }

        private void SetState(SafeZoneState state)
        {
            if (CurrentState == state) return;
            CurrentState = state;
            OnStateChanged?.Invoke(state);
        }

        private void CheckWarning()
        {
            if (warningEmitted || CurrentState != SafeZoneState.Waiting || CurrentPhase == null
                || stateTimer > CurrentPhase.warningTime) return;
            warningEmitted = true;
            OnWarningStarted?.Invoke();
        }

        private bool IsValidGroundCenter(Vector3 point)
        {
            if (config.allowZoneOverWater) return true;
            return PlayableArea.TryGround(point, out _, 0f);
        }

        private bool ValidateConfig()
        {
            if (config == null)
            {
                Debug.LogError("[SafeZoneController] SafeZoneConfig is not configured.", this);
                return false;
            }
            config.Normalize();
            if (config.phases.Count > 0) return true;
            Debug.LogError("[SafeZoneController] SafeZoneConfig has no phases.", this);
            return false;
        }

        private void EnsureWorldVisual()
        {
            if (worldVisual != null) return;
            if (config.worldVisualPrefab != null)
            {
                var instance = Instantiate(config.worldVisualPrefab, transform);
                instance.name = "Safe Zone World Visual";
                worldVisual = instance.GetComponent<SafeZoneWorldVisual>()
                    ?? instance.AddComponent<SafeZoneWorldVisual>();
            }
            else
            {
                var visualObject = new GameObject("Safe Zone World Visual");
                visualObject.transform.SetParent(transform, false);
                worldVisual = visualObject.AddComponent<SafeZoneWorldVisual>();
            }
            worldVisual.Configure(this, config);
        }

        private void RefreshVisuals()
        {
            if (config != null && worldVisual == null) EnsureWorldVisual();
            worldVisual?.Refresh();
            OnZoneUpdated?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            if (config != null && !config.showDebugGizmos) return;
            Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.95f);
            DrawCircle(CurrentCenter, CurrentRadius);
            if (HasNextZone)
            {
                Gizmos.color = new Color(1f, 0.78f, 0.1f, 0.95f);
                DrawCircle(NextCenter, NextRadius);
                Gizmos.DrawLine(CurrentCenter, NextCenter);
            }
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(PlayableArea.Bounds.center, PlayableArea.Bounds.size);
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            if (radius <= 0f) return;
            const int segments = 96;
            var previous = center + new Vector3(radius, 0.08f, 0f);
            for (var i = 1; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var next = center + new Vector3(Mathf.Cos(angle) * radius, 0.08f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private void OnDestroy()
        {
            if (runtimeLegacyConfig != null) Destroy(runtimeLegacyConfig);
        }
    }
}
