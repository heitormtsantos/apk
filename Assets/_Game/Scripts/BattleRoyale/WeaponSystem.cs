using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class WeaponSystem : MonoBehaviour
    {
        public const int ShotgunPelletCount = 7;
        [SerializeField] private Transform muzzle;
        [SerializeField] private LayerMask hitMask = ~0;

        private InventorySystem inventory;
        private BRCharacterMotor motor;
        private WeaponAudioEmitter audioEmitter;
        private HealthArmorSystem health;
        private readonly Dictionary<WeaponDefinition, int> magazines = new();
        private readonly Dictionary<HealthArmorSystem, PelletHit> pelletHits = new();
        private readonly Dictionary<VehicleHealth, float> vehiclePelletHits = new();
        private bool reloading;
        private Coroutine reloadRoutine;
        private float nextShotTime;
        private float reloadStartedAt;
        private float reloadDuration;
        private int burstShotIndex;
        private float previousShotTime = -10f;
        private readonly WeaponTriggerController trigger = new();
        private readonly WeaponSpread spreadState = new();

        public int Magazine => inventory != null && inventory.ActiveWeapon != null && magazines.TryGetValue(inventory.ActiveWeapon, out var value) ? value : 0;
        public bool Reloading => reloading;
        public float ReloadProgress => !reloading ? 0f : Mathf.Clamp01((Time.time - reloadStartedAt) / Mathf.Max(0.01f, reloadDuration));
        public float LastShotTime { get; private set; } = -10f;
        public bool LastShotHit { get; private set; }
        public bool LastShotHeadshot { get; private set; }
        public float LastShotDamage { get; private set; }
        public Vector3 LastShotEndPoint { get; private set; }
        public bool AimAssistLocked { get; private set; }
        public AimSolution LastAimSolution { get; private set; }
        public static int TotalShotsFired { get; private set; }

        public float CurrentSpread(bool aiming)
        {
            var weapon = inventory != null ? inventory.ActiveWeapon : null;
            if (weapon == null) return 0f;
            var moving = motor != null && motor.Velocity.sqrMagnitude > 0.5f;
            var sprinting = motor != null && motor.Sprinting;
            var airborne = motor != null && !motor.IsGrounded;
            var crouching = motor != null && motor.Crouching;
            return spreadState.Evaluate(weapon, aiming, moving, sprinting, airborne, crouching,
                inventory != null ? inventory.SpreadMultiplier : 1f);
        }

        private void Update() => spreadState.Tick(inventory != null ? inventory.ActiveWeapon : null, Time.deltaTime);

        private void Awake()
        {
            inventory = GetComponent<InventorySystem>();
            motor = GetComponent<BRCharacterMotor>();
            audioEmitter = GetComponent<WeaponAudioEmitter>();
            health = GetComponent<HealthArmorSystem>();
            if (health != null) health.Downed += OnOwnerDowned;
        }

        public static bool CanUseWeapons(BRParticipant participant) => participant == null || participant.IsCombatCapable;

        private bool OwnerCanUseWeapons() => CanUseWeapons(GetComponent<BRParticipant>());

        private void OnOwnerDowned(HealthArmorSystem ownerHealth) => CancelReload();

        private void OnDestroy()
        {
            if (health != null) health.Downed -= OnOwnerDowned;
        }

        public void EquipInitial(WeaponDefinition weapon)
        {
            if (!OwnerCanUseWeapons()) return;
            CancelReload();
            inventory ??= GetComponent<InventorySystem>();
            if (inventory == null) return;
            inventory.AddWeapon(weapon);
            if (weapon != null) magazines[weapon] = weapon.magazineSize;
        }

        public WeaponDefinition EquipFromLoot(WeaponDefinition weapon)
        {
            if (weapon == null || !OwnerCanUseWeapons()) return null;
            CancelReload();
            inventory ??= GetComponent<InventorySystem>();
            if (inventory == null) return null;
            var replaced = inventory.AddWeapon(weapon);
            if (!magazines.ContainsKey(weapon)) magazines[weapon] = weapon.magazineSize;
            return replaced;
        }

        public bool EquipFromDeathLoot(WeaponDefinition weapon, int loadedMagazine)
        {
            if (weapon == null || !OwnerCanUseWeapons()) return false;
            CancelReload();
            inventory ??= GetComponent<InventorySystem>();
            if (inventory == null) return false;
            inventory.AddWeapon(weapon);
            magazines[weapon] = Mathf.Clamp(loadedMagazine, 0, weapon.magazineSize);
            return true;
        }

        public int MagazineFor(WeaponDefinition weapon) => weapon != null && magazines.TryGetValue(weapon, out var value) ? value : 0;

        public bool TickTrigger(Camera camera, bool aiming, bool held, bool pressed, GameObject owner)
        {
            var weapon = inventory != null ? inventory.ActiveWeapon : null;
            return trigger.ShouldFire(weapon, held, pressed, Time.time) && TryFire(camera, aiming, owner);
        }

        public bool TryFire(Camera camera, bool aiming, GameObject owner)
        {
            if (!OwnerCanUseWeapons()) return false;
            var weapon = inventory.ActiveWeapon;
            if (weapon == null || reloading || Time.time < nextShotTime) return false;
            if (Magazine <= 0)
            {
                TryReload();
                return false;
            }
            magazines[weapon] = Magazine - 1;
            if (Time.time - previousShotTime > Mathf.Max(0.32f, weapon.fireInterval * 2.4f)) burstShotIndex = 0;
            else burstShotIndex++;
            previousShotTime = Time.time;
            TotalShotsFired++;
            nextShotTime = Time.time + weapon.fireInterval;
            AimAssistLocked = false;
            var visual = GetComponent<ParticipantWeaponVisual>();
            var shotOrigin = visual != null ? visual.MuzzlePosition : CharacterPresentationProfile.FallbackMuzzle(transform);
            var intentRay = camera != null
                ? camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(muzzle != null ? muzzle.position : shotOrigin, transform.forward);
            var spread = CurrentSpread(aiming);
            var spreadOffset = Random.insideUnitCircle * spread;
            var cameraController = camera != null ? camera.GetComponent<ThirdPersonCamera>() : null;
            LastAimSolution = AimSolver.Solve(intentRay, shotOrigin, weapon.range, spreadOffset,
                owner, hitMask, cameraController != null ? cameraController.AimAssistTarget : null);
            AimAssistLocked = LastAimSolution.AssistedTarget != null;
            var shotRay = LastAimSolution.MuzzleRay;
            var shotDistance = LastAimSolution.Distance;
            var endPoint = shotOrigin + shotRay.direction * shotDistance;
            var hasImpact = false;
            var hitCharacter = false;
            var impactNormal = -shotRay.direction;
            var impactColor = new Color(0.9f, 0.92f, 1f);
            LastShotHit = false;
            LastShotHeadshot = false;
            LastShotDamage = 0f;
            if (weapon.simulationType == WeaponSimulationType.Projectile)
            {
                BallisticProjectile.Launch(shotOrigin, shotRay.direction, weapon, owner, hitMask, this);
                LastShotTime = Time.time;
                LastShotEndPoint = endPoint;
                spreadState.RegisterShot(weapon);
                SpawnShotFeedback(endPoint, weapon, camera, false, -shotRay.direction, Color.white, false);
                audioEmitter ??= GetComponent<WeaponAudioEmitter>();
                audioEmitter?.PlayShot(weapon.weaponClass);
                return true;
            }
            if (weapon.weaponClass == WeaponClass.Shotgun)
            {
                ResolveShotgun(shotOrigin, shotRay, shotDistance, spread, weapon, owner);
                LastShotTime = Time.time;
                spreadState.RegisterShot(weapon);
                SpawnShotFeedback(LastShotEndPoint, weapon, camera, false, -shotRay.direction, Color.white, false);
                audioEmitter ??= GetComponent<WeaponAudioEmitter>();
                audioEmitter?.PlayShot(weapon.weaponClass);
                return true;
            }
            if (TryGetFirstHit(shotRay, shotDistance, out var hit))
            {
                var health = hit.collider.GetComponentInParent<HealthArmorSystem>();
                var vehicleHealth = hit.collider.GetComponentInParent<VehicleHealth>();
                var damagedTarget = false;
                var headshot = false;
                if (health != null && health.gameObject != gameObject && CanDamage(owner, health))
                {
                    headshot = hit.collider.name.Contains("Head");
                    var distanceDamage = DamageAtDistance(weapon, hit.distance);
                    var report = health.ApplyDamage(distanceDamage * (headshot ? weapon.headshotMultiplier : 1f), headshot, owner);
                    LastShotHit = report.FinalDamage > 0f;
                    LastShotHeadshot = LastShotHit && headshot;
                    LastShotDamage = report.FinalDamage;
                    damagedTarget = LastShotHit;
                    hitCharacter = damagedTarget;
                    var shooter = owner != null ? owner.GetComponent<BRParticipant>() : null;
                    if (damagedTarget && shooter != null)
                    {
                        shooter.DamageDealt += Mathf.RoundToInt(report.AppliedDamage);
                        if (shooter.IsPlayer) DamageFeedbackSystem.Report(hit.point, report.FinalDamage, headshot);
                    }
                }
                else if (vehicleHealth != null && vehicleHealth.gameObject != owner)
                {
                    var damage = DamageAtDistance(weapon, hit.distance);
                    var applied = vehicleHealth.ApplyDamage(damage, DamageType.Weapon, owner);
                    LastShotHit = applied > 0f;
                    LastShotDamage = applied;
                    damagedTarget = LastShotHit;
                    hitCharacter = false;
                    if (applied > 0f && owner != null && owner.GetComponent<BRParticipant>() is { IsPlayer: true })
                        DamageFeedbackSystem.Report(hit.point, applied, false);
                }
                endPoint = hit.point;
                hasImpact = true;
                impactNormal = hit.normal;
                impactColor = damagedTarget ? (headshot ? DamageFeedbackSystem.HeadshotColor : DamageFeedbackSystem.BodyColor)
                    : new Color(0.9f, 0.92f, 1f);
            }
            LastShotTime = Time.time;
            LastShotEndPoint = endPoint;
            spreadState.RegisterShot(weapon);
            SpawnShotFeedback(endPoint, weapon, camera, hasImpact, impactNormal, impactColor, hitCharacter);
            audioEmitter ??= GetComponent<WeaponAudioEmitter>();
            audioEmitter?.PlayShot(weapon.weaponClass);
            return true;
        }

        private void ResolveShotgun(Vector3 origin, Ray centerRay, float distance, float pelletSpreadDegrees,
            WeaponDefinition weapon, GameObject owner)
        {
            pelletHits.Clear();
            vehiclePelletHits.Clear();
            var right = Vector3.Cross(Vector3.up, centerRay.direction).normalized;
            if (right.sqrMagnitude < 0.1f) right = transform.right;
            var up = Vector3.Cross(centerRay.direction, right).normalized;
            var farthestEnd = origin + centerRay.direction * distance;

            var pelletCount = Mathf.Clamp(weapon.pelletCount, 1, 20);
            for (var i = 0; i < pelletCount; i++)
            {
                var offset = i == 0 ? Vector2.zero : Random.insideUnitCircle * pelletSpreadDegrees * 0.018f;
                var direction = (centerRay.direction + right * offset.x + up * offset.y).normalized;
                var pelletRay = new Ray(origin, direction);
                var end = origin + direction * distance;
                var impact = false;
                var normal = -direction;
                var color = new Color(0.9f, 0.92f, 1f);
                var characterImpact = false;
                if (TryGetFirstHit(pelletRay, distance, out var hit))
                {
                    end = hit.point;
                    normal = hit.normal;
                    impact = true;
                    var health = hit.collider.GetComponentInParent<HealthArmorSystem>();
                    var vehicleHealth = hit.collider.GetComponentInParent<VehicleHealth>();
                    if (health != null && health.gameObject != gameObject)
                    {
                        var headshot = hit.collider.name.Contains("Head");
                        var pelletDamage = DamageAtDistance(weapon, hit.distance) / pelletCount;
                        if (headshot) pelletDamage *= weapon.headshotMultiplier;
                        pelletHits.TryGetValue(health, out var aggregate);
                        if (!TryAggregateShotgunHit(owner, health, pelletDamage, headshot,
                                ref aggregate.BodyDamage, ref aggregate.HeadDamage)) continue;
                        aggregate.Headshot |= headshot;
                        aggregate.Point = hit.point;
                        pelletHits[health] = aggregate;
                        characterImpact = true;
                        color = headshot ? DamageFeedbackSystem.HeadshotColor : DamageFeedbackSystem.BodyColor;
                    }
                    else if (vehicleHealth != null && vehicleHealth.gameObject != owner)
                    {
                        var pelletDamage = DamageAtDistance(weapon, hit.distance) / pelletCount;
                        vehiclePelletHits.TryGetValue(vehicleHealth, out var accumulated);
                        vehiclePelletHits[vehicleHealth] = accumulated + pelletDamage;
                        color = DamageFeedbackSystem.BodyColor;
                    }
                }
                BulletVisual.Launch(origin, end, WeaponClass.Shotgun, impact, normal, color, characterImpact);
                if (i == 0) farthestEnd = end;
            }

            var shooter = owner != null ? owner.GetComponent<BRParticipant>() : null;
            foreach (var pair in pelletHits)
            {
                var report = pair.Key.ApplyCompositeDamage(pair.Value.BodyDamage, pair.Value.HeadDamage, owner);
                if (report.FinalDamage <= 0f) continue;
                LastShotHit = true;
                LastShotHeadshot |= pair.Value.Headshot;
                LastShotDamage += report.FinalDamage;
                if (shooter != null)
                {
                    shooter.DamageDealt += Mathf.RoundToInt(report.AppliedDamage);
                    if (shooter.IsPlayer) DamageFeedbackSystem.Report(pair.Value.Point, report.FinalDamage, pair.Value.Headshot);
                }
            }
            foreach (var pair in vehiclePelletHits)
            {
                var applied = pair.Key.ApplyDamage(pair.Value, DamageType.Weapon, owner);
                if (applied <= 0f) continue;
                LastShotHit = true;
                LastShotDamage += applied;
                if (shooter != null && shooter.IsPlayer)
                    DamageFeedbackSystem.Report(pair.Key.transform.position, applied, false);
            }
            LastShotEndPoint = farthestEnd;
        }

        private bool TryGetFirstHit(Ray ray, float distance, out RaycastHit firstHit)
        {
            var rayHits = Physics.RaycastAll(ray, distance, hitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(rayHits, RaycastHitDistanceComparer.Instance);
            foreach (var hit in rayHits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<LootPickup>() != null) continue;
                firstHit = hit;
                return true;
            }
            firstHit = default;
            return false;
        }

        public static float DamageAtDistance(WeaponDefinition weapon, float distance)
        {
            if (weapon == null) return 0f;
            var range = Mathf.Max(0.01f, weapon.range);
            var falloffStart = range * Mathf.Clamp(weapon.falloffStartRatio, 0.1f, 1f);
            var ratio = Mathf.InverseLerp(falloffStart, range, Mathf.Max(0f, distance));
            return weapon.damage * Mathf.Lerp(1f, Mathf.Clamp(weapon.minimumDamageMultiplier, 0.1f, 1f), ratio);
        }

        public void ReportProjectileHit(Vector3 point, float damage, bool headshot)
        {
            if (damage <= 0f) return;
            LastShotTime = Time.time;
            LastShotHit = true;
            LastShotHeadshot = headshot;
            LastShotDamage = damage;
            LastShotEndPoint = point;
        }

        public static bool CanDamage(GameObject owner, HealthArmorSystem targetHealth)
        {
            if (targetHealth == null) return false;
            var attacker = owner != null ? owner.GetComponent<BRParticipant>() : null;
            var target = targetHealth.GetComponent<BRParticipant>();
            return attacker == null || target == null || BRMatchRules.AreEnemies(attacker, target);
        }

        public static bool TryAggregateShotgunHit(GameObject owner, HealthArmorSystem targetHealth,
            float pelletDamage, bool headshot, ref float bodyDamage, ref float headDamage)
        {
            if (pelletDamage <= 0f || !CanDamage(owner, targetHealth)) return false;
            if (headshot) headDamage += pelletDamage;
            else bodyDamage += pelletDamage;
            return true;
        }

        public static bool IsAimAssistCandidate(GameObject owner, BRParticipant candidate)
        {
            if (candidate == null || candidate.Health == null || candidate.Health.IsDead
                || candidate.Phase is not (ParticipantPhase.Grounded or ParticipantPhase.Downed)) return false;
            var attacker = owner != null ? owner.GetComponent<BRParticipant>() : null;
            return attacker == null ? candidate.gameObject != owner : BRMatchRules.AreEnemies(attacker, candidate);
        }

        public void TryReload()
        {
            if (!OwnerCanUseWeapons()) return;
            var weapon = inventory.ActiveWeapon;
            if (weapon == null || reloading || Magazine >= weapon.magazineSize) return;
            var needed = weapon.magazineSize - Magazine;
            if (inventory.AmmoFor(weapon.ammoKind) <= 0) return;
            reloadRoutine = StartCoroutine(ReloadRoutine(weapon, needed));
        }

        private IEnumerator ReloadRoutine(WeaponDefinition weapon, int requested)
        {
            reloading = true;
            trigger.Reset(weapon);
            reloadStartedAt = Time.time;
            reloadDuration = weapon.reloadSeconds;
            audioEmitter ??= GetComponent<WeaponAudioEmitter>();
            audioEmitter?.PlayReload(weapon.weaponClass);
            yield return new WaitForSeconds(weapon.reloadSeconds);
            var loaded = 0;
            while (loaded < requested && inventory.TryConsumeAmmo(weapon.ammoKind, 1))
            {
                loaded++;
            }
            if (inventory.ActiveWeapon == weapon) magazines[weapon] = Magazine + loaded;
            reloading = false;
            reloadRoutine = null;
        }

        public void SwapWeapon()
        {
            if (!OwnerCanUseWeapons()) return;
            CancelReload();
            inventory?.SwapWeapon();
        }

        public void SelectWeapon(int index)
        {
            if (!OwnerCanUseWeapons()) return;
            CancelReload();
            inventory?.SelectWeapon(index);
        }

        private void CancelReload()
        {
            if (reloadRoutine != null) StopCoroutine(reloadRoutine);
            reloadRoutine = null;
            reloading = false;
            trigger.Reset(inventory != null ? inventory.ActiveWeapon : null);
        }

        public void CancelReloadForAction() => CancelReload();

        private void SpawnShotFeedback(Vector3 endPoint, WeaponDefinition weapon, Camera camera,
            bool hasImpact, Vector3 impactNormal, Color impactColor, bool hitCharacter)
        {
            var visual = GetComponent<ParticipantWeaponVisual>();
            var origin = visual != null ? visual.MuzzlePosition : CharacterPresentationProfile.FallbackMuzzle(transform);
            if (weapon.weaponClass != WeaponClass.Shotgun
                && weapon.simulationType != WeaponSimulationType.Projectile)
                BulletVisual.Launch(origin, endPoint, weapon.weaponClass, hasImpact, impactNormal, impactColor, hitCharacter);
            ShellCasingVisual.Eject(origin, transform.right, transform.forward, weapon.weaponClass);

            CombatEffectPool.SpawnMuzzle(origin);
            if (camera != null)
            {
                var recoil = inventory != null ? inventory.RecoilMultiplier : 1f;
                var step = GameplayTuning.RecoilStep(burstShotIndex, weapon.recoilPitch, weapon.recoilYaw) * recoil;
                camera.GetComponent<ThirdPersonCamera>()?.AddRecoil(step.x, step.y);
            }
        }

        private struct PelletHit
        {
            public float BodyDamage;
            public float HeadDamage;
            public bool Headshot;
            public Vector3 Point;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShotCounter() => TotalShotsFired = 0;
    }

    internal sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new();

        public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
    }
}
