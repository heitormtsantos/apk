using UnityEngine;

namespace BattleRoyale
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private float sensitivity = 2.2f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 62f;
        [SerializeField] private bool debugHipFireAim;

        private Transform target;
        private Vector3 smoothVelocity;
        private float yaw;
        private float pitch = -4f;
        private float recoilPitch;
        private float recoilYaw;
        private bool leftShoulder;
        private bool stagingView;
        private bool snapNextFrame = true;
        private bool hasResolvedCollision;
        private bool previousObstructed;
        private float previousResolvedClearance;
        private bool hasStablePosition;
        private Vector3 stablePosition;
        private Vector3 stablePivot;
        private Vector3 stableSide;
        private SphereCollider collisionProbe;
        private Camera viewCamera;
        private bool deploymentSteering;
        private float lastManualLookTime = float.NegativeInfinity;
        private readonly AimAssistController aimAssist = new();
        private readonly AimStateController aimState = new();
        private ScopeController scopeController;
        private HipFireAimDebug hipFireDebug;
        private LocalAdsVisibility localAdsVisibility;
        private static readonly RaycastHit[] CameraHits = new RaycastHit[64];
        private static readonly Collider[] CameraOverlaps = new Collider[64];

        private const float MaximumLocalRecovery = 1.25f;
        private const int MaximumPenetrationPasses = 8;

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            scopeController = GetComponent<ScopeController>();
            if (scopeController == null) scopeController = gameObject.AddComponent<ScopeController>();
            hipFireDebug = GetComponent<HipFireAimDebug>();
            if (hipFireDebug == null) hipFireDebug = gameObject.AddComponent<HipFireAimDebug>();
            collisionProbe = gameObject.GetComponent<SphereCollider>();
            if (collisionProbe == null) collisionProbe = gameObject.AddComponent<SphereCollider>();
            collisionProbe.isTrigger = true;
            collisionProbe.enabled = true;
        }

        public void SetTarget(Transform newTarget)
        {
            localAdsVisibility?.SetHidden(false);
            target = newTarget;
            localAdsVisibility = null;
            var participant = target != null ? target.GetComponent<BRParticipant>() : null;
            if (participant != null && participant.IsPlayer)
            {
                localAdsVisibility = target.GetComponent<LocalAdsVisibility>();
                if (localAdsVisibility == null)
                    localAdsVisibility = target.gameObject.AddComponent<LocalAdsVisibility>();
            }
            ResetViewState();
        }

        public void ResetViewState()
        {
            smoothVelocity = Vector3.zero;
            recoilPitch = 0f;
            recoilYaw = 0f;
            aimAssist.Clear();
            aimState.Reset();
            localAdsVisibility?.SetHidden(false);
            AimAssistTarget = null;
            Aiming = false;
            Indoors = false;
            yaw = target != null ? target.eulerAngles.y : 0f;
            pitch = -4f;
            deploymentSteering = false;
            lastManualLookTime = float.NegativeInfinity;
            ResetCollisionHistory();
        }

        private void ResetCollisionHistory()
        {
            snapNextFrame = true;
            hasResolvedCollision = false;
            previousObstructed = false;
            previousResolvedClearance = 0f;
            hasStablePosition = false;
            stablePosition = default;
            stablePivot = default;
            stableSide = default;
        }

        public void SetStagingView(bool enabled)
        {
            stagingView = enabled;
            smoothVelocity = Vector3.zero;
            ResetCollisionHistory();
        }

        public bool StagingViewEnabled => stagingView;
        public BRParticipant AimAssistTarget { get; private set; }
        public bool AimAssistLocked => AimAssistTarget != null;
        public bool Indoors { get; private set; }
        public bool Aiming { get; private set; }
        public float AdsProgress => aimState.Progress;
        public ScopeType ActiveScopeType => aimState.Mode;
        public AimPresentationMode PresentationMode => aimState.PresentationMode;
        public bool FullFirstPersonAds => aimState.PresentationMode == AimPresentationMode.FirstPerson
            && aimState.Progress > 0.55f;
        public bool SoftShoulderAds => aimState.PresentationMode == AimPresentationMode.SoftShoulder
            && aimState.Progress > 0.01f;
        public VerticalAimState VerticalAimState => aimAssist.VerticalState;
        public float HorizontalAssistStrength => aimAssist.CurrentHorizontalStrength;
        public float VerticalAssistStrength => aimAssist.CurrentVerticalStrength;
        public Vector2 LastFireDragDelta => aimAssist.FireDragDelta;
        public float ChestScreenDistance => aimAssist.ChestScreenDistance;
        public float HeadScreenDistance => aimAssist.HeadScreenDistance;
        public Vector2 HeadScreenOffset => aimAssist.HeadScreenOffset;
        public Vector2 LastAimSensitivityScale { get; private set; } = Vector2.one;
        public Transform TargetTransform => target;
        public float CurrentYaw => yaw;
        public float CurrentPitch => pitch;
        public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        public Vector3 PlanarRight => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        public bool RecoilSettled => Mathf.Approximately(recoilPitch, 0f) && Mathf.Approximately(recoilYaw, 0f);

        public void SetDeploymentSteering(bool active) => deploymentSteering = active;

        public void SetStaticView(Vector3 position, Vector3 lookAt, float fieldOfView = 62f)
        {
            target = null;
            smoothVelocity = Vector3.zero;
            ResetCollisionHistory();
            var fallbackRotation = transform.rotation;
            var preserveRotation = (lookAt - position).sqrMagnitude <= 0.000001f;
            position = ResolveSafePosition(lookAt, position, 0.18f, 0.2f, out _, out _);
            var rotation = preserveRotation
                ? fallbackRotation
                : SafeLookRotation(lookAt - position, fallbackRotation);
            transform.SetPositionAndRotation(position, rotation);
            viewCamera ??= GetComponent<Camera>();
            if (viewCamera != null) viewCamera.fieldOfView = Mathf.Clamp(fieldOfView, 24f, 80f);
        }

        public void Tick(BRInputFrame input)
        {
            PreCombatTick(input);
            PostMovementTick(input);
        }

        public void PreCombatTick(BRInputFrame input)
        {
            if (target == null) return;
            if (hipFireDebug != null) hipFireDebug.Visible = debugHipFireAim;
            Aiming = input.Aim;
            recoilPitch = Mathf.Lerp(recoilPitch, 0f, 12f * Time.deltaTime);
            recoilYaw = Mathf.Lerp(recoilYaw, 0f, 14f * Time.deltaTime);
            var participant = target.GetComponent<BRParticipant>();
            var weapon = target.GetComponent<InventorySystem>()?.ActiveWeapon;
            var adsProgress = aimState.Tick(input.Aim, weapon, Time.deltaTime);
            scopeController?.Tick(aimState.PresentationMode == AimPresentationMode.FirstPerson
                && weapon != null ? weapon.scope : null, adsProgress);
            var device = input.InputDevice;
            var orbitForward = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
            var assistOrigin = transform.position;
            var retainingHipFire = input.Fire && !input.Aim;
            var manualBreakMagnitude = retainingHipFire
                ? Mathf.Abs(input.Look.x) : input.Look.magnitude;
            if (participant != null && participant.IsPlayer && weapon != null)
                AimAssistTarget = aimAssist.UpdateTarget(participant, assistOrigin, orbitForward,
                    weapon, manualBreakMagnitude, retainingHipFire, input.Aim);
            else
            {
                aimAssist.Clear();
                AimAssistTarget = null;
            }
            viewCamera ??= GetComponent<Camera>();
            aimAssist.UpdateHipFireState(viewCamera, weapon, input.Fire, !input.Aim,
                input.FireDragDelta, device, Time.unscaledTime);
            AimAssistTarget = aimAssist.Target;
            var slowdown = weapon != null ? aimAssist.SensitivityScaleByAxis(weapon, device, input.Aim) : Vector2.one;
            LastAimSensitivityScale = slowdown;
            var configuredSensitivity = sensitivity * GamePreferences.LookSensitivity
                * (input.Aim ? GamePreferences.AdsSensitivity * (weapon != null ? weapon.AdsSensitivity : 1f) : 1f);
            var look = input.Look;
            if (viewCamera != null)
            {
                var adsFieldOfView = aimState.PresentationMode == AimPresentationMode.SoftShoulder
                    ? weapon?.ResolvedAimDefinition.shoulderFieldOfView ?? 58f
                    : weapon != null ? weapon.AdsFieldOfView : 54f;
                var hipFieldOfView = weapon?.ResolvedAimDefinition.hipFieldOfView
                    ?? TargetFieldOfView(false, input.Sprint && input.Move.sqrMagnitude > 0.45f);
                viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView,
                    Mathf.Lerp(hipFieldOfView, adsFieldOfView, adsProgress),
                    (weapon != null && weapon.scope != null ? weapon.scope.transitionSpeed : 10f) * Time.deltaTime);
            }
            if (look.sqrMagnitude > 0.0004f) lastManualLookTime = Time.unscaledTime;
            yaw += look.x * configuredSensitivity * slowdown.x;
            pitch = Mathf.Clamp(pitch - look.y * configuredSensitivity * slowdown.y, minPitch, maxPitch);
            var deploying = participant != null
                && participant.Phase is ParticipantPhase.Freefall or ParticipantPhase.Parachute;
            if (deploying && deploymentSteering && Time.unscaledTime - lastManualLookTime >= 0.45f)
                yaw = DampedDeploymentYaw(yaw, target.eulerAngles.y, 5.5f, Time.deltaTime);
            if (input.ShoulderSwap) leftShoulder = !leftShoulder;
            if (weapon != null)
            {
                var assistInputMagnitude = aimAssist.VerticalState == VerticalAimState.ChestAcquire
                    ? look.magnitude : Mathf.Abs(look.x);
                var assisted = aimAssist.CorrectAngles(yaw, pitch, assistOrigin, weapon, device,
                    input.Aim || input.Fire, assistInputMagnitude, Time.deltaTime, minPitch, maxPitch,
                    input.Aim);
                yaw = assisted.x;
                pitch = assisted.y;
            }
            transform.rotation = Quaternion.Euler(pitch - recoilPitch, yaw + recoilYaw, 0f);
        }

        public void PostMovementTick(BRInputFrame input)
        {
            if (target == null) return;
            var rotation = Quaternion.Euler(pitch - recoilPitch, yaw + recoilYaw, 0f);
            Indoors = IsUnderCover();
            var hipOffset = ShoulderOffsetFor(false, Indoors, leftShoulder);
            var activeWeapon = target.GetComponent<InventorySystem>()?.ActiveWeapon;
            var presentation = AimStateController.PresentationFor(activeWeapon);
            var adsOffset = presentation == AimPresentationMode.FirstPerson
                ? FirstPersonOffset(activeWeapon) : SoftShoulderOffset(Indoors, leftShoulder);
            if (presentation == AimPresentationMode.SoftShoulder && leftShoulder)
            {
                adsOffset.x = -Mathf.Abs(adsOffset.x);
            }
            var offset = Vector3.Lerp(hipOffset, adsOffset, aimState.Progress);
            var lookHeight = 0f;
            if (stagingView)
            {
                offset = new Vector3(0.65f, 3.2f, -5.6f);
                lookHeight = -0.35f;
            }
            var participant = target.GetComponent<BRParticipant>();
            var groundedCombatView = participant != null
                && participant.Phase is ParticipantPhase.Grounded or ParticipantPhase.Downed;
            var hideLocalPresentation = groundedCombatView
                && presentation == AimPresentationMode.FirstPerson && aimState.Progress > 0.42f
                && activeWeapon != null;
            localAdsVisibility?.SetHidden(
                hideLocalPresentation && activeWeapon.ResolvedAimDefinition.hideLocalBodyInAds,
                hideLocalPresentation && activeWeapon.ResolvedAimDefinition.hideLocalWeaponInAds);
            if (participant != null)
            {
                if (participant.Phase == ParticipantPhase.WaitingPlane) offset = new Vector3(2.8f, 5.5f, -19f);
                else if (participant.Phase == ParticipantPhase.Freefall)
                {
                    offset = DeploymentOffset(ParticipantPhase.Freefall);
                    lookHeight = 0.45f;
                }
                else if (participant.Phase == ParticipantPhase.Parachute)
                {
                    offset = DeploymentOffset(ParticipantPhase.Parachute);
                    lookHeight = 0.65f;
                }
            }
            else if (target.GetComponentInParent<VehicleController>() != null)
            {
                offset = new Vector3(leftShoulder ? -0.8f : 0.8f, 1.1f, -6.8f);
                lookHeight = 0.2f;
            }
            var traversing = participant != null && participant.Motor != null && participant.Motor.Vaulting;
            var pivot = target.position + Vector3.up * CharacterPresentationProfile.CameraPivot(traversing);
            var desired = pivot + rotation * offset;
            var firstPersonBlend = presentation == AimPresentationMode.FirstPerson ? aimState.Progress : 0f;
            var collisionRadius = Mathf.Lerp(Indoors ? 0.14f : 0.22f, 0.08f, firstPersonBlend);
            var safeDesired = ResolveSafePosition(pivot, desired, collisionRadius, 0.12f,
                out var resolvedClearance, out var obstructed);
            Vector3 nextPosition;
            if (ShouldSnapForClearance(snapNextFrame, hasResolvedCollision, previousResolvedClearance,
                previousObstructed, resolvedClearance, obstructed))
            {
                nextPosition = safeDesired;
                smoothVelocity = Vector3.zero;
            }
            else
            {
                nextPosition = SmoothedFollowPosition(transform.position, safeDesired, ref smoothVelocity,
                    obstructed ? 0.035f : 0.055f, Time.deltaTime);
                var clampedPosition = ResolveSafePosition(pivot, nextPosition, collisionRadius, 0.12f,
                    out var candidateClearance, out var candidateObstructed);
                if (Vector3.SqrMagnitude(clampedPosition - nextPosition) > 0.000001f)
                {
                    nextPosition = clampedPosition;
                    smoothVelocity = Vector3.zero;
                    if (candidateObstructed && candidateClearance < resolvedClearance)
                        resolvedClearance = candidateClearance;
                    obstructed |= candidateObstructed;
                }
            }
            snapNextFrame = false;
            hasResolvedCollision = true;
            previousResolvedClearance = resolvedClearance;
            previousObstructed = obstructed;
            var lookRotation = FinalViewRotation(aimState.Progress > 0.5f, pitch, yaw, recoilPitch, recoilYaw,
                rotation * Vector3.forward + Vector3.up * lookHeight, transform.rotation);
            transform.SetPositionAndRotation(nextPosition, lookRotation);
        }

        private bool IsUnderCover()
        {
            if (target == null) return false;
            var origin = target.position + Vector3.up * CharacterPresentationProfile.CameraPivotHeight;
            var hits = Physics.RaycastAll(origin, Vector3.up, 7f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (ShouldIgnoreCollision(hit.collider)) continue;
                return true;
            }
            return false;
        }

        private bool ShouldIgnoreCollision(Collider collider)
        {
            if (collider == null) return true;
            if (collider == collisionProbe) return true;
            if (target != null && (collider.transform == target || collider.transform.IsChildOf(target))) return true;
            var targetVehicle = target != null ? target.GetComponentInParent<VehicleController>() : null;
            if (targetVehicle != null && collider.GetComponentInParent<VehicleController>() == targetVehicle)
                return true;
            if (collider.GetComponentInParent<BRParticipant>() != null) return true;
            if (collider.GetComponentInParent<LootPickup>() != null) return true;
            if (collider.GetComponentInParent<DeathLootContainer>() != null) return true;
            if (collider.GetComponentInParent<WeaponSystem>() != null) return true;
            return collider.GetComponentInParent<ParticipantWeaponVisual>() != null;
        }

        private Vector3 ResolveSafePosition(Vector3 pivot, Vector3 candidate, float radius, float skin,
            out float resolvedClearance, out bool obstructed)
        {
            var direction = candidate - pivot;
            var desiredDistance = direction.magnitude;
            var preferredDirection = desiredDistance > 0.0001f
                ? direction / desiredDistance
                : target != null ? -target.forward : -transform.forward;
            if (HasBlockingOverlap(pivot, radius))
            {
                obstructed = true;
                if (TryUseStablePosition(pivot, preferredDirection, radius, out var stable))
                {
                    resolvedClearance = Vector3.Distance(pivot, stable);
                    return stable;
                }

                if (TryResolveLocalOverlap(pivot, preferredDirection, radius, skin, out var local))
                {
                    resolvedClearance = Vector3.Distance(pivot, local);
                    RememberStablePosition(pivot, local, preferredDirection, radius);
                    return local;
                }

                resolvedClearance = 0f;
                return pivot;
            }

            if (desiredDistance <= 0.0001f)
            {
                resolvedClearance = 0f;
                obstructed = false;
                return pivot;
            }

            var nearest = desiredDistance;
            var hitCount = Physics.SphereCastNonAlloc(pivot, radius, direction / desiredDistance,
                CameraHits, desiredDistance, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hitCount; i++)
            {
                var hit = CameraHits[i];
                if (ShouldIgnoreCollision(hit.collider)) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }

            if (nearest >= desiredDistance && HasBlockingOverlap(candidate, radius)) nearest = 0f;
            resolvedClearance = CorrectedCollisionDistance(desiredDistance, nearest, skin);
            obstructed = resolvedClearance < desiredDistance - 0.0001f;
            var resolved = pivot + preferredDirection * resolvedClearance;
            RememberStablePosition(pivot, resolved, preferredDirection, radius);
            return resolved;
        }

        private bool TryUseStablePosition(Vector3 pivot, Vector3 preferredDirection, float radius,
            out Vector3 position)
        {
            position = stablePosition;
            if (!hasStablePosition || HasBlockingOverlap(stablePosition, radius)) return false;
            var currentSide = stablePosition - pivot;
            var expectedSide = stableSide.sqrMagnitude > 0.000001f ? stableSide : preferredDirection;
            if (currentSide.sqrMagnitude <= 0.000001f || Vector3.Dot(currentSide.normalized, expectedSide) < 0f)
                return false;
            if (!IsCollisionPathValid(stablePivot, stablePosition, radius)) return false;
            if (!IsCollisionPathValid(transform.position, stablePosition, radius)) return false;
            return true;
        }

        private bool TryResolveLocalOverlap(Vector3 pivot, Vector3 preferredDirection, float radius, float skin,
            out Vector3 position)
        {
            EnsureCollisionProbe(radius);
            var recoverySide = stableSide.sqrMagnitude > 0.000001f
                ? stableSide.normalized
                : preferredDirection.sqrMagnitude > 0.000001f ? preferredDirection.normalized : Vector3.back;
            position = pivot + recoverySide * 0.001f;
            for (var pass = 0; pass < MaximumPenetrationPasses; pass++)
            {
                var overlapCount = Physics.OverlapSphereNonAlloc(position, radius, CameraOverlaps,
                    ~0, QueryTriggerInteraction.Ignore);
                var correction = Vector3.zero;
                var blocked = false;
                for (var i = 0; i < overlapCount; i++)
                {
                    var collider = CameraOverlaps[i];
                    if (ShouldIgnoreCollision(collider)) continue;
                    blocked = true;
                    if (!Physics.ComputePenetration(collisionProbe, position, Quaternion.identity,
                            collider, collider.transform.position, collider.transform.rotation,
                            out var direction, out var distance)) continue;
                    var candidate = direction * (distance + Mathf.Max(0.002f, skin));
                    if (Vector3.Dot(candidate, recoverySide) < -0.001f) continue;
                    if (candidate.sqrMagnitude > correction.sqrMagnitude) correction = candidate;
                }
                if (!blocked) return IsLocalRecoveryValid(pivot, position, recoverySide, radius);
                if (correction.sqrMagnitude <= 0.000001f) return false;
                var remaining = MaximumLocalRecovery - Vector3.Distance(pivot, position);
                if (remaining <= 0f || correction.magnitude > remaining) return false;
                position += correction;
            }
            return !HasBlockingOverlap(position, radius)
                && IsLocalRecoveryValid(pivot, position, recoverySide, radius);
        }

        private bool IsLocalRecoveryValid(Vector3 pivot, Vector3 position, Vector3 recoverySide, float radius)
        {
            var offset = position - pivot;
            if (offset.magnitude > MaximumLocalRecovery + 0.0001f || Vector3.Dot(offset, recoverySide) < 0f)
                return false;
            if (hasStablePosition && !IsCollisionPathValid(stablePosition, position, radius)) return false;
            return true;
        }

        private void RememberStablePosition(Vector3 pivot, Vector3 position, Vector3 side, float radius)
        {
            if (HasBlockingOverlap(position, radius) || !IsCollisionPathValid(pivot, position, radius)) return;
            hasStablePosition = true;
            stablePosition = position;
            stablePivot = pivot;
            stableSide = side.sqrMagnitude > 0.000001f ? side.normalized : (position - pivot).normalized;
        }

        private bool IsCollisionPathValid(Vector3 start, Vector3 end, float radius)
        {
            var path = end - start;
            if (path.sqrMagnitude <= 0.000001f) return !HasBlockingOverlap(end, radius);
            var hitCount = Physics.SphereCastNonAlloc(start, radius, path.normalized, CameraHits,
                path.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hitCount; i++)
                if (!ShouldIgnoreCollision(CameraHits[i].collider)) return false;
            return !HasBlockingOverlap(end, radius);
        }

        private void EnsureCollisionProbe(float radius)
        {
            if (collisionProbe == null)
            {
                collisionProbe = gameObject.GetComponent<SphereCollider>();
                if (collisionProbe == null) collisionProbe = gameObject.AddComponent<SphereCollider>();
                collisionProbe.isTrigger = true;
                collisionProbe.enabled = true;
            }
            collisionProbe.radius = radius;
            collisionProbe.center = Vector3.zero;
        }

        private bool HasBlockingOverlap(Vector3 position, float radius)
        {
            var overlapCount = Physics.OverlapSphereNonAlloc(position, radius, CameraOverlaps,
                ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < overlapCount; i++)
            {
                if (!ShouldIgnoreCollision(CameraOverlaps[i])) return true;
            }
            return false;
        }

        public void AddRecoil(float vertical, float horizontal)
        {
            recoilPitch = Mathf.Clamp(recoilPitch + vertical, 0f, 4f);
            recoilYaw = Mathf.Clamp(recoilYaw + horizontal, -2f, 2f);
        }

        public static Vector3 OffsetFor(bool aiming, bool indoors) =>
            CharacterPresentationProfile.CameraOffset(aiming, indoors);

        public static Vector3 ShoulderOffsetFor(bool aiming, bool indoors, bool leftShoulder)
        {
            var offset = OffsetFor(aiming, indoors);
            if (leftShoulder) offset.x = -offset.x;
            return offset;
        }

        public static Vector3 FirstPersonOffset(WeaponDefinition weapon) =>
            weapon != null && weapon.scope != null ? weapon.scope.cameraOffset : new Vector3(0f, 0.015f, 0.08f);

        public static Vector3 SoftShoulderOffset(bool indoors, bool leftShoulder)
        {
            var offset = indoors ? new Vector3(0.27f, 0.14f, -0.98f)
                : new Vector3(0.34f, 0.16f, -1.20f);
            if (leftShoulder) offset.x = -offset.x;
            return offset;
        }

        public static Vector3 DeploymentOffset(ParticipantPhase phase) => phase switch
        {
            ParticipantPhase.Freefall => new Vector3(0f, 2.8f, -7f),
            ParticipantPhase.Parachute => new Vector3(0f, 2.7f, -6.5f),
            _ => Vector3.zero
        };

        public static float DampedDeploymentYaw(float currentYaw, float targetYaw,
            float response, float deltaTime)
        {
            var blend = 1f - Mathf.Exp(-Mathf.Max(0f, response) * Mathf.Max(0f, deltaTime));
            return Mathf.LerpAngle(currentYaw, targetYaw, blend);
        }

        public static float CorrectedCollisionDistance(float desiredDistance, float nearestHitDistance, float skin)
        {
            var desired = Mathf.Max(0f, desiredDistance);
            if (nearestHitDistance >= desired) return desired;
            return Mathf.Clamp(nearestHitDistance - Mathf.Max(0f, skin), 0f, desired);
        }

        public static bool ShouldSnapForClearance(bool firstFrame, bool hasPrevious,
            float previousClearance, bool previousWasObstructed, float currentClearance, bool currentlyObstructed)
        {
            if (firstFrame || !hasPrevious) return true;
            if (previousWasObstructed && !currentlyObstructed) return false;
            return currentlyObstructed && currentClearance < previousClearance - 0.0001f;
        }

        public static Vector3 SmoothedFollowPosition(Vector3 current, Vector3 target,
            ref Vector3 velocity, float smoothTime, float deltaTime) =>
            Vector3.SmoothDamp(current, target, ref velocity, smoothTime,
                Mathf.Infinity, Mathf.Max(0f, deltaTime));

        public static Quaternion SafeLookRotation(Vector3 direction, Quaternion fallback)
        {
            if (direction.sqrMagnitude <= 0.000001f) return fallback;
            var forward = direction.normalized;
            var up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(forward, up);
        }

        public static Quaternion FinalViewRotation(bool aiming, float pitch, float yaw,
            float recoilPitch, float recoilYaw, Vector3 hipLookDirection, Quaternion fallback) =>
            aiming
                ? Quaternion.Euler(pitch - recoilPitch, yaw + recoilYaw, 0f)
                : SafeLookRotation(hipLookDirection, fallback);

        public static Vector2 BoundedAimAngles(float currentYaw, float currentPitch, Vector3 targetDirection,
            float manualLookMagnitude, float deltaTime, float minimumPitch, float maximumPitch)
        {
            if (targetDirection.sqrMagnitude <= 0.000001f)
                return new Vector2(currentYaw, Mathf.Clamp(currentPitch, minimumPitch, maximumPitch));

            var direction = targetDirection.normalized;
            var desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var desiredPitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            var strength = AimAssistTargeting.MagnetismStrength(manualLookMagnitude);
            var maxStep = 118f * strength * Mathf.Max(0f, deltaTime);
            var nextYaw = Mathf.MoveTowardsAngle(currentYaw, desiredYaw, maxStep);
            var nextPitch = Mathf.Clamp(Mathf.MoveTowardsAngle(currentPitch, desiredPitch, maxStep * 0.72f),
                minimumPitch, maximumPitch);
            return new Vector2(nextYaw, nextPitch);
        }

        public static float MinimumCollisionDistance(bool indoors) => indoors ? 0.42f : 0.62f;

        public static float TargetFieldOfView(bool aiming, bool sprinting) => aiming ? 54f : sprinting ? 66f : 64f;
    }
}
