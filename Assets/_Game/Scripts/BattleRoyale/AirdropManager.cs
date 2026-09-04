using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class AirdropManager : MonoBehaviour
    {
        private readonly List<AirdropCrate> activeDrops = new();
        private MatchManager match;
        private IReadOnlyList<WeaponDefinition> weapons;
        private AirdropConfig config;
        private System.Random random;
        private float nextDropAt;
        private bool scheduled;

        public event Action<AirdropCrate> AirdropSpawned;
        public event Action<AirdropCrate> AirdropLanded;
        public event Action<string> Announcement;
        public IReadOnlyList<AirdropCrate> ActiveDrops => activeDrops;

        public void ResetRuntime()
        {
            scheduled = false;
            nextDropAt = 0f;
            activeDrops.Clear();
            for (var i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
        }

        public void Configure(MatchManager owner, IReadOnlyList<WeaponDefinition> weaponCatalog,
            AirdropConfig settings, int seed)
        {
            match = owner;
            weapons = weaponCatalog;
            config = settings != null ? settings : AirdropConfig.CreateRuntimeDefault();
            random = new System.Random(seed ^ 0x4A1D);
            scheduled = false;
        }

        private void Update()
        {
            for (var i = activeDrops.Count - 1; i >= 0; i--)
                if (activeDrops[i] == null) activeDrops.RemoveAt(i);
            if (match == null || match.State != MatchState.Active)
            {
                scheduled = false;
                return;
            }
            if (!scheduled)
            {
                scheduled = true;
                nextDropAt = Time.time + config.FirstDropDelay;
            }
            if (Time.time < nextDropAt || activeDrops.Count >= config.MaxActiveDrops) return;
            SpawnAirdrop();
            nextDropAt = Time.time + Mathf.Lerp(config.MinInterval, config.MaxInterval,
                (float)random.NextDouble());
        }

        [ContextMenu("Spawn Airdrop")]
        public bool SpawnAirdrop()
        {
            if (config == null || random == null || activeDrops.Count >= config.MaxActiveDrops) return false;
            if (!TrySelectDropPoint(out var target)) return false;
            var area = PlayableArea.Bounds;
            var routeHeight = area.max.y + Mathf.Max(100f, Mathf.Max(area.size.x, area.size.z) * 0.06f);
            var alongX = random.NextDouble() >= 0.5;
            var margin = Mathf.Max(40f, Mathf.Max(area.size.x, area.size.z) * 0.04f);
            var start = alongX
                ? new Vector3(area.min.x - margin, routeHeight, target.z)
                : new Vector3(target.x, routeHeight, area.min.z - margin);
            var end = alongX
                ? new Vector3(area.max.x + margin, routeHeight, target.z)
                : new Vector3(target.x, routeHeight, area.max.z + margin);
            var plane = new GameObject("Airdrop Aircraft").AddComponent<AirdropPlane>();
            plane.transform.SetParent(transform, true);
            plane.Configure(start, end, new Vector3(target.x, routeHeight, target.z), config.PlaneSpeed,
                spawn => ReleaseCrate(spawn, target));
            Announcement?.Invoke("AIRDROP A CAMINHO");
            return true;
        }

        public bool TrySelectDropPoint(out Vector3 point)
        {
            point = default;
            if (random == null) return false;
            for (var attempt = 0; attempt < 64; attempt++)
            {
                var sample = PlayableArea.Sample(random, 30f);
                if (!PlayableArea.TryGround(sample, out var grounded, 0.2f)) continue;
                if (config.RequireSafeZone && match?.SafeZone != null
                    && match.SafeZone.CurrentState != SafeZoneState.Inactive
                    && !match.SafeZone.IsInsideSafeZone(grounded)) continue;
                point = grounded;
                return true;
            }
            return false;
        }

        private void ReleaseCrate(Vector3 spawn, Vector3 target)
        {
            var crateObject = new GameObject("Falling Airdrop Crate");
            crateObject.transform.SetParent(transform, true);
            crateObject.transform.position = new Vector3(target.x, spawn.y, target.z);
            var crate = crateObject.AddComponent<AirdropCrate>();
            crate.Configure(config, weapons, random.Next());
            crate.Landed += OnCrateLanded;
            activeDrops.Add(crate);
            AirdropSpawned?.Invoke(crate);
            Announcement?.Invoke("AIRDROP LIBERADO");
        }

        private void OnCrateLanded(AirdropCrate crate)
        {
            if (crate != null) crate.Landed -= OnCrateLanded;
            AirdropLanded?.Invoke(crate);
            Announcement?.Invoke("AIRDROP DISPONIVEL");
        }
    }
}
