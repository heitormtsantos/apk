using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class DeathLootContainer : MonoBehaviour
    {
        private static readonly List<DeathLootContainer> Registry = new();
        private static readonly Stack<DeathLootContainer> Pool = new();
        private readonly List<DeathLootEntry> entries = new();
        private int nextEntryId = 1;
        private float expiresAt;
        private bool destroyWhenEmpty = true;
        private bool pooled;
        private bool visualBuilt;
        private bool hadLoot;

        public IReadOnlyList<DeathLootEntry> Entries => entries;
        public string OwnerName { get; private set; }
        public bool IsOpen { get; private set; }
        public bool Empty => entries.Count == 0;
        public float LifetimeRemaining => Mathf.Max(0f, expiresAt - Time.time);

        public static DeathLootContainer Create(BRParticipant victim)
            => Create(victim, null);

        public static DeathLootContainer Create(BRParticipant victim, BattleRoyaleLifeConfig config)
        {
            if (victim == null) return null;
            DeathLootContainer container = null;
            while (Pool.Count > 0 && container == null) container = Pool.Pop();
            var root = container != null ? container.gameObject : new GameObject();
            if (container == null) container = root.AddComponent<DeathLootContainer>();
            container.pooled = false;
            container.entries.Clear();
            container.nextEntryId = 1;
            container.IsOpen = false;
            container.hadLoot = false;
            root.name = $"Death Loot - {victim.DisplayName}";
            root.transform.position = ProjectToGround(victim.transform.position);
            root.SetActive(true);
            var collider = root.GetComponent<BoxCollider>();
            if (collider == null) collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 0.17f, 0f);
            collider.size = new Vector3(0.68f, 0.34f, 0.58f);

            container.OwnerName = victim.DisplayName;
            container.destroyWhenEmpty = config == null || config.DestroyWhenEmpty;
            container.expiresAt = Time.time + (config != null ? config.DeathBoxLifetime : 180f);
            container.Snapshot(victim, config != null && config.DropMatchCurrency);
            container.hadLoot = !container.Empty;
            victim.Inventory.DrainDroppableContents();
            var backpackConfig = victim.Inventory.BackpackConfig;
            if (backpackConfig == null || backpackConfig.dropBackpackOnDeath)
                victim.Backpack?.RemoveOnDeath();
            if (config != null && config.DropMatchCurrency) victim.Wallet?.ResetCurrency();
            if (container.Empty)
            {
                container.RefreshLifecycle();
                return container;
            }
            if (!container.visualBuilt) container.BuildVisual();
            return container;
        }

        public static DeathLootContainer CreateAirdrop(Vector3 position,
            IReadOnlyList<WeaponDefinition> weapons, int seed, float lifetime = 240f)
        {
            DeathLootContainer container = null;
            while (Pool.Count > 0 && container == null) container = Pool.Pop();
            var root = container != null ? container.gameObject : new GameObject("Airdrop Loot");
            if (container == null) container = root.AddComponent<DeathLootContainer>();
            root.name = "Airdrop Loot";
            root.transform.position = ProjectToGround(position);
            root.SetActive(true);
            var collider = root.GetComponent<BoxCollider>();
            if (collider == null) collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 0.22f, 0f);
            collider.size = new Vector3(0.92f, 0.44f, 0.78f);
            container.entries.Clear();
            container.nextEntryId = 1;
            container.OwnerName = "Airdrop";
            container.IsOpen = false;
            container.destroyWhenEmpty = true;
            container.expiresAt = Time.time + Mathf.Max(30f, lifetime);
            container.pooled = false;
            container.hadLoot = true;
            var random = new System.Random(seed);
            if (weapons != null && weapons.Count > 0)
            {
                var weapon = weapons[random.Next(weapons.Count)];
                container.AddEntry(DeathLootEntry.WeaponEntry(weapon, weapon.magazineSize));
                container.AddEntry(DeathLootEntry.AmmoEntry(weapon.ammoKind,
                    Mathf.Max(30, weapon.magazineSize * 2)));
            }
            container.AddEntry(DeathLootEntry.MedKitEntry(2));
            container.AddEntry(DeathLootEntry.QuantityEntry(DeathLootKind.GlooWall, 3, "Parede de gel"));
            container.AddEntry(DeathLootEntry.ArmorEntry(DeathLootKind.Vest, 4));
            container.AddEntry(DeathLootEntry.ArmorEntry(DeathLootKind.Helmet, 3));
            var backpack = BackpackConfig.LoadOrRuntimeDefault().Roll(random, true);
            if (backpack != null) container.AddEntry(DeathLootEntry.BackpackEntry(backpack));
            container.BuildVisual();
            return container;
        }

        private void Update()
        {
            if (!pooled && expiresAt > 0f && Time.time >= expiresAt) ReleaseToPool();
        }

        public static DeathLootContainer FindNearest(Vector3 position, float radius)
        {
            DeathLootContainer best = null;
            var bestSqr = radius * radius;
            foreach (var container in Registry)
            {
                if (container == null || container.Empty) continue;
                var sqr = (container.transform.position - position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = container;
            }
            return best;
        }

        public static void ClearAll()
        {
            var containers = FindObjectsByType<DeathLootContainer>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var container in containers)
            {
                if (container == null) continue;
                if (Application.isPlaying) Destroy(container.gameObject);
                else DestroyImmediate(container.gameObject);
            }
            Registry.Clear();
            Pool.Clear();
        }

        public void SetOpen(bool open) => IsOpen = open && !Empty;

        public bool Take(int index, BRParticipant receiver)
        {
            if (index < 0 || index >= entries.Count) return false;
            return TakeById(entries[index].EntryId, receiver).Success;
        }

        public LootTransferResult TakeById(int entryId, BRParticipant receiver)
        {
            var index = FindEntryIndex(entryId);
            if (index < 0) return LootTransferResult.Failed(0, "ENTRADA INDISPONIVEL");
            var entry = entries[index];
            if (receiver == null || receiver.Inventory == null || receiver.Health == null
                || receiver.Weapon == null || !receiver.IsCombatCapable)
                return LootTransferResult.Failed(entry.TransferAmount, "JOGADOR INDISPONIVEL");

            WeaponDefinition swappedWeapon = null;
            var swappedMagazine = 0;
            if (entry.Kind == DeathLootKind.Weapon && entry.Weapon != null && receiver.Inventory.Weapons.Count >= 2)
            {
                var alreadyOwned = false;
                foreach (var owned in receiver.Inventory.Weapons)
                    if (owned == entry.Weapon) alreadyOwned = true;
                if (!alreadyOwned)
                {
                    swappedWeapon = receiver.Inventory.ActiveWeapon;
                    swappedMagazine = receiver.Weapon.MagazineFor(swappedWeapon);
                }
            }

            if (!CanAcceptEntry(entry, receiver, out var failureReason))
                return LootTransferResult.Failed(entry.TransferAmount, failureReason);

            if (entry.Kind == DeathLootKind.Ammo)
            {
                var result = receiver.Inventory.AddAmmoQuantitative(entry.AmmoKind, entry.Amount);
                return ApplyQuantityTransfer(index, entry, result);
            }
            if (entry.Kind == DeathLootKind.MedKit)
            {
                var result = receiver.Inventory.AddMedKitsQuantitative(entry.Amount);
                return ApplyQuantityTransfer(index, entry, result);
            }
            if (entry.Kind == DeathLootKind.GlooWall)
                return ApplyQuantityTransfer(index, entry,
                    receiver.Inventory.AddGlooWallsQuantitative(entry.Amount));
            if (entry.Kind == DeathLootKind.Grenade)
                return ApplyQuantityTransfer(index, entry,
                    receiver.Inventory.AddGrenadesQuantitative(entry.Amount));
            if (entry.Kind == DeathLootKind.Currency)
            {
                receiver.Wallet.AddCurrency(entry.Amount);
                entries.RemoveAt(index);
                RefreshLifecycle();
                return LootTransferResult.Transferred(entry.Amount, 0);
            }
            if (entry.Kind == DeathLootKind.Backpack)
            {
                var result = receiver.Backpack != null
                    ? receiver.Backpack.TryEquipCollectedItem(entry.Backpack, out _)
                    : BackpackEquipResult.InventoryUnavailable;
                if (result != BackpackEquipResult.Equipped)
                    return LootTransferResult.Failed(1, result == BackpackEquipResult.SameOrLowerLevel
                        ? "MOCHILA EQUIPADA E MELHOR" : "TRANSFERENCIA RECUSADA");
                entries.RemoveAt(index);
                RefreshLifecycle();
                return LootTransferResult.Transferred(1, 0);
            }

            var accepted = entry.Kind switch
            {
                DeathLootKind.Weapon => receiver.Weapon.EquipFromDeathLoot(entry.Weapon, entry.LoadedMagazine),
                DeathLootKind.Vest => EquipVest(receiver, entry.Level),
                DeathLootKind.Helmet => EquipHelmet(receiver, entry.Level),
                DeathLootKind.Attachment => receiver.Inventory.TryEquipStabilizerLevel(entry.Level),
                _ => false
            };
            if (!accepted) return LootTransferResult.Failed(entry.TransferAmount, "TRANSFERENCIA RECUSADA");
            if (swappedWeapon != null) entries[index] = CreateEntry(DeathLootEntry.WeaponEntry(swappedWeapon, swappedMagazine));
            else entries.RemoveAt(index);
            RefreshLifecycle();
            return LootTransferResult.Transferred(entry.TransferAmount, 0);
        }

        public int TakeAll(BRParticipant receiver) => TakeAllResults(receiver).FindAll(result => result.Success).Count;

        public List<LootTransferResult> TakeAllResults(BRParticipant receiver)
        {
            var ids = new List<int>(entries.Count);
            foreach (var entry in entries) ids.Add(entry.EntryId);
            var results = new List<LootTransferResult>(ids.Count);
            foreach (var id in ids) results.Add(TakeById(id, receiver));
            return results;
        }

        public bool TryTakeBest(BRParticipant receiver)
        {
            if (receiver == null || entries.Count == 0) return false;
            var bestIndex = -1;
            var bestScore = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var score = ScoreFor(entries[i], receiver);
                if (score <= bestScore) continue;
                bestScore = score;
                bestIndex = i;
            }
            return bestIndex >= 0 && TakeById(entries[bestIndex].EntryId, receiver).Success;
        }

        private static int ScoreFor(DeathLootEntry entry, BRParticipant receiver)
        {
            var activeWeapon = receiver.Inventory.ActiveWeapon;
            return entry.Kind switch
            {
                DeathLootKind.Weapon when receiver.Inventory.Weapons.Count == 0 => 120,
                DeathLootKind.Weapon when receiver.Inventory.Weapons.Count < 2 => 55,
                DeathLootKind.Ammo when activeWeapon != null && entry.AmmoKind == activeWeapon.ammoKind
                    && receiver.Inventory.AmmoFor(entry.AmmoKind) < activeWeapon.magazineSize * 2 => 110,
                DeathLootKind.Ammo when receiver.Inventory.AmmoFor(entry.AmmoKind) < 90 => 42,
                DeathLootKind.MedKit when receiver.Inventory.MedKits < 3 => 85,
                DeathLootKind.GlooWall when receiver.Inventory.GlooWalls < InventorySystem.MaxGlooWalls => 82,
                DeathLootKind.Grenade when receiver.Inventory.Grenades < InventorySystem.MaxGrenades => 68,
                DeathLootKind.Currency => 36,
                DeathLootKind.Vest when entry.Level > receiver.Health.VestLevel => 96,
                DeathLootKind.Helmet when entry.Level > receiver.Health.HelmetLevel => 94,
                DeathLootKind.Attachment when entry.Level > receiver.Inventory.StabilizerLevel => 72,
                DeathLootKind.Backpack when receiver.Backpack != null
                    && entry.Backpack != null && entry.Backpack.level > receiver.Backpack.CurrentLevel => 104,
                _ => 0
            };
        }

        private void Snapshot(BRParticipant victim, bool includeCurrency)
        {
            foreach (var weapon in victim.Inventory.Weapons)
                if (weapon != null) AddEntry(DeathLootEntry.WeaponEntry(weapon, victim.Weapon.MagazineFor(weapon)));
            var ammo = victim.Inventory.GetAmmoSnapshot();
            foreach (var pair in ammo)
                if (pair.Value > 0) AddEntry(DeathLootEntry.AmmoEntry(pair.Key, pair.Value));
            if (victim.Inventory.MedKits > 0) AddEntry(DeathLootEntry.MedKitEntry(victim.Inventory.MedKits));
            if (victim.Inventory.GlooWalls > 0) AddEntry(DeathLootEntry.QuantityEntry(
                DeathLootKind.GlooWall, victim.Inventory.GlooWalls, "Parede de gel"));
            if (victim.Inventory.Grenades > 0) AddEntry(DeathLootEntry.QuantityEntry(
                DeathLootKind.Grenade, victim.Inventory.Grenades, "Granada"));
            if (includeCurrency && victim.Wallet != null && victim.Wallet.CurrentCurrency > 0)
                AddEntry(DeathLootEntry.QuantityEntry(DeathLootKind.Currency,
                    victim.Wallet.CurrentCurrency, "Creditos"));
            if (victim.Health.VestLevel > 0) AddEntry(DeathLootEntry.ArmorEntry(DeathLootKind.Vest, victim.Health.VestLevel));
            if (victim.Health.HelmetLevel > 0) AddEntry(DeathLootEntry.ArmorEntry(DeathLootKind.Helmet, victim.Health.HelmetLevel));
            if (victim.Inventory.StabilizerLevel > 0) AddEntry(DeathLootEntry.AttachmentEntry(victim.Inventory.StabilizerLevel));
            var backpackConfig = victim.Inventory.BackpackConfig;
            if (victim.Backpack?.Current != null && (backpackConfig == null || backpackConfig.dropBackpackOnDeath))
                AddEntry(DeathLootEntry.BackpackEntry(victim.Backpack.Current));
        }

        private void AddEntry(DeathLootEntry entry) => entries.Add(CreateEntry(entry));

        private DeathLootEntry CreateEntry(DeathLootEntry entry)
        {
            entry.AssignId(nextEntryId++);
            return entry;
        }

        private int FindEntryIndex(int entryId)
        {
            for (var i = 0; i < entries.Count; i++)
                if (entries[i].EntryId == entryId) return i;
            return -1;
        }

        private LootTransferResult ApplyQuantityTransfer(int index, DeathLootEntry entry, LootTransferResult result)
        {
            if (!result.Success) return result;
            if (result.RemainingAmount > 0) entry.SetAmount(result.RemainingAmount);
            else entries.RemoveAt(index);
            RefreshLifecycle();
            return result;
        }

        private void RefreshLifecycle()
        {
            if (entries.Count != 0) return;
            IsOpen = false;
            if (!destroyWhenEmpty) return;
            if (hadLoot) ReleaseToPool();
            else gameObject.SetActive(false);
        }

        private void ReleaseToPool()
        {
            if (pooled) return;
            pooled = true;
            IsOpen = false;
            entries.Clear();
            gameObject.SetActive(false);
            Pool.Push(this);
        }

        private static bool CanAcceptEntry(DeathLootEntry entry, BRParticipant receiver, out string reason)
        {
            reason = string.Empty;
            switch (entry.Kind)
            {
                case DeathLootKind.Weapon:
                    if (entry.Weapon == null) reason = "ARMA INDISPONIVEL";
                    else
                    {
                        foreach (var weapon in receiver.Inventory.Weapons)
                            if (weapon == entry.Weapon) reason = "ARMA JA POSSUIDA";
                    }
                    break;
                case DeathLootKind.Vest when receiver.Health.VestLevel >= entry.Level:
                    reason = "COLETE EQUIPADO E MELHOR";
                    break;
                case DeathLootKind.Helmet when receiver.Health.HelmetLevel >= entry.Level:
                    reason = "CAPACETE EQUIPADO E MELHOR";
                    break;
                case DeathLootKind.Attachment when receiver.Inventory.StabilizerLevel >= entry.Level:
                    reason = "ESTABILIZADOR EQUIPADO E MELHOR";
                    break;
                case DeathLootKind.Backpack when entry.Backpack == null:
                    reason = "MOCHILA INDISPONIVEL";
                    break;
                case DeathLootKind.Backpack when receiver.Backpack == null
                    || receiver.Backpack.CurrentLevel >= entry.Backpack.level:
                    reason = "MOCHILA EQUIPADA E MELHOR";
                    break;
            }
            return string.IsNullOrEmpty(reason);
        }

        private void BuildVisual()
        {
            visualBuilt = true;
            var crate = ImportedGameplayVisuals.InstantiateTextured(
                ImportedGameplayVisuals.DeathCrateModel, ImportedGameplayVisuals.DeathCrateTextureRoot,
                transform, "Imported Futuristic Death Crate", 0.72f, 0.38f);
            if (crate != null)
            {
                ImportedGameplayVisuals.NormalizeLargestDimension(crate, 0.68f);
                ImportedGameplayVisuals.AlignBottomToParent(crate);
            }

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Death Loot Beacon";
            beacon.transform.SetParent(transform, false);
            beacon.transform.localPosition = Vector3.up * 0.78f;
            beacon.transform.localScale = new Vector3(0.025f, 0.42f, 0.025f);
            ReleaseObject(beacon.GetComponent<Collider>());
            beacon.GetComponent<Renderer>().sharedMaterial = BRMaterialFactory.Create("Death Loot Beacon", new Color(1f, 0.58f, 0.08f));
        }

        private static bool EquipVest(BRParticipant receiver, int level)
        {
            if (receiver.Health.VestLevel >= level) return false;
            receiver.Health.EquipVest(level);
            return true;
        }

        private static bool EquipHelmet(BRParticipant receiver, int level)
        {
            if (receiver.Health.HelmetLevel >= level) return false;
            receiver.Health.EquipHelmet(level);
            return true;
        }

        private static Vector3 ProjectToGround(Vector3 position)
        {
            var hits = Physics.RaycastAll(position + Vector3.up * 3f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.GetComponentInParent<BRParticipant>() != null) continue;
                if (hit.collider.name.Contains("Water")) continue;
                return hit.point + Vector3.up * 0.03f;
            }
            return new Vector3(position.x, Mathf.Max(0.03f, position.y), position.z);
        }

        private static void ReleaseObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnEnable()
        {
            pooled = false;
            if (!Registry.Contains(this)) Registry.Add(this);
        }

        private void OnDisable() => Registry.Remove(this);
    }

    public sealed class DeathLootEntry
    {
        public int EntryId { get; private set; }
        public DeathLootKind Kind { get; private set; }
        public WeaponDefinition Weapon { get; private set; }
        public BackpackData Backpack { get; private set; }
        public AmmoKind AmmoKind { get; private set; }
        public int Amount { get; private set; }
        public int Level { get; private set; }
        public int LoadedMagazine { get; private set; }
        public string DisplayName { get; private set; }
        public int TransferAmount => Kind is DeathLootKind.Ammo or DeathLootKind.MedKit
            or DeathLootKind.GlooWall or DeathLootKind.Grenade or DeathLootKind.Currency ? Amount : 1;

        internal void AssignId(int entryId) => EntryId = entryId;

        internal void SetAmount(int value)
        {
            Amount = Mathf.Max(0, value);
            if (Kind == DeathLootKind.Ammo) DisplayName = $"Municao {AmmoKind}  x{Amount}";
            else if (Kind == DeathLootKind.MedKit) DisplayName = $"Kit medico  x{Amount}";
            else if (Kind == DeathLootKind.GlooWall) DisplayName = $"Parede de gel  x{Amount}";
            else if (Kind == DeathLootKind.Grenade) DisplayName = $"Granada  x{Amount}";
            else if (Kind == DeathLootKind.Currency) DisplayName = $"Creditos  x{Amount}";
        }

        public static DeathLootEntry WeaponEntry(WeaponDefinition weapon, int loaded) => new()
        {
            Kind = DeathLootKind.Weapon,
            Weapon = weapon,
            LoadedMagazine = Mathf.Max(0, loaded),
            DisplayName = weapon != null ? $"{weapon.displayName}  {loaded}/{weapon.magazineSize}" : "Arma"
        };

        public static DeathLootEntry AmmoEntry(AmmoKind kind, int amount) => new()
        {
            Kind = DeathLootKind.Ammo,
            AmmoKind = kind,
            Amount = amount,
            DisplayName = $"Municao {kind}  x{amount}"
        };

        public static DeathLootEntry MedKitEntry(int amount) => new()
        {
            Kind = DeathLootKind.MedKit,
            Amount = amount,
            DisplayName = $"Kit medico  x{amount}"
        };

        public static DeathLootEntry ArmorEntry(DeathLootKind kind, int level) => new()
        {
            Kind = kind,
            Level = level,
            DisplayName = kind == DeathLootKind.Vest ? $"Colete nivel {level}" : $"Capacete nivel {level}"
        };

        public static DeathLootEntry AttachmentEntry(int level) => new()
        {
            Kind = DeathLootKind.Attachment,
            Level = Mathf.Clamp(level, 1, 3),
            DisplayName = $"Estabilizador nivel {Mathf.Clamp(level, 1, 3)}"
        };

        public static DeathLootEntry BackpackEntry(BackpackData backpack) => new()
        {
            Kind = DeathLootKind.Backpack,
            Backpack = backpack,
            Level = backpack != null ? backpack.level : 0,
            DisplayName = backpack != null ? backpack.displayName : "Mochila"
        };

        public static DeathLootEntry QuantityEntry(DeathLootKind kind, int amount, string displayName) => new()
        {
            Kind = kind,
            Amount = Mathf.Max(1, amount),
            DisplayName = $"{displayName}  x{Mathf.Max(1, amount)}"
        };
    }

    public enum DeathLootKind
    {
        Weapon,
        Ammo,
        MedKit,
        Vest,
        Helmet,
        Attachment,
        GlooWall,
        Grenade,
        Currency,
        Backpack
    }
}
