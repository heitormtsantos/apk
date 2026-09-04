using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class InventorySystem : MonoBehaviour
    {
        public const int MaxAmmoPerKind = 240;
        public const int MaxMedKits = 5;
        public const int MaxGlooWalls = 5;
        public const int MaxGrenades = 4;

        private readonly Dictionary<AmmoKind, int> ammo = new();
        private readonly List<WeaponDefinition> weapons = new(2);
        private int activeWeaponIndex;
        [SerializeField] private BackpackConfig backpackConfig;
        private bool cancelHealing;
        private float healStartedAt;
        private const float HealDuration = 3f;

        public event Action Changed;

        public int MedKits { get; private set; }
        public int GlooWalls { get; private set; }
        public int Grenades { get; private set; }
        public int StabilizerLevel { get; private set; }
        public float SpreadMultiplier => 1f - StabilizerLevel * 0.12f;
        public float RecoilMultiplier => 1f - StabilizerLevel * 0.15f;
        public bool UsingMedKit { get; private set; }
        public float HealProgress => UsingMedKit ? Mathf.Clamp01((Time.time - healStartedAt) / HealDuration) : 0f;
        public IReadOnlyList<WeaponDefinition> Weapons => weapons;
        public WeaponDefinition ActiveWeapon => weapons.Count == 0 ? null : weapons[Mathf.Clamp(activeWeaponIndex, 0, weapons.Count - 1)];
        public BackpackConfig BackpackConfig => backpackConfig;
        public float BaseCapacity => backpackConfig != null ? Mathf.Max(1f, backpackConfig.baseCapacity) : 50f;
        public float MaxCapacity => BaseCapacity + (GetComponent<BackpackEquipment>()?.AdditionalCapacity ?? 0f);
        public float CurrentUsage
        {
            get
            {
                var config = EnsureBackpackConfig();
                var usage = 0f;
                foreach (var pair in ammo) usage += pair.Value * config.ammoUnitCost;
                usage += MedKits * config.medKitUnitCost;
                usage += GlooWalls * config.glooWallUnitCost;
                usage += Grenades * config.grenadeUnitCost;
                if (StabilizerLevel > 0) usage += config.attachmentCost;
                return Mathf.Max(0f, usage);
            }
        }
        public float RemainingCapacity => Mathf.Max(0f, MaxCapacity - CurrentUsage);

        private void Awake()
        {
            EnsureBackpackConfig();
            var health = GetComponent<HealthArmorSystem>();
            if (health != null) health.Damaged += OnDamaged;
        }

        public bool CanUseInventory
        {
            get
            {
                var participant = GetComponent<BRParticipant>();
                return participant == null || participant.IsCombatCapable;
            }
        }

        public WeaponDefinition AddWeapon(WeaponDefinition weapon)
        {
            if (weapon == null || !CanUseInventory) return null;
            var existing = weapons.IndexOf(weapon);
            if (existing >= 0)
            {
                if (activeWeaponIndex == existing) return null;
                activeWeaponIndex = existing;
            }
            else if (weapons.Count < 2)
            {
                weapons.Add(weapon);
                activeWeaponIndex = weapons.Count - 1;
            }
            else
            {
                var replaced = weapons[activeWeaponIndex];
                weapons[activeWeaponIndex] = weapon;
                Changed?.Invoke();
                return replaced;
            }
            Changed?.Invoke();
            return null;
        }

        public void AddAmmo(AmmoKind kind, int amount)
        {
            AddAmmoQuantitative(kind, amount);
        }

        public LootTransferResult TryAddAmmo(AmmoKind kind, int amount) => AddAmmoQuantitative(kind, amount);

        public LootTransferResult AddAmmoWithResult(AmmoKind kind, int amount) => AddAmmoQuantitative(kind, amount);

        public LootTransferResult AddAmmoQuantitative(AmmoKind kind, int amount)
        {
            if (amount <= 0) return LootTransferResult.Failed(Mathf.Max(0, amount), "QUANTIDADE INVALIDA");
            if (!CanUseInventory) return LootTransferResult.Failed(amount, "INVENTARIO INDISPONIVEL");
            ammo.TryGetValue(kind, out var current);
            var accepted = Mathf.Min(amount, Mathf.Max(0, MaxAmmoPerKind - current));
            accepted = Mathf.Min(accepted, CapacityLimitedCount(EnsureBackpackConfig().ammoUnitCost));
            if (accepted <= 0) return LootTransferResult.Failed(amount,
                RemainingCapacity + 0.001f < EnsureBackpackConfig().ammoUnitCost ? "MOCHILA CHEIA" : "MUNICAO NO LIMITE");
            ammo[kind] = current + accepted;
            Changed?.Invoke();
            return LootTransferResult.Transferred(accepted, amount - accepted);
        }

        public bool TryConsumeAmmo(AmmoKind kind, int amount)
        {
            if (!CanUseInventory || amount <= 0) return false;
            ammo.TryGetValue(kind, out var current);
            if (current < amount) return false;
            ammo[kind] = current - amount;
            Changed?.Invoke();
            return true;
        }

        public int AmmoFor(AmmoKind kind) => ammo.TryGetValue(kind, out var current) ? current : 0;

        public bool CanAddAmmo(AmmoKind kind, int amount, bool requireFullPackage = true)
        {
            if (!CanUseInventory || amount <= 0) return false;
            var capacity = Mathf.Min(Mathf.Max(0, MaxAmmoPerKind - AmmoFor(kind)),
                CapacityLimitedCount(EnsureBackpackConfig().ammoUnitCost));
            return requireFullPackage ? capacity >= amount : capacity > 0;
        }

        public Dictionary<AmmoKind, int> GetAmmoSnapshot() => new(ammo);

        public void DrainDroppableContents()
        {
            weapons.Clear();
            ammo.Clear();
            activeWeaponIndex = 0;
            MedKits = 0;
            GlooWalls = 0;
            Grenades = 0;
            StabilizerLevel = 0;
            UsingMedKit = false;
            cancelHealing = true;
            Changed?.Invoke();
        }

        public void AddMedKit(int amount)
        {
            AddMedKitsQuantitative(amount);
        }

        public LootTransferResult TryAddMedKits(int amount) => AddMedKitsQuantitative(amount);

        public LootTransferResult AddMedKitsWithResult(int amount) => AddMedKitsQuantitative(amount);

        public LootTransferResult AddMedKitsQuantitative(int amount)
        {
            if (amount <= 0) return LootTransferResult.Failed(Mathf.Max(0, amount), "QUANTIDADE INVALIDA");
            if (!CanUseInventory) return LootTransferResult.Failed(amount, "INVENTARIO INDISPONIVEL");
            var accepted = Mathf.Min(amount, Mathf.Max(0, MaxMedKits - MedKits));
            accepted = Mathf.Min(accepted, CapacityLimitedCount(EnsureBackpackConfig().medKitUnitCost));
            if (accepted <= 0) return LootTransferResult.Failed(amount,
                RemainingCapacity + 0.001f < EnsureBackpackConfig().medKitUnitCost ? "MOCHILA CHEIA" : "KITS MEDICOS NO LIMITE");
            MedKits += accepted;
            Changed?.Invoke();
            return LootTransferResult.Transferred(accepted, amount - accepted);
        }

        public bool TryAddStabilizer(int amount = 1)
        {
            if (!CanUseInventory) return false;
            if (StabilizerLevel == 0 && RemainingCapacity + 0.001f < EnsureBackpackConfig().attachmentCost)
                return false;
            var next = Mathf.Clamp(StabilizerLevel + Mathf.Max(0, amount), 0, 3);
            if (next <= StabilizerLevel) return false;
            StabilizerLevel = next;
            Changed?.Invoke();
            return true;
        }

        public bool CanAddMedKits(int amount, bool requireFullPackage = true)
        {
            if (!CanUseInventory || amount <= 0) return false;
            var capacity = Mathf.Min(Mathf.Max(0, MaxMedKits - MedKits),
                CapacityLimitedCount(EnsureBackpackConfig().medKitUnitCost));
            return requireFullPackage ? capacity >= amount : capacity > 0;
        }

        public LootTransferResult AddGlooWallsQuantitative(int amount)
        {
            if (amount <= 0) return LootTransferResult.Failed(Mathf.Max(0, amount), "QUANTIDADE INVALIDA");
            if (!CanUseInventory) return LootTransferResult.Failed(amount, "INVENTARIO INDISPONIVEL");
            var accepted = Mathf.Min(amount, Mathf.Max(0, MaxGlooWalls - GlooWalls));
            accepted = Mathf.Min(accepted, CapacityLimitedCount(EnsureBackpackConfig().glooWallUnitCost));
            if (accepted <= 0) return LootTransferResult.Failed(amount,
                RemainingCapacity + 0.001f < EnsureBackpackConfig().glooWallUnitCost ? "MOCHILA CHEIA" : "PAREDE DE GEL NO LIMITE");
            GlooWalls += accepted;
            Changed?.Invoke();
            return LootTransferResult.Transferred(accepted, amount - accepted);
        }

        public LootTransferResult AddGrenadesQuantitative(int amount)
        {
            if (amount <= 0) return LootTransferResult.Failed(Mathf.Max(0, amount), "QUANTIDADE INVALIDA");
            if (!CanUseInventory) return LootTransferResult.Failed(amount, "INVENTARIO INDISPONIVEL");
            var accepted = Mathf.Min(amount, Mathf.Max(0, MaxGrenades - Grenades));
            accepted = Mathf.Min(accepted, CapacityLimitedCount(EnsureBackpackConfig().grenadeUnitCost));
            if (accepted <= 0) return LootTransferResult.Failed(amount,
                RemainingCapacity + 0.001f < EnsureBackpackConfig().grenadeUnitCost ? "MOCHILA CHEIA" : "GRANADAS NO LIMITE");
            Grenades += accepted;
            Changed?.Invoke();
            return LootTransferResult.Transferred(accepted, amount - accepted);
        }

        public bool CanAddGlooWalls(int amount, bool requireFullPackage = true)
        {
            var capacity = Mathf.Min(Mathf.Max(0, MaxGlooWalls - GlooWalls),
                CapacityLimitedCount(EnsureBackpackConfig().glooWallUnitCost));
            return CanUseInventory && amount > 0 && (requireFullPackage ? capacity >= amount : capacity > 0);
        }

        public bool CanAddGrenades(int amount, bool requireFullPackage = true)
        {
            var capacity = Mathf.Min(Mathf.Max(0, MaxGrenades - Grenades),
                CapacityLimitedCount(EnsureBackpackConfig().grenadeUnitCost));
            return CanUseInventory && amount > 0 && (requireFullPackage ? capacity >= amount : capacity > 0);
        }

        public bool TryEquipStabilizerLevel(int level)
        {
            if (!CanUseInventory) return false;
            if (StabilizerLevel == 0 && RemainingCapacity + 0.001f < EnsureBackpackConfig().attachmentCost)
                return false;
            var next = Mathf.Clamp(level, 0, 3);
            if (next <= StabilizerLevel) return false;
            StabilizerLevel = next;
            Changed?.Invoke();
            return true;
        }

        public bool TryUseMedKit(HealthArmorSystem health)
        {
            if (!CanUseInventory || UsingMedKit || MedKits <= 0 || health == null
                || health.Health >= health.MaxHealth || health.IsDead || health.IsDowned) return false;
            StartCoroutine(UseMedKitRoutine(health));
            return true;
        }

        private IEnumerator UseMedKitRoutine(HealthArmorSystem health)
        {
            UsingMedKit = true;
            cancelHealing = false;
            healStartedAt = Time.time;
            var weapon = GetComponent<WeaponSystem>();
            var shotTimeAtStart = weapon != null ? weapon.LastShotTime : -10f;
            Changed?.Invoke();
            while (Time.time - healStartedAt < HealDuration)
            {
                if (cancelHealing || health == null || health.IsDead || health.IsDowned
                    || (weapon != null && weapon.LastShotTime > shotTimeAtStart))
                {
                    UsingMedKit = false;
                    Changed?.Invoke();
                    yield break;
                }
                yield return null;
            }
            MedKits = Mathf.Max(0, MedKits - 1);
            health.Heal(45f);
            UsingMedKit = false;
            Changed?.Invoke();
        }

        private void OnDamaged(HealthArmorSystem health, DamageReport report) => cancelHealing = true;

        private void OnDestroy()
        {
            var health = GetComponent<HealthArmorSystem>();
            if (health != null) health.Damaged -= OnDamaged;
        }

        public void SwapWeapon()
        {
            if (!CanUseInventory) return;
            if (weapons.Count > 1)
            {
                activeWeaponIndex = (activeWeaponIndex + 1) % weapons.Count;
                Changed?.Invoke();
            }
        }

        public bool SelectWeapon(int index)
        {
            if (!CanUseInventory) return false;
            if (index < 0 || index >= weapons.Count || index == activeWeaponIndex) return false;
            activeWeaponIndex = index;
            Changed?.Invoke();
            return true;
        }

        public void ConfigureBackpackCapacity(BackpackConfig config)
        {
            backpackConfig = config != null ? config : BackpackConfig.LoadOrRuntimeDefault();
            Changed?.Invoke();
        }

        public void NotifyEquipmentChanged() => Changed?.Invoke();

        public bool TryRemoveAmmo(AmmoKind kind, int requested, out int removed)
        {
            removed = 0;
            if (requested <= 0 || !CanUseInventory) return false;
            ammo.TryGetValue(kind, out var current);
            removed = Mathf.Min(current, requested);
            if (removed <= 0) return false;
            var remaining = current - removed;
            if (remaining > 0) ammo[kind] = remaining;
            else ammo.Remove(kind);
            Changed?.Invoke();
            return true;
        }

        public bool TryRemoveMedKits(int requested, out int removed)
        {
            removed = 0;
            if (requested <= 0 || !CanUseInventory) return false;
            removed = Mathf.Min(MedKits, requested);
            if (removed <= 0) return false;
            MedKits -= removed;
            Changed?.Invoke();
            return true;
        }

        public bool TryRemoveGlooWalls(int requested, out int removed)
        {
            removed = 0;
            if (requested <= 0 || !CanUseInventory) return false;
            removed = Mathf.Min(GlooWalls, requested);
            if (removed <= 0) return false;
            GlooWalls -= removed;
            Changed?.Invoke();
            return true;
        }

        public bool TryRemoveGrenades(int requested, out int removed)
        {
            removed = 0;
            if (requested <= 0 || !CanUseInventory) return false;
            removed = Mathf.Min(Grenades, requested);
            if (removed <= 0) return false;
            Grenades -= removed;
            Changed?.Invoke();
            return true;
        }

        private int CapacityLimitedCount(float unitCost)
        {
            if (unitCost <= 0.0001f) return int.MaxValue;
            return Mathf.Max(0, Mathf.FloorToInt((RemainingCapacity + 0.0001f) / unitCost));
        }

        private BackpackConfig EnsureBackpackConfig()
        {
            if (backpackConfig == null) backpackConfig = BackpackConfig.LoadOrRuntimeDefault();
            return backpackConfig;
        }
    }
}
