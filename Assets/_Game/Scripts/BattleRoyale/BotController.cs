using UnityEngine;

namespace BattleRoyale
{
    public enum BotState
    {
        Roaming,
        Looting,
        Rotating,
        Engaging,
        Reviving,
        Healing,
        Recovering
    }

    public sealed class BotController : MonoBehaviour
    {
        public const float ReviveApproachTimeout = 1.5f;
        public const float ReviveApproachProgressDistance = 0.2f;
        private BRParticipant participant;
        private LootSpawner lootSpawner;
        private SafeZoneController zone;
        private BRGameConfig config;
        private BRParticipant target;
        private BRParticipant reviveTarget;
        private Vector3 destination;
        private float nextThink;
        private float nextShot;
        private float nextProgressCheck;
        private float recoveryUntil;
        private float nextCollect;
        private float collectJitter;
        private Vector3 progressPosition;
        private float strafeSign = 1f;
        private float reviveProgress;
        private int reviveDamageSequence;
        private float reviveApproachElapsed;
        private float reviveApproachReferenceDistance;
        private readonly System.Collections.Generic.List<LootPickup> collectCandidates = new();

        public BotState State { get; private set; } = BotState.Roaming;
        public BRParticipant Target => target;
        public BRParticipant ReviveTarget => reviveTarget;
        public float ReviveProgress => reviveProgress;

        public void Configure(BRParticipant owner, LootSpawner spawner, SafeZoneController safeZone, BRGameConfig gameConfig)
        {
            participant = owner;
            lootSpawner = spawner;
            zone = safeZone;
            config = gameConfig;
            nextShot = Time.time + Random.Range(2.5f, 4.5f);
            collectJitter = Random.Range(0f, 0.075f);
            progressPosition = transform.position;
            nextProgressCheck = Time.time + 0.75f;
            PickDestination();
        }

        public void BeginStaging(Vector3 center, int index)
        {
            destination = transform.position;
        }

        public void TickStaging(Vector3 center, float radius)
        {
            if (!isActiveAndEnabled || participant == null || participant.Health.IsDead) return;
            participant.Motor.MoveWorld(Vector3.zero, 0f);
        }

        public void Tick(System.Collections.Generic.IReadOnlyList<BRParticipant> participants)
        {
            if (!isActiveAndEnabled || participant == null || participant.Health.IsDead) return;
            if (participant.IsDowned)
            {
                CancelRevive();
                participant.Motor.MoveWorld(Vector3.zero, 0f);
                return;
            }
            if (participant.Phase != ParticipantPhase.Grounded) return;
            if (participant.Inventory.UsingMedKit)
            {
                State = BotState.Healing;
                participant.Motor.MoveWorld(Vector3.zero, 0f);
                return;
            }
            if (State != BotState.Recovering && Time.time >= nextThink)
            {
                nextThink = Time.time + Random.Range(0.18f, 0.5f);
                Think(participants);
            }

            if (State == BotState.Reviving && reviveTarget != null)
            {
                TickRevive(Time.deltaTime);
                return;
            }

            var toDestination = destination - transform.position;
            toDestination.y = 0f;
            if (toDestination.magnitude < 2f) PickDestination();
            var move = toDestination.normalized;
            if (State == BotState.Recovering)
            {
                if (Time.time >= recoveryUntil)
                {
                    State = BotState.Roaming;
                    PickDestination();
                }
            }
            else if (target != null && State == BotState.Engaging)
            {
                var toTarget = target.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude > 17f) move = toTarget.normalized;
                else if (toTarget.magnitude < 7f) move = -toTarget.normalized * 0.65f;
                else move = Vector3.Cross(Vector3.up, toTarget.normalized) * strafeSign * 0.72f;
            }
            TryFireAtTarget();
            move = SteerAroundObstacle(move);
            var speedMultiplier = State switch
            {
                BotState.Rotating => 1.28f,
                BotState.Looting => 1.08f,
                _ => 1f
            };
            participant.Motor.MoveWorld(move, config.botMoveSpeed * speedMultiplier);
            TryCollectNearby();
            CheckProgress(move);
            var threatClose = target != null && Vector3.Distance(target.transform.position, transform.position) < 20f && HasSight(target);
            var fromCenter = Vector3.ProjectOnPlane(transform.position - zone.CurrentCenter, Vector3.up).magnitude;
            var safelyInside = fromCenter < zone.CurrentRadius * 0.82f;
            if (participant.Health.Health < 45f && !threatClose && safelyInside
                && participant.Inventory.TryUseMedKit(participant.Health))
                State = BotState.Healing;
        }

        private void Think(System.Collections.Generic.IReadOnlyList<BRParticipant> participants)
        {
            target = null;
            var closeVisibleEnemy = FindClosestVisibleEnemy(participants, 20f);
            var downedAlly = SelectReviveTarget(participant, participants, 8f, closeVisibleEnemy != null);
            if (downedAlly != null)
            {
                if (reviveTarget != downedAlly)
                {
                    reviveTarget = downedAlly;
                    reviveProgress = 0f;
                    reviveDamageSequence = participant.Health.DamageSequence;
                    ResetReviveApproach();
                }
                destination = downedAlly.transform.position;
                State = BotState.Reviving;
                return;
            }
            CancelRevive();
            var activeWeapon = participant.Inventory.ActiveWeapon;
            if (activeWeapon == null)
            {
                var weaponLoot = lootSpawner.FindNearestVisible(transform.position, 72f, LootKind.Weapon)
                    ?? lootSpawner.FindNearest(transform.position, 40f, LootKind.Weapon);
                if (weaponLoot != null)
                {
                    destination = weaponLoot.transform.position;
                    State = BotState.Looting;
                    return;
                }
                var deathLoot = DeathLootContainer.FindNearest(transform.position, 48f);
                if (deathLoot != null)
                {
                    destination = deathLoot.transform.position;
                    State = BotState.Looting;
                    return;
                }
            }
            else if (participant.Weapon.Magazine == 0 && participant.Inventory.AmmoFor(activeWeapon.ammoKind) == 0)
            {
                var ammoLoot = lootSpawner.FindNearestVisibleAmmo(transform.position, 72f, activeWeapon.ammoKind)
                    ?? lootSpawner.FindNearestAmmo(transform.position, 40f, activeWeapon.ammoKind);
                if (ammoLoot != null)
                {
                    destination = ammoLoot.transform.position;
                    State = BotState.Looting;
                    return;
                }
                var deathLoot = DeathLootContainer.FindNearest(transform.position, 48f);
                if (deathLoot != null)
                {
                    destination = deathLoot.transform.position;
                    State = BotState.Looting;
                    return;
                }
            }
            else if (participant.Weapon.Magazine == 0)
            {
                participant.Weapon.TryReload();
            }
            var best = float.MaxValue;
            foreach (var candidate in participants)
            {
                if (!IsTargetCandidate(participant, candidate)) continue;
                var sqr = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    target = candidate;
                }
            }

            if (NeedsZoneRotation(transform.position, zone.CurrentCenter, zone.CurrentRadius,
                    zone.NextCenter, zone.NextRadius))
            {
                destination = zone.NextCenter + Random.insideUnitSphere * Mathf.Max(3f, zone.NextRadius * 0.28f);
                destination.y = 0f;
                State = BotState.Rotating;
            }
            else if (target != null)
            {
                State = BotState.Engaging;
            }
            else if (participant.Health.Health < 45f && participant.Inventory.MedKits > 0)
            {
                State = BotState.Healing;
            }
            else if (Random.value < 0.55f)
            {
                var loot = lootSpawner.FindNearest(transform.position, 24f);
                if (loot != null)
                {
                    destination = loot.transform.position;
                    State = BotState.Looting;
                }
                else State = BotState.Roaming;
            }
            else State = BotState.Roaming;
        }

        public static bool NeedsZoneRotation(Vector3 position, Vector3 currentCenter, float currentRadius,
            Vector3 nextCenter, float nextRadius)
        {
            var currentDistance = Vector3.ProjectOnPlane(position - currentCenter, Vector3.up).magnitude;
            var nextDistance = Vector3.ProjectOnPlane(position - nextCenter, Vector3.up).magnitude;
            return currentDistance > currentRadius * 0.9f || nextDistance > nextRadius * 0.78f;
        }

        public static bool IsTargetCandidate(BRParticipant owner, BRParticipant candidate) =>
            candidate != null && candidate.Health != null && !candidate.Health.IsDead
            && candidate.Phase is ParticipantPhase.Grounded or ParticipantPhase.Downed
            && BRMatchRules.AreEnemies(owner, candidate);

        public static BRParticipant FindNearestDownedAlly(BRParticipant owner,
            System.Collections.Generic.IReadOnlyList<BRParticipant> participants, float radius)
        {
            if (owner == null || participants == null || radius <= 0f) return null;
            BRParticipant nearest = null;
            var bestSqr = radius * radius;
            foreach (var candidate in participants)
            {
                if (candidate == null || candidate == owner || candidate.TeamId != owner.TeamId || !candidate.IsDowned)
                    continue;
                var sqr = (candidate.transform.position - owner.transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                nearest = candidate;
            }
            return nearest;
        }

        public static BRParticipant SelectReviveTarget(BRParticipant owner,
            System.Collections.Generic.IReadOnlyList<BRParticipant> participants, float radius,
            bool hasVisibleThreat) => hasVisibleThreat
            ? null
            : FindNearestDownedAlly(owner, participants, radius);

        private BRParticipant FindClosestVisibleEnemy(
            System.Collections.Generic.IReadOnlyList<BRParticipant> participants, float radius)
        {
            BRParticipant nearest = null;
            var bestSqr = radius * radius;
            foreach (var candidate in participants)
            {
                if (!IsTargetCandidate(participant, candidate)) continue;
                var sqr = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqr > bestSqr || !HasSight(candidate)) continue;
                bestSqr = sqr;
                nearest = candidate;
            }
            return nearest;
        }

        public void TickRevive(float deltaTime)
        {
            if (reviveTarget == null || reviveTarget.IsEliminated
                || !BRMatchRules.CanContinueRevive(participant, reviveTarget, 8f, reviveDamageSequence))
            {
                CancelRevive();
                State = BotState.Roaming;
                return;
            }
            var toAlly = reviveTarget.transform.position - transform.position;
            toAlly.y = 0f;
            if (toAlly.magnitude > config.reviveRadius)
            {
                if (TrackReviveApproach(deltaTime))
                {
                    CancelRevive();
                    State = BotState.Recovering;
                    recoveryUntil = Time.time + 1f;
                    PickDestination();
                    return;
                }
                destination = reviveTarget.transform.position;
                participant.Motor.MoveWorld(SteerAroundObstacle(toAlly.normalized), config.botMoveSpeed);
                return;
            }
            participant.Weapon.CancelReloadForAction();
            participant.Motor.StopMotion();
            var step = BRMatchRules.AdvanceReviveHold(reviveProgress, deltaTime,
                config.reviveSeconds, true, true);
            reviveProgress = step.Progress;
            if (step.Status != ReviveHoldStatus.Completed) return;
            reviveTarget.Health.Revive();
            if (participant.Stats != null) participant.Stats.RecordRevive();
            else participant.Revives++;
            CancelRevive();
            State = BotState.Roaming;
            PickDestination();
        }

        private void CancelRevive()
        {
            reviveTarget = null;
            reviveProgress = 0f;
            reviveApproachElapsed = 0f;
            reviveApproachReferenceDistance = 0f;
        }

        private void ResetReviveApproach()
        {
            reviveApproachElapsed = 0f;
            reviveApproachReferenceDistance = PlanarDistanceToReviveTarget();
        }

        private bool TrackReviveApproach(float deltaTime)
        {
            var currentDistance = PlanarDistanceToReviveTarget();
            if (reviveApproachReferenceDistance - currentDistance >= ReviveApproachProgressDistance)
            {
                reviveApproachElapsed = 0f;
                reviveApproachReferenceDistance = currentDistance;
                return false;
            }
            reviveApproachElapsed += Mathf.Max(0f, deltaTime);
            return reviveApproachElapsed >= ReviveApproachTimeout;
        }

        private float PlanarDistanceToReviveTarget()
        {
            if (reviveTarget == null) return 0f;
            return Vector3.ProjectOnPlane(reviveTarget.transform.position - transform.position, Vector3.up).magnitude;
        }

        private float FireDelay()
        {
            var weapon = participant.Inventory.ActiveWeapon;
            if (weapon == null) return 1.2f;
            return weapon.weaponClass switch
            {
                WeaponClass.Smg => Random.Range(0.22f, 0.42f),
                WeaponClass.Rifle => Random.Range(0.32f, 0.58f),
                WeaponClass.Shotgun => Random.Range(0.85f, 1.25f),
                WeaponClass.Sniper => Random.Range(1.25f, 1.9f),
                _ => Random.Range(0.5f, 0.9f)
            };
        }

        private void TryFireAtTarget()
        {
            var weapon = participant.Inventory.ActiveWeapon;
            if (!IsTargetCandidate(participant, target) || weapon == null || Time.time < nextShot) return;
            var toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > Mathf.Min(weapon.range * 0.9f, 52f) * Mathf.Min(weapon.range * 0.9f, 52f)
                || !HasSight(target)) return;
            transform.rotation = BRCharacterMotor.TurnTowardsWorldDirection(transform.rotation, toTarget,
                14f, Time.deltaTime);
            if (Vector3.Angle(transform.forward, toTarget) > 8f) return;
            if (participant.Weapon.TryFire(null, true, gameObject))
            {
                nextShot = Time.time + FireDelay();
                if (Random.value < 0.2f) strafeSign *= -1f;
            }
        }

        private Vector3 SteerAroundObstacle(Vector3 intendedMove)
        {
            if (intendedMove.sqrMagnitude < 0.05f) return intendedMove;
            var direction = Vector3.ProjectOnPlane(intendedMove, Vector3.up).normalized;
            var origin = transform.position + Vector3.up * 0.55f;
            if (!Physics.SphereCast(origin, 0.28f, direction, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                return intendedMove;
            if (hit.collider.GetComponentInParent<BRParticipant>() != null || hit.collider.GetComponentInParent<LootPickup>() != null)
                return intendedMove;
            var tangent = Vector3.Cross(Vector3.up, hit.normal).normalized;
            if (Vector3.Dot(tangent, direction) < 0f) tangent = -tangent;
            return (tangent * 0.82f + hit.normal * 0.28f).normalized;
        }

        private void CheckProgress(Vector3 intendedMove)
        {
            if (Time.time < nextProgressCheck || State == BotState.Engaging || intendedMove.sqrMagnitude < 0.1f) return;
            var progress = Vector3.ProjectOnPlane(transform.position - progressPosition, Vector3.up).magnitude;
            progressPosition = transform.position;
            nextProgressCheck = Time.time + 0.75f;
            if (progress >= 0.22f) return;

            State = BotState.Recovering;
            recoveryUntil = Time.time + Random.Range(0.75f, 1.2f);
            var side = Vector3.Cross(Vector3.up, intendedMove.normalized) * (Random.value < 0.5f ? -1f : 1f);
            destination = transform.position + side * 4.5f - intendedMove.normalized * 1.5f;
        }

        private bool HasSight(BRParticipant candidate)
        {
            var weaponVisual = GetComponent<ParticipantWeaponVisual>();
            var origin = SightOrigin(transform, weaponVisual != null ? weaponVisual.MuzzlePosition : (Vector3?)null);
            var targetPoint = AimAssistTargeting.AimPoint(candidate);
            var direction = targetPoint - origin;
            return !Physics.Raycast(origin, direction.normalized, out var hit, direction.magnitude, ~0, QueryTriggerInteraction.Ignore)
                || hit.collider.GetComponentInParent<BRParticipant>() == candidate;
        }

        public static Vector3 SightOrigin(Transform root, Vector3? weaponMuzzle) =>
            weaponMuzzle ?? CharacterPresentationProfile.FallbackMuzzle(root);

        private void TryCollectNearby()
        {
            if (!participant.IsCombatCapable || State == BotState.Reviving) return;
            if (Time.time < nextCollect) return;
            nextCollect = Time.time + 0.18f + collectJitter;
            lootSpawner.FillCollectibleCandidates(transform.position, config.interactRadius, collectCandidates);
            foreach (var loot in collectCandidates)
            {
                if (loot.CollectQuantitative(participant).Success) return;
            }
            DeathLootContainer.FindNearest(transform.position, config.interactRadius + 0.5f)?.TryTakeBest(participant);
        }

        private void PickDestination()
        {
            destination = new Vector3(Random.Range(-config.mapSize.x * 0.42f, config.mapSize.x * 0.42f),
                0f, Random.Range(-config.mapSize.y * 0.42f, config.mapSize.y * 0.42f));
        }
    }
}
