using UnityEngine;

namespace BattleRoyale
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class BRCharacterMotor : MonoBehaviour
    {
        public const float LowVaultProbeHeight = 0.40f;
        public const float HighVaultProbeHeight = 1.24f;
        public const float VaultForwardProbeDistance = 0.92f;
        public const float MaxVaultLandingHeight = 0.82f;
        public const float CompactVaultHeight = 0.58f;
        public const float CompactVaultArcHeight = 0.60f;
        public const float CompactVaultGroundClearance = 0.10f;
        public const float CompactVaultRadius = 0.19f;
        public const float CompactVaultForwardDistance = 1.58f;

        private const float MinVaultLandingHeight = -0.7f;
        private const float VaultLandingProbeClearance = 0.2f;
        private const float VaultArcHeight = 0.68f;
        private const float VaultCollisionInset = 0.01f;
        private const int VaultArcSamples = 12;
        private CharacterController controller;
        private Vector3 verticalVelocity;
        private Vector3 planarVelocity;
        private float yaw;
        private bool vaulting;
        private float vaultProgress;
        private Vector3 vaultStart;
        private Vector3 vaultEnd;
        private bool vaultControllerWasEnabled;
        private bool compactVault;
        private float activeVaultArcHeight = VaultArcHeight;

        public Vector3 Velocity { get; private set; }
        public bool IsGrounded => controller != null && controller.isGrounded;
        public bool Crouching { get; private set; }
        public bool Vaulting => vaulting;
        public bool CompactVaulting => vaulting && compactVault;
        public float NormalizedSpeed { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Aiming { get; private set; }
        public float RequestedMoveSpeed { get; private set; }

        public static bool ShouldFaceCamera(bool aiming, bool firing) => aiming || firing;

        public static float ResolveMoveSpeed(bool downed, BRGameConfig config, bool sprinting,
            bool crouching, bool aiming) => downed
            ? config.downedMoveSpeed
            : config.playerMoveSpeed * GameplayTuning.MovementMultiplier(sprinting, crouching, aiming);

        public static bool CanStartJump(bool downed, bool canJump, bool grounded, bool jumpPressed) =>
            !downed && canJump && grounded && jumpPressed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            yaw = transform.eulerAngles.y;
        }

        public void Move(BRInputFrame input, Transform cameraTransform, BRGameConfig config, bool canJump)
        {
            controller ??= GetComponent<CharacterController>();
            if (controller == null) return;
            var participant = GetComponent<BRParticipant>();
            var downed = participant != null && participant.IsDowned;
            Aiming = !downed && input.Aim;
            var facingCamera = !downed && ApplyCameraFacing(input, cameraTransform);
            if (vaulting)
            {
                if (downed) CancelVault();
                else
                {
                TickVault();
                return;
                }
            }
            var forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            var right = cameraTransform != null ? cameraTransform.right : transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            var move = Vector3.ClampMagnitude(right * input.Move.x + forward * input.Move.y, 1f);
            Crouching = downed || input.Crouch && controller.isGrounded;
            Sprinting = !downed && input.Sprint && !Aiming && move.sqrMagnitude > 0.2f;
            var speed = ResolveMoveSpeed(downed, config, Sprinting, Crouching, Aiming);
            RequestedMoveSpeed = speed;
            planarVelocity = GameplayTuning.AcceleratePlanar(planarVelocity, move * speed, controller.isGrounded, Time.deltaTime);
            NormalizedSpeed = config.playerMoveSpeed > 0f ? planarVelocity.magnitude / config.playerMoveSpeed : 0f;
            if (controller.isGrounded && verticalVelocity.y < 0f) verticalVelocity.y = -2f;
            if (CanStartJump(downed, canJump, controller.isGrounded, input.Jump))
            {
                if (TryStartVault(move)) return;
                verticalVelocity.y = config.jumpSpeed;
            }
            verticalVelocity.y -= config.gravity * Time.deltaTime;
            var displacement = planarVelocity + verticalVelocity;
            controller.Move(displacement * Time.deltaTime);
            Velocity = controller.velocity;

            if (!facingCamera && move.sqrMagnitude > 0.02f)
            {
                yaw = transform.eulerAngles.y;
                var targetYaw = Quaternion.LookRotation(move).eulerAngles.y;
                yaw = Mathf.LerpAngle(yaw, targetYaw, TurnInterpolationFactor(14f, Time.deltaTime));
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private bool ApplyCameraFacing(BRInputFrame input, Transform cameraTransform)
        {
            if (!ShouldFaceCamera(input.Aim, input.Fire) || cameraTransform == null) return false;
            var facing = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (facing.sqrMagnitude <= 0.01f) return false;
            yaw = Quaternion.LookRotation(facing.normalized).eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return true;
        }

        public void MoveWorld(Vector3 worldMove, float speed)
        {
            controller ??= GetComponent<CharacterController>();
            if (controller == null) return;
            Crouching = false;
            Aiming = false;
            Sprinting = false;
            planarVelocity = GameplayTuning.AcceleratePlanar(planarVelocity,
                Vector3.ClampMagnitude(worldMove, 1f) * speed, controller.isGrounded, Time.deltaTime);
            if (controller.isGrounded && verticalVelocity.y < 0f) verticalVelocity.y = -2f;
            verticalVelocity.y -= 22f * Time.deltaTime;
            controller.Move((planarVelocity + verticalVelocity) * Time.deltaTime);
            NormalizedSpeed = speed > 0f ? planarVelocity.magnitude / speed : 0f;
            if (worldMove.sqrMagnitude > 0.03f)
                transform.rotation = TurnTowardsWorldDirection(transform.rotation, worldMove, 9f, Time.deltaTime);
            Velocity = controller.velocity;
        }

        public void StopMotion()
        {
            planarVelocity = Vector3.zero;
            verticalVelocity = Vector3.zero;
            Velocity = Vector3.zero;
            NormalizedSpeed = 0f;
            RequestedMoveSpeed = 0f;
            Sprinting = false;
            Aiming = false;
        }

        public void Teleport(Vector3 position)
        {
            controller ??= GetComponent<CharacterController>();
            CancelVault();
            if (controller == null)
            {
                transform.position = position;
                verticalVelocity = Vector3.zero;
                planarVelocity = Vector3.zero;
                Crouching = false;
                return;
            }
            var wasEnabled = controller.enabled;
            if (wasEnabled) controller.enabled = false;
            transform.position = position;
            verticalVelocity = Vector3.zero;
            planarVelocity = Vector3.zero;
            Crouching = false;
            NormalizedSpeed = 0f;
            controller.enabled = wasEnabled;
        }

        private bool TryStartVault(Vector3 move)
        {
            if (move.sqrMagnitude < 0.08f) move = transform.forward;
            var forward = Vector3.ProjectOnPlane(move, Vector3.up).normalized;
            var lowOrigin = transform.position + Vector3.up * LowVaultProbeHeight;
            if (!TryObstacle(lowOrigin, forward, VaultForwardProbeDistance, out _)) return false;
            var highOrigin = transform.position + Vector3.up * HighVaultProbeHeight;
            if (TryObstacle(highOrigin, forward, VaultForwardProbeDistance, out _)) return false;
            var proposedStart = transform.position;
            var standardDistance = VaultForwardProbeDistance + CharacterPresentationProfile.ControllerRadius + 0.17f;
            if (TryFindVaultLanding(forward, standardDistance, out var proposedEnd)
                && IsCapsuleClear(proposedEnd, false)
                && IsVaultArcClear(proposedStart, proposedEnd, false, VaultArcHeight))
            {
                BeginVault(proposedStart, proposedEnd, false, VaultArcHeight);
                return true;
            }

            if (!TryFindVaultLanding(forward, CompactVaultForwardDistance, out proposedEnd)
                || !IsCapsuleClear(proposedEnd, false)
                || !IsVaultArcClear(proposedStart, proposedEnd, true, CompactVaultArcHeight)) return false;
            BeginVault(proposedStart, proposedEnd, true, CompactVaultArcHeight);
            return true;
        }

        private bool TryFindVaultLanding(Vector3 forward, float forwardDistance, out Vector3 end)
        {
            var probeHeight = HighVaultProbeHeight + MaxVaultLandingHeight + VaultLandingProbeClearance;
            var probe = transform.position + forward * forwardDistance + Vector3.up * probeHeight;
            var probeDistance = probeHeight - MinVaultLandingHeight;
            var hits = Physics.RaycastAll(probe, Vector3.down, probeDistance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var landing in hits)
            {
                if (ShouldIgnoreVaultCollider(landing.collider)) continue;
                var heightDelta = landing.point.y - transform.position.y;
                if (heightDelta < MinVaultLandingHeight || heightDelta > MaxVaultLandingHeight
                    || landing.normal.y < 0.65f) continue;
                end = landing.point + Vector3.up * 0.05f;
                return true;
            }
            end = default;
            return false;
        }

        private void BeginVault(Vector3 start, Vector3 end, bool compact, float arcHeight)
        {
            compactVault = compact;
            activeVaultArcHeight = arcHeight;
            vaultStart = start;
            vaultEnd = end;
            vaultProgress = 0f;
            vaulting = true;
            verticalVelocity = Vector3.zero;
            vaultControllerWasEnabled = controller.enabled;
            controller.enabled = false;
        }

        private bool IsVaultArcClear(Vector3 start, Vector3 end, bool compact, float arcHeight)
        {
            var previous = start;
            for (var sample = 1; sample <= VaultArcSamples; sample++)
            {
                var next = VaultPosition(start, end, sample / (float)VaultArcSamples, arcHeight, compact);
                if (!IsCapsuleSegmentClear(previous, next, compact)) return false;
                previous = next;
            }
            return true;
        }

        private bool IsCapsuleSegmentClear(Vector3 from, Vector3 to, bool compact)
        {
            var delta = to - from;
            if (delta.sqrMagnitude <= 0.000001f) return IsCapsuleClear(to, compact);
            GetCapsule(from, compact, out var bottom, out var top, out var radius);
            var hits = Physics.CapsuleCastAll(bottom, top, radius, delta.normalized, delta.magnitude,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (!ShouldIgnoreVaultCollider(hit.collider)) return false;
            }
            return IsCapsuleClear(to, compact);
        }

        private bool IsCapsuleClear(Vector3 rootPosition, bool compact)
        {
            GetCapsule(rootPosition, compact, out var bottom, out var top, out var radius);
            var overlaps = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var overlap in overlaps)
            {
                if (!ShouldIgnoreVaultCollider(overlap)) return false;
            }
            return true;
        }

        private void GetCapsule(Vector3 rootPosition, bool compact, out Vector3 bottom, out Vector3 top,
            out float radius)
        {
            radius = compact ? CompactVaultRadius : Mathf.Max(0.01f, controller.radius - VaultCollisionInset);
            var height = compact ? CompactVaultHeight : controller.height;
            var centerOffset = compact
                ? CompactVaultHeight * 0.5f + CompactVaultGroundClearance
                : controller.center.y;
            var halfHeight = Mathf.Max(radius, height * 0.5f - VaultCollisionInset);
            var axis = transform.rotation * Vector3.up;
            var center = rootPosition + axis * centerOffset;
            var segment = Mathf.Max(0f, halfHeight - radius);
            bottom = center - axis * segment;
            top = center + axis * segment;
        }

        private bool ShouldIgnoreVaultCollider(Collider collider) => collider == null
            || collider == controller || collider.transform == transform || collider.transform.IsChildOf(transform);

        private static Vector3 VaultPosition(Vector3 start, Vector3 end, float progress, float arcHeight,
            bool compact)
        {
            var position = Vector3.Lerp(start, end, progress);
            position.y += VaultArcOffset(progress, compact) * arcHeight;
            return position;
        }

        public static float VaultArcOffset(float progress, bool compact)
        {
            var arc = Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI));
            return compact ? Mathf.Pow(arc, 0.65f) : arc;
        }

        private bool TryObstacle(Vector3 origin, Vector3 direction, float distance, out RaycastHit obstacle)
        {
            var hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<LootPickup>() != null) continue;
                obstacle = hit;
                return true;
            }
            obstacle = default;
            return false;
        }

        private void TickVault()
        {
            var nextProgress = Mathf.MoveTowards(vaultProgress, 1f, Time.deltaTime / 0.42f);
            var position = VaultPosition(vaultStart, vaultEnd, nextProgress, activeVaultArcHeight, compactVault);
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
            {
                CancelVault();
                Velocity = Vector3.zero;
                return;
            }
            if (!IsCapsuleSegmentClear(transform.position, position, compactVault))
            {
                CancelVault();
                Velocity = Vector3.zero;
                return;
            }
            vaultProgress = nextProgress;
            transform.position = position;
            Velocity = (vaultEnd - vaultStart) / 0.42f;
            if (vaultProgress < 1f) return;
            vaulting = false;
            controller.enabled = vaultControllerWasEnabled;
            if (controller.enabled) controller.Move(Vector3.down * 0.03f);
            Velocity = planarVelocity;
        }

        private void CancelVault()
        {
            if (!vaulting) return;
            vaulting = false;
            vaultProgress = 0f;
            verticalVelocity = Vector3.zero;
            compactVault = false;
            activeVaultArcHeight = VaultArcHeight;
            if (controller != null) controller.enabled = vaultControllerWasEnabled;
        }

        private void OnDisable() => CancelVault();

        public static float TurnInterpolationFactor(float turnSpeed, float deltaTime) =>
            1f - Mathf.Exp(-Mathf.Max(0f, turnSpeed) * Mathf.Max(0f, deltaTime));

        public static Quaternion TurnTowardsWorldDirection(Quaternion current, Vector3 worldDirection,
            float turnSpeed, float deltaTime)
        {
            var planarDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            if (planarDirection.sqrMagnitude <= 0.000001f) return current;
            return Quaternion.Slerp(current, Quaternion.LookRotation(planarDirection.normalized),
                TurnInterpolationFactor(turnSpeed, deltaTime));
        }
    }
}
