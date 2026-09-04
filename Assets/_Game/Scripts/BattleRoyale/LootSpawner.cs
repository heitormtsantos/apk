using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class LootSpawner : MonoBehaviour
    {
        private readonly List<LootPickup> pickups = new();
        private static readonly RaycastHit[] SurfaceHits = new RaycastHit[64];
        private BackpackConfig backpackConfig;
        public IReadOnlyList<LootPickup> Pickups => pickups;

        public void SpawnLoot(BRGameConfig config, IReadOnlyList<WeaponDefinition> weapons,
            MatchEconomyConfig economy = null)
        {
            Clear();
            backpackConfig = BackpackConfig.LoadOrRuntimeDefault();
            config.Normalize();
            var random = new System.Random(config.lootSeed);
            var total = WorldMapGeometry.RecommendedLootCount(PlayableArea.Bounds, config.participantCount);
            var minDistanceSqr = 4f;
            var accepted = new List<Vector3>(total);
            for (var i = 0; i < total; i++)
            {
                var placed = false;
                for (var attempt = 0; attempt < 48 && !placed; attempt++)
                {
                    var pos = PlayableArea.Sample(random, 2f);
                    if (!TryFindLootSurface(pos, out var candidate)) continue;
                    var clear = true;
                    foreach (var previous in accepted)
                    {
                        if ((previous - candidate).sqrMagnitude < minDistanceSqr)
                        {
                            clear = false;
                            break;
                        }
                    }
                    if (!clear) continue;
                    accepted.Add(candidate);
                    SpawnPickup(candidate, random, weapons, economy);
                    placed = true;
                }
                if (!placed) Debug.LogWarning($"LootSpawner: no valid position for item {i} after limited attempts.");
            }
        }

        public LootPickup FindNearest(Vector3 position, float radius)
            => FindNearest(position, radius, null, null);

        public LootPickup FindNearest(Vector3 position, float radius, LootKind kind)
            => FindNearest(position, radius, kind, null);

        public LootPickup FindNearestAmmo(Vector3 position, float radius, AmmoKind ammoKind)
            => FindNearest(position, radius, LootKind.Ammo, ammoKind);

        public LootPickup FindNearestVisible(Vector3 position, float radius, LootKind kind)
            => FindNearest(position, radius, kind, null, true);

        public LootPickup FindNearestVisibleAmmo(Vector3 position, float radius, AmmoKind ammoKind)
            => FindNearest(position, radius, LootKind.Ammo, ammoKind, true);

        public void FillCollectibleCandidates(Vector3 position, float radius, List<LootPickup> results)
        {
            results.Clear();
            var radiusSqr = radius * radius;
            foreach (var pickup in pickups)
            {
                if (pickup == null || pickup.Collected) continue;
                if ((pickup.transform.position - position).sqrMagnitude <= radiusSqr) results.Add(pickup);
            }
            results.Sort((a, b) => ((a.transform.position - position).sqrMagnitude)
                .CompareTo((b.transform.position - position).sqrMagnitude));
        }

        public void RegisterPickup(LootPickup pickup)
        {
            if (pickup != null && !pickups.Contains(pickup)) pickups.Add(pickup);
        }

        public LootPickup SpawnAmmoPickup(Vector3 position, AmmoKind kind, int amount)
        {
            if (amount <= 0) return null;
            var pickup = CreatePickup(position, "Ammo Overflow Pickup");
            pickup.ConfigureAmmo(kind, amount);
            RegisterPickup(pickup);
            return pickup;
        }

        public LootPickup SpawnBackpackPickup(Vector3 position, BackpackData data)
        {
            if (data == null) return null;
            var pickup = CreatePickup(position, $"Backpack Lv{data.level} Pickup");
            pickup.ConfigureBackpack(data);
            RegisterPickup(pickup);
            return pickup;
        }

        public LootPickup SpawnHealPickup(Vector3 position, int amount)
        {
            if (amount <= 0) return null;
            var pickup = CreatePickup(position, "Dropped MedKit");
            pickup.ConfigureHeal(amount);
            RegisterPickup(pickup);
            return pickup;
        }

        public LootPickup SpawnGlooWallPickup(Vector3 position, int amount)
        {
            if (amount <= 0) return null;
            var pickup = CreatePickup(position, "Dropped Gloo Wall");
            pickup.ConfigureGlooWall(amount);
            RegisterPickup(pickup);
            return pickup;
        }

        public LootPickup SpawnGrenadePickup(Vector3 position, int amount)
        {
            if (amount <= 0) return null;
            var pickup = CreatePickup(position, "Dropped Grenade");
            pickup.ConfigureGrenade(amount);
            RegisterPickup(pickup);
            return pickup;
        }

        public LootPickup FindNearestAutoCollectible(Vector3 position, float radius)
        {
            LootPickup best = null;
            var bestSqr = radius * radius;
            foreach (var pickup in pickups)
            {
                if (pickup == null || pickup.Collected || !MatchManager.IsAutoPickupKind(pickup.Kind)) continue;
                var sqr = (pickup.transform.position - position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = pickup;
            }
            return best;
        }

        private LootPickup FindNearest(Vector3 position, float radius, LootKind? kind, AmmoKind? ammoKind,
            bool requireClearLine = false)
        {
            LootPickup best = null;
            var bestSqr = radius * radius;
            foreach (var pickup in pickups)
            {
                if (pickup == null || pickup.Collected) continue;
                if (kind.HasValue && pickup.Kind != kind.Value) continue;
                if (ammoKind.HasValue && !pickup.MatchesAmmo(ammoKind.Value)) continue;
                var sqr = (pickup.transform.position - position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                if (requireClearLine)
                {
                    var start = position + Vector3.up * 1.25f;
                    var end = new Vector3(pickup.transform.position.x, start.y, pickup.transform.position.z);
                    if (Physics.Linecast(start, end, ~0, QueryTriggerInteraction.Ignore)) continue;
                }
                bestSqr = sqr;
                best = pickup;
            }
            return best;
        }

        public void SpawnStarterLoot(Vector3 center, IReadOnlyList<WeaponDefinition> weapons,
            MatchEconomyConfig economy = null)
        {
            var random = new System.Random(1777);
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI * 2f / 12f;
                var radius = 2.8f + (i % 3) * 1.35f;
                var sample = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!TryFindLootSurface(sample, out var grounded)) continue;
                if (i == 0)
                {
                    backpackConfig ??= BackpackConfig.LoadOrRuntimeDefault();
                    var starter = backpackConfig.FindLevel(1);
                    if (starter != null) SpawnBackpackPickup(grounded, starter);
                    else SpawnPickup(grounded, random, weapons, economy);
                }
                else SpawnPickup(grounded, random, weapons, economy);
            }
        }

        private void SpawnPickup(Vector3 position, System.Random random, IReadOnlyList<WeaponDefinition> weapons,
            MatchEconomyConfig economy)
        {
            var pickup = CreatePickup(position, "Loot Pickup");
            var roll = random.NextDouble();
            backpackConfig ??= BackpackConfig.LoadOrRuntimeDefault();
            if (roll < 0.08 && backpackConfig.Roll(random, false) is { } backpack)
                pickup.ConfigureBackpack(backpack);
            else if (roll < 0.30 && weapons.Count > 0) pickup.ConfigureWeapon(weapons[random.Next(weapons.Count)]);
            else if (roll < 0.54) pickup.ConfigureAmmo((AmmoKind)random.Next(4), random.Next(18, 46));
            else if (roll < 0.67) pickup.ConfigureHeal();
            else if (roll < 0.77)
            {
                var minimum = economy != null ? economy.currencyPickupMin : 60;
                var maximum = economy != null ? economy.currencyPickupMax : 180;
                pickup.ConfigureCurrency(random.Next(Mathf.Max(0, minimum), Mathf.Max(minimum, maximum) + 1));
            }
            else if (roll < 0.86) pickup.ConfigureVest(RollArmorLevel(random));
            else if (roll < 0.95) pickup.ConfigureHelmet(RollArmorLevel(random));
            else pickup.ConfigureAttachment();
            RegisterPickup(pickup);
        }

        private static int RollArmorLevel(System.Random random)
        {
            var roll = random.NextDouble();
            if (roll < 0.06) return 4;
            if (roll < 0.24) return 3;
            if (roll < 0.56) return 2;
            return 1;
        }

        private LootPickup CreatePickup(Vector3 position, string objectName)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = objectName;
            obj.transform.SetParent(transform);
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(0.62f, 0.18f, 0.62f);
            var pickupCollider = obj.GetComponent<Collider>();
            if (pickupCollider != null)
            {
                if (Application.isPlaying) Destroy(pickupCollider);
                else DestroyImmediate(pickupCollider);
            }
            return obj.AddComponent<LootPickup>();
        }

        private static bool TryFindLootSurface(Vector3 sample, out Vector3 position)
        {
            var originY = Mathf.Max(42f, PlayableArea.Bounds.max.y + 12f);
            var hitCount = Physics.RaycastNonAlloc(new Vector3(sample.x, originY, sample.z), Vector3.down,
                SurfaceHits, originY + 30f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(SurfaceHits, 0, hitCount, RaycastHitDistanceComparer.Instance);
            for (var i = 0; i < hitCount; i++)
            {
                var hit = SurfaceHits[i];
                if (hit.collider == null || hit.normal.y < 0.72f) continue;
                var objectName = hit.collider.gameObject.name;
                if (objectName.Contains("Water") || objectName.Contains("Technical Ground")) continue;
                if (hit.point.y > PlayableArea.Bounds.max.y + 0.5f) continue;
                if (Physics.CheckSphere(hit.point + Vector3.up * 0.28f, 0.22f, ~0, QueryTriggerInteraction.Ignore)) continue;
                position = hit.point + Vector3.up * 0.16f;
                return true;
            }
            position = default;
            return false;
        }

        private void Clear()
        {
            foreach (var pickup in pickups)
                if (pickup != null) Destroy(pickup.gameObject);
            pickups.Clear();
        }
    }

    public sealed class LootPickup : MonoBehaviour
    {
        private static readonly Dictionary<Color32, Material> BeaconMaterials = new();
        private WeaponDefinition weapon;
        private AmmoKind ammoKind;
        private int amount;
        private int level;
        private int loadedMagazine;
        private int reserveBonus;
        private BackpackData backpackData;
        private GameObject visualModel;

        public LootKind Kind { get; private set; }
        public bool Collected { get; private set; }
        public string DisplayName { get; private set; }
        public string LastFailureReason { get; private set; }
        public WeaponDefinition Weapon => weapon;
        public int Amount => amount;
        public AmmoKind AmmoKind => ammoKind;
        public int Level => level;
        public int LoadedMagazine => loadedMagazine;
        public int ReserveBonus => reserveBonus;
        public BackpackData BackpackData => backpackData;
        public bool MatchesAmmo(AmmoKind kind) => Kind == LootKind.Ammo && ammoKind == kind;

        public void ConfigureWeapon(WeaponDefinition value)
        {
            Kind = LootKind.Weapon;
            weapon = value;
            loadedMagazine = value != null ? Mathf.Max(0, value.magazineSize) : 0;
            reserveBonus = loadedMagazine;
            UpdateWeaponDisplayName();
            Paint(new Color(0.95f, 0.68f, 0.18f));
            var size = value != null && value.weaponClass == WeaponClass.Pistol ? 0.30f : 0.48f;
            AddModel(value != null ? value.modelResource : null, new Vector3(0f, 0.14f, 0f), new Vector3(0f, 90f, 0f), size);
        }

        public void ConfigureAmmo(AmmoKind kind, int count)
        {
            Kind = LootKind.Ammo;
            ammoKind = kind;
            amount = Mathf.Max(0, count);
            UpdateDisplayName();
            Paint(new Color(0.28f, 0.72f, 0.95f));
            AddModel("Quaternius/Props/Crate", new Vector3(0f, 0.2f, 0f), Vector3.zero, 0.48f);
        }

        public void ConfigureHeal(int count = 1)
        {
            Kind = LootKind.Heal;
            amount = Mathf.Max(0, count);
            UpdateDisplayName();
            Paint(new Color(0.24f, 0.95f, 0.48f));
            AddModel("Quaternius/Props/Health", new Vector3(0f, 0.18f, 0f), Vector3.zero, 0.65f);
        }

        public void ConfigureVest(int itemLevel)
        {
            Kind = LootKind.Vest;
            level = itemLevel;
            DisplayName = $"Aegis Vest L{level}";
            Paint(new Color(0.62f, 0.62f, 0.72f));
            AddModel("Quaternius/Props/Crate", new Vector3(0f, 0.2f, 0f), Vector3.zero, 0.58f);
        }

        public void ConfigureHelmet(int itemLevel)
        {
            Kind = LootKind.Helmet;
            level = itemLevel;
            DisplayName = $"Vector Helm L{level}";
            Paint(new Color(0.84f, 0.84f, 0.95f));
            AddModel("Quaternius/Props/Crate", new Vector3(0f, 0.2f, 0f), Vector3.zero, 0.52f);
        }

        public void ConfigureAttachment()
        {
            Kind = LootKind.Attachment;
            DisplayName = "Stabilizer Mod";
            Paint(new Color(0.8f, 0.42f, 1f));
            AddModel("Quaternius/Weapons/Pistol", new Vector3(0f, 0.2f, 0f), new Vector3(0f, 90f, 0f), 0.7f);
        }

        public void ConfigureCurrency(int value)
        {
            Kind = LootKind.Currency;
            amount = Mathf.Max(0, value);
            DisplayName = $"Creditos x{amount}";
            Paint(new Color(1f, 0.72f, 0.12f));
            AddModel("Quaternius/Props/Crate", new Vector3(0f, 0.18f, 0f), Vector3.zero, 0.38f);
        }

        public void ConfigureBackpack(BackpackData data)
        {
            Kind = LootKind.Backpack;
            backpackData = data;
            level = data != null ? Mathf.Clamp(data.level, 1, 3) : 0;
            DisplayName = data != null ? data.displayName : "Mochila";
            var color = level switch
            {
                1 => new Color(0.32f, 0.86f, 0.52f),
                2 => new Color(0.24f, 0.68f, 1f),
                _ => new Color(0.78f, 0.42f, 1f)
            };
            Paint(color);
            AddModel(data != null ? data.worldLootPrefab : null,
                new Vector3(0f, 0.22f, 0f), Vector3.zero, 0.42f);
        }

        public void ConfigureGlooWall(int count)
        {
            Kind = LootKind.GlooWall;
            amount = Mathf.Max(0, count);
            DisplayName = $"Parede de gel x{amount}";
            Paint(new Color(0.25f, 0.86f, 1f));
            AddModel("Quaternius/Props/Crate", new Vector3(0f, 0.18f, 0f), Vector3.zero, 0.42f);
        }

        public void ConfigureGrenade(int count)
        {
            Kind = LootKind.Grenade;
            amount = Mathf.Max(0, count);
            DisplayName = $"Granada x{amount}";
            Paint(new Color(1f, 0.42f, 0.18f));
            AddModel("Quaternius/Weapons/Grenade", new Vector3(0f, 0.2f, 0f), Vector3.zero, 0.38f);
        }

        public bool Collect(BRParticipant participant)
            => CollectQuantitative(participant).Success;

        public LootTransferResult CollectQuantitative(BRParticipant participant)
        {
            LastFailureReason = string.Empty;
            if (Collected) return Fail(0, "ITEM JA COLETADO");
            var inventory = participant != null ? participant.Inventory : null;
            var health = participant != null ? participant.Health : null;
            if (inventory == null || health == null) return Fail(amount, "ITEM INDISPONIVEL");
            if (!participant.IsCombatCapable)
                return Fail(amount, "JOGADOR INDISPONIVEL");

            var accepted = true;
            if (Kind == LootKind.Weapon)
            {
                if (weapon == null) return Fail(1, "ARMA INDISPONIVEL");
                if (!participant.IsPlayer && inventory.Weapons.Count >= 2) accepted = false;
                else
                {
                    var incomingWeapon = weapon;
                    var incomingMagazine = loadedMagazine;
                    var incomingBonus = reserveBonus;
                    WeaponDefinition replaced = null;
                    var replacedMagazine = 0;
                    var alreadyOwned = false;
                    foreach (var owned in inventory.Weapons)
                        if (owned == incomingWeapon) alreadyOwned = true;
                    if (alreadyOwned) return Fail(1, "ARMA JA POSSUIDA");
                    if (inventory.Weapons.Count >= 2)
                    {
                        replaced = inventory.ActiveWeapon;
                        replacedMagazine = participant.Weapon.MagazineFor(replaced);
                    }
                    if (!participant.Weapon.EquipFromDeathLoot(incomingWeapon, incomingMagazine))
                        return Fail(1, "TRANSFERENCIA RECUSADA");
                    var overflow = 0;
                    if (incomingBonus > 0)
                        overflow = inventory.AddAmmoQuantitative(incomingWeapon.ammoKind, incomingBonus).RemainingAmount;
                    if (replaced != null)
                    {
                        ReplaceGroundWeapon(replaced, replacedMagazine,
                            participant.transform.position - participant.transform.forward * 1.1f);
                        PreserveReserveOverflow(incomingWeapon.ammoKind, overflow,
                            participant.transform.position + participant.transform.right * 0.65f);
                        return LootTransferResult.Transferred(1, 0);
                    }
                    PreserveReserveOverflow(incomingWeapon.ammoKind, overflow,
                        participant.transform.position + participant.transform.right * 0.65f);
                }
            }
            else if (Kind == LootKind.Ammo)
            {
                var result = inventory.AddAmmoQuantitative(ammoKind, amount);
                return ApplyQuantityResult(result);
            }
            else if (Kind == LootKind.Heal)
            {
                var result = inventory.AddMedKitsQuantitative(amount);
                return ApplyQuantityResult(result);
            }
            else if (Kind == LootKind.Vest)
            {
                accepted = level > health.VestLevel;
                if (!accepted) LastFailureReason = "COLETE EQUIPADO E MELHOR";
                if (accepted) health.EquipVest(level);
            }
            else if (Kind == LootKind.Helmet)
            {
                accepted = level > health.HelmetLevel;
                if (!accepted) LastFailureReason = "CAPACETE EQUIPADO E MELHOR";
                if (accepted) health.EquipHelmet(level);
            }
            else if (Kind == LootKind.Attachment)
            {
                accepted = inventory.TryAddStabilizer();
                if (!accepted) LastFailureReason = "ESTABILIZADOR NO NIVEL MAXIMO";
            }
            else if (Kind == LootKind.Currency)
            {
                if (amount <= 0 || participant.Wallet == null) return Fail(amount, "CREDITOS INDISPONIVEIS");
                participant.Wallet.AddCurrency(amount);
                Collected = true;
                gameObject.SetActive(false);
                return LootTransferResult.Transferred(amount, 0);
            }
            else if (Kind == LootKind.Backpack)
            {
                if (backpackData == null || participant.Backpack == null)
                    return Fail(1, "MOCHILA INDISPONIVEL");
                var equip = participant.Backpack.TryEquipCollectedItem(backpackData, out var replaced);
                if (equip != BackpackEquipResult.Equipped)
                    return Fail(1, equip == BackpackEquipResult.SameOrLowerLevel
                        ? "MOCHILA EQUIPADA E MELHOR" : "TRANSFERENCIA RECUSADA");
                var config = inventory.BackpackConfig;
                if (replaced != null && config != null && config.dropPreviousBackpack)
                {
                    var spawner = GetComponentInParent<LootSpawner>();
                    spawner?.SpawnBackpackPickup(participant.transform.position - participant.transform.forward,
                        replaced);
                }
            }
            else if (Kind == LootKind.GlooWall)
                return ApplyQuantityResult(inventory.AddGlooWallsQuantitative(amount));
            else if (Kind == LootKind.Grenade)
                return ApplyQuantityResult(inventory.AddGrenadesQuantitative(amount));
            if (!accepted)
            {
                if (string.IsNullOrEmpty(LastFailureReason)) LastFailureReason = "SEM ESPACO PARA ESTE ITEM";
                return LootTransferResult.Failed(Mathf.Max(1, amount), LastFailureReason);
            }
            Collected = true;
            gameObject.SetActive(false);
            return LootTransferResult.Transferred(1, 0);
        }

        private LootTransferResult ApplyQuantityResult(LootTransferResult result)
        {
            if (!result.Success)
            {
                LastFailureReason = result.FailureReason;
                return result;
            }
            amount = result.RemainingAmount;
            UpdateDisplayName();
            if (amount == 0)
            {
                Collected = true;
                gameObject.SetActive(false);
            }
            return result;
        }

        private LootTransferResult Fail(int remaining, string reason)
        {
            LastFailureReason = reason;
            return LootTransferResult.Failed(Mathf.Max(0, remaining), reason);
        }

        private void UpdateDisplayName()
        {
            if (Kind == LootKind.Ammo) DisplayName = $"{ammoKind} Ammo x{amount}";
            else if (Kind == LootKind.Heal) DisplayName = $"Pulse Patch x{amount}";
            else if (Kind == LootKind.GlooWall) DisplayName = $"Parede de gel x{amount}";
            else if (Kind == LootKind.Grenade) DisplayName = $"Granada x{amount}";
        }

        private void Paint(Color color)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Loot Beacon";
            beacon.transform.SetParent(transform, false);
            beacon.transform.localPosition = Vector3.up * 0.86f;
            beacon.transform.localScale = new Vector3(0.08f, 0.55f, 0.08f);
            var collider = beacon.GetComponent<Collider>();
            Release(collider);
            var beaconRenderer = beacon.GetComponent<Renderer>();
            if (beaconRenderer != null) beaconRenderer.sharedMaterial = BeaconMaterial(color);
        }

        private static Material BeaconMaterial(Color color)
        {
            var key = (Color32)color;
            if (BeaconMaterials.TryGetValue(key, out var material) && material != null) return material;
            material = BRMaterialFactory.Create("Loot Beacon Shared", color);
            BeaconMaterials[key] = material;
            return material;
        }

        private void AddModel(string resourcePath, Vector3 localPosition, Vector3 localEuler, float scale)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return;
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null) return;
            var visual = Instantiate(prefab, transform, false);
            visualModel = visual;
            visual.name = $"{DisplayName} Model";
            BRAssetVisuals.PrepareForUrp(visual);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.Euler(localEuler);
            visual.transform.localScale = Vector3.one;
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) Release(collider);
            NormalizeModel(visual.transform, scale);
        }

        private void AddModel(GameObject prefab, Vector3 localPosition, Vector3 localEuler, float scale)
        {
            if (prefab == null) return;
            var visual = Instantiate(prefab, transform, false);
            visualModel = visual;
            visual.name = $"{DisplayName} Model";
            BRAssetVisuals.PrepareForUrp(visual);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.Euler(localEuler);
            visual.transform.localScale = Vector3.one;
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) Release(collider);
            NormalizeModel(visual.transform, scale);
        }

        private void ReplaceGroundWeapon(WeaponDefinition replacement, int replacementMagazine, Vector3 dropPosition)
        {
            weapon = replacement;
            loadedMagazine = replacement != null
                ? Mathf.Clamp(replacementMagazine, 0, replacement.magazineSize)
                : 0;
            reserveBonus = 0;
            UpdateWeaponDisplayName();
            if (visualModel != null) Destroy(visualModel);
            visualModel = null;
            var size = replacement != null && replacement.weaponClass == WeaponClass.Pistol ? 0.30f : 0.48f;
            AddModel(replacement != null ? replacement.modelResource : null, new Vector3(0f, 0.14f, 0f),
                new Vector3(0f, 90f, 0f), size);
            transform.position = dropPosition + Vector3.up * 0.18f;
            Collected = false;
            gameObject.SetActive(true);
        }

        private void UpdateWeaponDisplayName()
        {
            DisplayName = weapon != null
                ? $"{weapon.displayName}  {loadedMagazine}/{weapon.magazineSize}"
                : "Weapon";
        }

        private void PreserveReserveOverflow(AmmoKind kind, int overflow, Vector3 position)
        {
            if (overflow <= 0) return;
            var spawner = GetComponentInParent<LootSpawner>();
            if (spawner != null)
            {
                spawner.SpawnAmmoPickup(position, kind, overflow);
                return;
            }
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Ammo Overflow Pickup";
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(0.62f, 0.18f, 0.62f);
            Release(obj.GetComponent<Collider>());
            obj.AddComponent<LootPickup>().ConfigureAmmo(kind, overflow);
        }

        private static void NormalizeModel(Transform visual, float targetSize)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest > 0.001f) visual.localScale *= targetSize / largest;
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
