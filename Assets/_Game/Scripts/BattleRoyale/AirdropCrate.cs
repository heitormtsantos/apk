using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public enum AirdropState
    {
        Falling,
        Parachuting,
        Landed,
        Released
    }

    public sealed class AirdropCrate : MonoBehaviour
    {
        private static readonly List<AirdropCrate> Registry = new();
        private IReadOnlyList<WeaponDefinition> weapons;
        private AirdropConfig config;
        private int seed;
        private GameObject parachute;

        public static IReadOnlyList<AirdropCrate> Active => Registry;
        public AirdropState State { get; private set; }
        public bool ShowMapMarker => State is AirdropState.Falling or AirdropState.Parachuting or AirdropState.Landed;
        public event Action<AirdropCrate> Landed;

        public void Configure(AirdropConfig settings, IReadOnlyList<WeaponDefinition> weaponCatalog, int lootSeed)
        {
            config = settings;
            weapons = weaponCatalog;
            seed = lootSeed;
            State = AirdropState.Falling;
            BuildCrateVisual();
        }

        private void Update()
        {
            if (State is not (AirdropState.Falling or AirdropState.Parachuting) || config == null) return;
            if (State == AirdropState.Falling && TryGround(out var ground) && transform.position.y - ground.y <= 32f)
            {
                State = AirdropState.Parachuting;
                BuildParachute();
            }
            var speed = State == AirdropState.Parachuting ? config.ParachuteFallSpeed : config.CrateFallSpeed;
            transform.position += Vector3.down * speed * Time.deltaTime;
            if (!TryGround(out ground) || transform.position.y > ground.y + 0.35f) return;
            transform.position = ground + Vector3.up * 0.08f;
            State = AirdropState.Landed;
            if (parachute != null) Destroy(parachute);
            DeathLootContainer.CreateAirdrop(transform.position, weapons, seed, config.CrateLifetime);
            Landed?.Invoke(this);
            State = AirdropState.Released;
            Destroy(gameObject);
        }

        private bool TryGround(out Vector3 ground) => PlayableArea.TryGround(transform.position, out ground, 0f);

        private void BuildCrateVisual()
        {
            var visual = ImportedGameplayVisuals.InstantiateTextured(
                ImportedGameplayVisuals.DeathCrateModel, ImportedGameplayVisuals.DeathCrateTextureRoot,
                transform, "Airdrop Crate Visual", 0.9f, 0.5f);
            if (visual != null)
            {
                ImportedGameplayVisuals.NormalizeLargestDimension(visual, 0.9f);
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) Destroy(collider);
            }
        }

        private void BuildParachute()
        {
            parachute = new GameObject("Airdrop Parachute");
            parachute.transform.SetParent(transform, false);
            parachute.transform.localPosition = Vector3.up * 2.2f;
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.transform.SetParent(parachute.transform, false);
            canopy.transform.localScale = new Vector3(3.2f, 0.45f, 3.2f);
            Destroy(canopy.GetComponent<Collider>());
            canopy.GetComponent<Renderer>().sharedMaterial = BRMaterialFactory.Create(
                "Airdrop Parachute", new Color(0.75f, 0.12f, 0.08f));
        }

        private void OnEnable()
        {
            if (!Registry.Contains(this)) Registry.Add(this);
        }

        private void OnDisable() => Registry.Remove(this);
    }
}
