using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class BattleRoyaleBootstrap : MonoBehaviour
    {
        [SerializeField] private BRGameConfig config = new();
        [SerializeField] private SafeZoneConfig safeZoneConfig;
        [SerializeField] private MatchEconomyConfig economyConfig;
        [SerializeField] private MatchShopDatabase matchShopDatabase;
        [SerializeField] private BattleRoyaleLifeConfig lifeConfig;
        [SerializeField] private AirdropConfig airdropConfig;
        [SerializeField] private AccountLevelConfig accountLevelConfig;
        [SerializeField] private BattlePassSeasonData battlePassSeason;
        [SerializeField] private MissionCatalog missionCatalog;
        [SerializeField] private MatchProgressionConfig matchProgressionConfig;
        [SerializeField] private AccountSeasonData accountSeason;

        private readonly List<BRParticipant> participants = new();
        private MatchManager match;

        private void Awake()
        {
            Application.runInBackground = true;
            GamePreferences.LoadAndApply();
            config.Normalize();
            if (safeZoneConfig == null) safeZoneConfig = Resources.Load<SafeZoneConfig>("SafeZone/DefaultSafeZoneConfig");
            if (economyConfig == null) economyConfig = Resources.Load<MatchEconomyConfig>("MatchShop/DefaultMatchEconomy");
            if (matchShopDatabase == null)
                matchShopDatabase = Resources.Load<MatchShopDatabase>("MatchShop/DefaultMatchShopDatabase");
            if (lifeConfig == null) lifeConfig = Resources.Load<BattleRoyaleLifeConfig>("Life/DefaultLifeConfig");
            if (lifeConfig == null) lifeConfig = BattleRoyaleLifeConfig.CreateRuntimeDefault();
            if (airdropConfig == null) airdropConfig = Resources.Load<AirdropConfig>("Airdrop/DefaultAirdropConfig");
            if (airdropConfig == null) airdropConfig = AirdropConfig.CreateRuntimeDefault();
            var accountProfile = GetComponent<PlayerProfileService>();
            if (accountProfile == null) accountProfile = gameObject.AddComponent<PlayerProfileService>();
            accountProfile.Configure(null, accountLevelConfig, battlePassSeason,
                missionCatalog, matchProgressionConfig, accountSeason);
            BuildOriginalTechnicalMap();
            if (safeZoneConfig != null)
            {
                safeZoneConfig = Instantiate(safeZoneConfig);
                safeZoneConfig.hideFlags = HideFlags.DontSave;
                safeZoneConfig.AdaptToPlayableArea(PlayableArea.Bounds);
            }
            var lobbyShowcase = BuildStagingIsland();
            var weapons = CreateWeapons();
            if (matchShopDatabase == null) matchShopDatabase = MatchShopRuntimeBuilder.CreateDefaultCatalog(weapons);
            MatchShopRuntimeBuilder.SpawnDefaultTerminals(matchShopDatabase);
            var loot = new GameObject("LootSpawner").AddComponent<LootSpawner>();
            if (DamageFeedbackSystem.Instance == null)
                new GameObject("Damage Feedback System").AddComponent<DamageFeedbackSystem>();
            var safeZone = new GameObject("SafeZone").AddComponent<SafeZoneController>();
            if (safeZoneConfig != null) safeZone.Configure(safeZoneConfig, config.lootSeed, false);
            else safeZone.Configure(config);
            var drop = new GameObject("DropSystem").AddComponent<DropSystem>();
            drop.Configure(config);
            var cameraRig = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera");
            cameraRig.tag = "MainCamera";
            var camera = cameraRig.GetComponent<Camera>();
            if (camera == null) camera = cameraRig.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.allowHDR = !Application.isMobilePlatform;
            var thirdPerson = cameraRig.GetComponent<ThirdPersonCamera>();
            if (thirdPerson == null) thirdPerson = cameraRig.AddComponent<ThirdPersonCamera>();
            match = gameObject.AddComponent<MatchManager>();
            match.Configure(config, weapons, loot, safeZone, drop, thirdPerson, participants, lobbyShowcase,
                safeZoneConfig, economyConfig, matchShopDatabase, lifeConfig, airdropConfig, accountProfile);
            var vehicleSpawner = gameObject.GetComponent<VehicleRuntimeSpawner>()
                ?? gameObject.AddComponent<VehicleRuntimeSpawner>();
            vehicleSpawner.Configure(match);
            var hud = gameObject.AddComponent<MatchHUD>();
            hud.Configure(match);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (System.Environment.GetCommandLineArgs().Any(argument =>
                    string.Equals(argument, "-brOpeningScan", System.StringComparison.OrdinalIgnoreCase)))
            {
                gameObject.AddComponent<RuntimeMapOpeningDiscovery>();
                return;
            }
            if (System.Environment.GetCommandLineArgs().Any(argument =>
                    string.Equals(argument, "-brSmoke", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(argument, "-brTeamSmoke", System.StringComparison.OrdinalIgnoreCase)))
                gameObject.AddComponent<RuntimeSmokeCapture>();
            if (System.Environment.GetCommandLineArgs().Contains("-brSoak"))
                gameObject.AddComponent<RuntimeSoakCapture>();
            if (System.Environment.GetCommandLineArgs().Contains("-brVehicleSmoke"))
                gameObject.AddComponent<VehicleRuntimeSmokeCapture>();
            if (System.Environment.GetCommandLineArgs().Contains("-brBackpackSmoke"))
                gameObject.AddComponent<BackpackRuntimeSmokeCapture>();
#endif
        }

        private GameObject BuildStagingIsland()
        {
            if (PlayableArea.TryFindOpenGround(PlayableArea.Center, out var staging))
                config.stagingCenter = staging;

            var showcase = new GameObject("Main Lobby Character Showcase");
            showcase.transform.position = config.stagingCenter;
            BuildLobbyHangar(showcase.transform);
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "Lobby Staging Platform";
            platform.transform.SetParent(showcase.transform, false);
            platform.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            platform.transform.localScale = new Vector3(0.92f, 0.008f, 0.92f);
            Destroy(platform.GetComponent<Collider>());
            platform.GetComponent<Renderer>().sharedMaterial = BRMaterialFactory.Create(
                "Lobby Staging Platform", new Color(0.14f, 0.17f, 0.18f));
            var profile = CharacterVisualCatalog.ResolveAvailable(CharacterVisualCatalog.Lobby);
            var characterPrefab = profile.LoadPrefab();
            if (characterPrefab != null)
            {
                var character = Instantiate(characterPrefab, showcase.transform, false);
                character.name = $"Lobby Raven Unit [{profile.Id}]";
                BRAssetVisuals.PrepareForUrp(character);
                if (profile == CharacterVisualCatalog.Lightweight)
                {
                    foreach (var renderer in character.GetComponentsInChildren<Renderer>(true))
                    {
                        var keep = renderer.name is "Body" or "Head" or "ShoulderPad.L" or "ShoulderPad.R";
                        renderer.enabled = keep;
                    }
                }
                BRAssetVisuals.NormalizeHeight(character, profile.TargetHeight);
                AlignModelToGround(character, showcase.transform.position.y);
                BuildLobbyHeldWeapon(character);
                showcase.AddComponent<CharacterVisualAnimator>().Configure(character, profile, true);
            }
            return showcase;
        }

        private void BuildLobbyHangar(Transform showcase)
        {
            var hangar = new GameObject("Raven Lobby Hangar");
            hangar.transform.SetParent(showcase, false);
            var premium = new GameObject("Premium Operations Hangar");
            premium.transform.SetParent(hangar.transform, false);

            var white = BRMaterialFactory.Create("Hangar Ceramic White", new Color(0.68f, 0.73f, 0.75f));
            var graphite = BRMaterialFactory.Create("Hangar Graphite", new Color(0.035f, 0.055f, 0.065f));
            var panel = BRMaterialFactory.Create("Hangar Technical Panel", new Color(0.11f, 0.15f, 0.17f));
            var floor = BRMaterialFactory.Create("Hangar Composite Floor", new Color(0.09f, 0.12f, 0.13f));
            var glass = BRMaterialFactory.Create("Hangar Panoramic Glass", new Color(0.10f, 0.28f, 0.34f));
            var screen = BRMaterialFactory.CreateEmissive("Hangar Data Screen",
                new Color(0.025f, 0.24f, 0.31f), 2.1f);
            var cyan = BRMaterialFactory.CreateEmissive("Hangar Cyan", new Color(0.02f, 0.72f, 0.92f), 3.2f);
            var amber = BRMaterialFactory.CreateEmissive("Hangar Amber", new Color(1f, 0.42f, 0.04f), 2.6f);
            SetLobbyMaterialSurface(white, 0.12f, 0.58f);
            SetLobbyMaterialSurface(graphite, 0.36f, 0.64f);
            SetLobbyMaterialSurface(floor, 0.22f, 0.48f);
            SetLobbyMaterialSurface(glass, 0.52f, 0.78f);

            LobbyPart(premium.transform, "Operations Deck", new Vector3(0f, -0.1f, 0f),
                new Vector3(12.8f, 0.16f, 8.6f), floor);
            LobbyPart(premium.transform, "Rear Architectural Wall", new Vector3(0f, 2.45f, 4.15f),
                new Vector3(12.8f, 5.1f, 0.18f), white);
            LobbyPart(premium.transform, "Rear Graphite Spine", new Vector3(-4.7f, 2.2f, 4.02f),
                new Vector3(1.05f, 3.8f, 0.12f), graphite);
            LobbyPart(premium.transform, "Left Architecture", new Vector3(-6.25f, 2.2f, 0f),
                new Vector3(0.28f, 4.5f, 8.2f), white);
            LobbyPart(premium.transform, "Right Architecture", new Vector3(6.25f, 2.2f, 0f),
                new Vector3(0.28f, 4.5f, 8.2f), white);
            LobbyPart(premium.transform, "Ceiling Canopy", new Vector3(0f, 5.35f, 1.7f),
                new Vector3(12.8f, 0.12f, 4.8f), graphite);

            var windows = new GameObject("Panoramic Window Array");
            windows.transform.SetParent(premium.transform, false);
            for (var i = -2; i <= 2; i++)
            {
                LobbyPart(windows.transform, $"Panoramic Glass {i}", new Vector3(i * 2.15f, 2.35f, 3.88f),
                    new Vector3(1.82f, 2.45f, 0.08f), glass);
                LobbyPart(windows.transform, $"Window Mullion {i}", new Vector3(i * 2.15f - 1.02f, 2.35f, 3.72f),
                    new Vector3(0.08f, 2.8f, 0.12f), graphite);
            }
            LobbyPart(windows.transform, "Window Header", new Vector3(0f, 3.72f, 3.7f),
                new Vector3(10.9f, 0.18f, 0.16f), graphite);
            LobbyPart(windows.transform, "Window Sill", new Vector3(0f, 0.98f, 3.7f),
                new Vector3(10.9f, 0.18f, 0.16f), graphite);

            var runway = new GameObject("Character Presentation Runway");
            runway.transform.SetParent(premium.transform, false);
            LobbyPart(runway.transform, "Runway Inlay", new Vector3(0f, -0.005f, -0.35f),
                new Vector3(3.35f, 0.025f, 7.2f), panel);
            for (var side = -1; side <= 1; side += 2)
            {
                LobbyPart(runway.transform, $"Runway Light {side}", new Vector3(side * 1.78f, 0.02f, -0.35f),
                    new Vector3(0.055f, 0.025f, 7.15f), cyan);
                LobbyPart(premium.transform, $"Ceiling Light Rail {side}", new Vector3(side * 3.65f, 4.28f, 0.25f),
                    new Vector3(0.09f, 0.055f, 5.9f), cyan);
                LobbyPart(premium.transform, $"Angled Support {side}", new Vector3(side * 5.05f, 3.25f, 2.25f),
                    new Vector3(0.28f, 2.5f, 0.34f), graphite, new Vector3(0f, 0f, side * 17f));
            }

            var platformAccent = new GameObject("Animated Platform Accent");
            platformAccent.transform.SetParent(premium.transform, false);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * 45f;
                var radians = angle * Mathf.Deg2Rad;
                LobbyPart(platformAccent.transform, $"Platform Arc {i}",
                    new Vector3(Mathf.Sin(radians) * 1.62f, 0.018f, Mathf.Cos(radians) * 1.62f),
                    new Vector3(0.08f, 0.025f, 0.58f), i % 3 == 0 ? amber : cyan,
                    new Vector3(0f, angle, 0f));
            }

            var command = new GameObject("Command Display Assembly");
            command.transform.SetParent(premium.transform, false);
            LobbyPart(command.transform, "Command Display", new Vector3(-3.05f, 2.25f, 3.64f),
                new Vector3(2.05f, 1.35f, 0.07f), screen);
            LobbyPart(command.transform, "Command Display Header", new Vector3(-3.05f, 2.98f, 3.54f),
                new Vector3(2.28f, 0.07f, 0.08f), cyan);
            var scanLine = LobbyPart(command.transform, "Command Display Scan", new Vector3(-3.05f, 2.25f, 3.48f),
                new Vector3(1.8f, 0.025f, 0.04f), cyan).transform;
            for (var i = -2; i <= 2; i++)
                LobbyPart(command.transform, $"Telemetry Bar {i}", new Vector3(-3.05f + i * 0.31f, 1.95f + Mathf.Abs(i) * 0.12f, 3.47f),
                    new Vector3(0.26f, 0.32f + (2 - Mathf.Abs(i)) * 0.16f, 0.035f), i == 0 ? amber : cyan);

            var props = new GameObject("Lobby Equipment Props");
            props.transform.SetParent(premium.transform, false);
            for (var side = -1; side <= 1; side += 2)
            {
                LobbyPart(props.transform, $"Equipment Bay {side}", new Vector3(side * 4.65f, 1.42f, 2.85f),
                    new Vector3(1.75f, 2.55f, 0.48f), graphite);
                LobbyPart(props.transform, $"Equipment Screen {side}", new Vector3(side * 4.64f, 1.72f, 2.57f),
                    new Vector3(1.32f, 0.78f, 0.035f), screen);
                LobbyPart(props.transform, $"Equipment Accent {side}", new Vector3(side * 4.64f, 2.58f, 2.55f),
                    new Vector3(1.4f, 0.055f, 0.035f), side < 0 ? cyan : amber);
                LobbyPart(props.transform, $"Supply Case {side} A", new Vector3(side * 4.9f, 0.28f, -0.65f),
                    new Vector3(1.2f, 0.55f, 0.8f), panel, new Vector3(0f, side * 8f, 0f));
                LobbyPart(props.transform, $"Supply Case {side} B", new Vector3(side * 4.35f, 0.62f, -0.1f),
                    new Vector3(0.82f, 0.48f, 0.62f), graphite, new Vector3(0f, side * -12f, 0f));
            }

            var weaponDisplay = BuildLobbyWeaponDisplay(props.transform);
            BuildLobbyBackgroundVehicle(premium.transform, graphite, panel, cyan);
            var drone = BuildLobbyDrone(premium.transform, graphite, cyan, amber);
            var hologram = BuildLobbyHologram(premium.transform, cyan, screen);
            BuildLobbyParticles(premium.transform, cyan);
            var key = LobbyLight(premium.transform, "Lobby Key Light", new Vector3(-2.25f, 3.55f, -1.1f),
                new Color(0.76f, 0.91f, 1f), 7.5f, 1.9f);
            LobbyLight(premium.transform, "Lobby Fill Light", new Vector3(2.65f, 2.55f, -0.3f),
                new Color(1f, 0.68f, 0.34f), 6.2f, 1.15f);
            LobbyLight(premium.transform, "Lobby Back Light", new Vector3(0f, 3.05f, 2.8f),
                new Color(0.16f, 0.82f, 1f), 6.8f, 1.35f);
            premium.AddComponent<LobbyEnvironmentPresentation>()
                .Configure(platformAccent.transform, weaponDisplay, scanLine, key,
                    drone.transform, hologram.transform);
        }

        private GameObject LobbyPart(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material, Vector3 rotation = default, PrimitiveType primitive = PrimitiveType.Cube)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = rotation;
            part.transform.localScale = scale;
            Destroy(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static Light LobbyLight(Transform parent, string name, Vector3 position, Color color,
            float range, float intensity)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private static void SetLobbyMaterialSurface(Material material, float metallic, float smoothness)
        {
            if (material == null) return;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        }

        private static Transform BuildLobbyWeaponDisplay(Transform parent)
        {
            var pedestal = new GameObject("Featured Weapon Display");
            pedestal.transform.SetParent(parent, false);
            pedestal.transform.localPosition = new Vector3(4.65f, 1.68f, 2.45f);
            var prefab = Resources.Load<GameObject>("Weapons/TastyTonyScarH/Optimized/SCAR_H_Optimized");
            if (prefab == null) return pedestal.transform;
            var weapon = Instantiate(prefab, pedestal.transform, false);
            weapon.name = "Featured SCAR-H";
            BRAssetVisuals.PrepareForUrp(weapon);
            var renderers = weapon.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                var longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (longest > 0.001f) weapon.transform.localScale *= 1.15f / longest;
            }
            weapon.transform.localRotation = Quaternion.Euler(0f, 20f, 90f);
            return pedestal.transform;
        }

        private static void BuildLobbyHeldWeapon(GameObject character)
        {
            if (character == null) return;
            var animator = character.GetComponentInChildren<Animator>();
            var hand = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand == null) return;
            var prefab = Resources.Load<GameObject>("Weapons/TastyTonyScarH/Optimized/SCAR_H_Optimized");
            if (prefab == null) return;
            var weapon = Instantiate(prefab, hand, false);
            weapon.name = "Lobby Held SCAR-H";
            BRAssetVisuals.PrepareForUrp(weapon);
            RemoveLobbyColliders(weapon);
            NormalizeLongestDimension(weapon, 0.52f);
            weapon.transform.localPosition = new Vector3(0.015f, 0.19f, 0.035f);
            weapon.transform.localRotation = Quaternion.Euler(82f, 3f, 92f);
        }

        private void BuildLobbyBackgroundVehicle(Transform parent, Material graphite, Material panel,
            Material cyan)
        {
            var vehicle = new GameObject("Background Armored Vehicle Bay");
            vehicle.transform.SetParent(parent, false);
            vehicle.transform.localPosition = new Vector3(-3.85f, 0.25f, 2.35f);
            LobbyPart(vehicle.transform, "Vehicle Hull", Vector3.zero,
                new Vector3(2.5f, 0.72f, 1.18f), panel);
            LobbyPart(vehicle.transform, "Vehicle Cabin", new Vector3(0.25f, 0.62f, 0f),
                new Vector3(1.15f, 0.58f, 0.96f), graphite);
            LobbyPart(vehicle.transform, "Vehicle Sensor", new Vector3(0.2f, 1.02f, 0f),
                new Vector3(0.46f, 0.08f, 0.48f), cyan);
            for (var side = -1; side <= 1; side += 2)
            for (var axle = -1; axle <= 1; axle += 2)
                LobbyPart(vehicle.transform, $"Vehicle Wheel {side} {axle}",
                    new Vector3(axle * 0.78f, -0.3f, side * 0.62f), new Vector3(0.42f, 0.18f, 0.42f),
                    graphite, new Vector3(90f, 0f, 0f), PrimitiveType.Cylinder);
        }

        private GameObject BuildLobbyDrone(Transform parent, Material graphite, Material cyan, Material amber)
        {
            var drone = new GameObject("Raven Companion Drone");
            drone.transform.SetParent(parent, false);
            drone.transform.localPosition = new Vector3(1.05f, 1.28f, 0.15f);
            LobbyPart(drone.transform, "Drone Core", Vector3.zero, new Vector3(0.22f, 0.14f, 0.3f),
                graphite, default, PrimitiveType.Sphere);
            LobbyPart(drone.transform, "Drone Eye", new Vector3(0f, 0f, -0.16f),
                new Vector3(0.1f, 0.06f, 0.025f), amber);
            for (var side = -1; side <= 1; side += 2)
            {
                LobbyPart(drone.transform, $"Drone Wing {side}", new Vector3(side * 0.24f, 0f, 0f),
                    new Vector3(0.28f, 0.035f, 0.16f), graphite, new Vector3(0f, 0f, side * 12f));
                LobbyPart(drone.transform, $"Drone Rotor {side}", new Vector3(side * 0.38f, 0.02f, 0f),
                    new Vector3(0.18f, 0.015f, 0.18f), cyan, default, PrimitiveType.Cylinder);
            }
            return drone;
        }

        private GameObject BuildLobbyHologram(Transform parent, Material cyan, Material screen)
        {
            var hologram = new GameObject("Lobby Tactical Hologram");
            hologram.transform.SetParent(parent, false);
            hologram.transform.localPosition = new Vector3(2.9f, 0.68f, 2.15f);
            LobbyPart(hologram.transform, "Hologram Base", Vector3.zero,
                new Vector3(0.72f, 0.05f, 0.72f), screen, default, PrimitiveType.Cylinder);
            LobbyPart(hologram.transform, "Hologram Beacon", new Vector3(0f, 0.42f, 0f),
                new Vector3(0.14f, 0.72f, 0.14f), cyan, default, PrimitiveType.Cylinder);
            return hologram;
        }

        private static void BuildLobbyParticles(Transform parent, Material cyan)
        {
            var particlesObject = new GameObject("Lobby Ambient Particles");
            particlesObject.transform.SetParent(parent, false);
            particlesObject.transform.localPosition = new Vector3(0f, 0.2f, 0.8f);
            var particles = particlesObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.025f);
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.rateOverTime = 5f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 0.5f, 4f);
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(
                new Color(0.1f, 0.82f, 0.95f, 0f), new Color(0.1f, 0.82f, 0.95f, 0.34f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = cyan;
        }

        private static void NormalizeLongestDimension(GameObject root, float target)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (longest > 0.001f) root.transform.localScale *= target / longest;
        }

        private static void RemoveLobbyColliders(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Destroy(collider);
        }

        private static void AlignModelToGround(GameObject model, float rootY)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            model.transform.localPosition += Vector3.up * (rootY - bounds.min.y + 0.015f);
        }

        private static List<WeaponDefinition> CreateWeapons()
        {
            return new List<WeaponDefinition>
            {
                Weapon("Vesper-9", WeaponClass.Rifle, AmmoKind.Medium, 23f, 0.11f, 30, 92f, true,
                    AimPresentationMode.FirstPerson, AimAssistMode.Standard, ScopeType.Scope2X, 2f,
                    ImportedGameplayVisuals.WeaponPrefabRoot + "ScarH"),
                Weapon("Kairo SMG", WeaponClass.Smg, AmmoKind.Light, 16f, 0.075f, 34, 52f, true,
                    AimPresentationMode.FirstPerson, AimAssistMode.Strong, ScopeType.RedDot, 1f,
                    ImportedGameplayVisuals.WeaponPrefabRoot + "Kriss_Vector"),
                Weapon("Bastion-12", WeaponClass.Shotgun, AmmoKind.Shell, 58f, 0.82f, 6, 24f, false,
                    AimPresentationMode.SoftShoulder, AimAssistMode.Standard, ScopeType.None, 1f,
                    ImportedGameplayVisuals.WeaponPrefabRoot + "Spas_12"),
                Weapon("Longveil", WeaponClass.Sniper, AmmoKind.Long, 82f, 1.2f, 5, 160f, false,
                    AimPresentationMode.FirstPerson, AimAssistMode.Standard, ScopeType.Sniper, 8f,
                    ImportedGameplayVisuals.WeaponPrefabRoot + "L115_Awp"),
                Weapon("Mako Sidearm", WeaponClass.Pistol, AmmoKind.Light, 19f, 0.24f, 14, 48f, false,
                    AimPresentationMode.SoftShoulder, AimAssistMode.Standard, ScopeType.None, 1f,
                    ImportedGameplayVisuals.WeaponPrefabRoot + "Glock17")
            };
        }

        private static WeaponDefinition Weapon(string name, WeaponClass cls, AmmoKind ammo, float damage,
            float interval, int magazine, float range, bool automatic, AimPresentationMode presentation,
            AimAssistMode assistMode, ScopeType scopeType, float magnification, string modelResource)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.displayName = name;
            weapon.weaponClass = cls;
            weapon.ammoKind = ammo;
            weapon.damage = damage;
            weapon.fireInterval = interval;
            weapon.magazineSize = magazine;
            weapon.range = range;
            weapon.automatic = automatic;
            weapon.fireMode = automatic ? WeaponFireMode.Automatic : WeaponFireMode.SemiAutomatic;
            weapon.scope = CreateScope(name, scopeType, magnification);
            weapon.ResolvedAimDefinition.presentation = presentation;
            weapon.ResolvedAimDefinition.aimAssistMode = assistMode;
            weapon.ResolvedAimDefinition.hideLocalBodyInAds = presentation == AimPresentationMode.FirstPerson;
            weapon.ResolvedAimDefinition.hideLocalWeaponInAds = presentation == AimPresentationMode.FirstPerson;
            weapon.ResolvedAimDefinition.hipProfile.retainedConeMultiplier = 3f;
            weapon.ResolvedAimDefinition.hipProfile.fovMultiplier = 1.22f;
            weapon.ResolvedAimDefinition.hipProfile.strengthMultiplier = 1.18f;
            weapon.ResolvedAimDefinition.hipProfile.slowdownMultiplier = 0.82f;
            weapon.ResolvedAimDefinition.adsProfile.retainedConeMultiplier = 1.65f;
            weapon.pelletCount = cls == WeaponClass.Shotgun ? 9 : 1;
            weapon.aimAssistStrength = cls == WeaponClass.Sniper ? 0.34f : cls == WeaponClass.Shotgun ? 0.48f : 0.64f;
            weapon.aimAssistFov = cls == WeaponClass.Sniper ? 7f : cls == WeaponClass.Shotgun ? 18f : 14f;
            weapon.horizontalAimAssistStrength = cls == WeaponClass.Sniper ? 0.72f : 0.9f;
            weapon.verticalAimAssistStrength = cls == WeaponClass.Sniper ? 0.30f : 0.48f;
            weapon.aimAssistRotationSpeed = cls == WeaponClass.Sniper ? 88f : 118f;
            weapon.touchAimAssistMultiplier = 1.25f;
            weapon.gamepadAimAssistMultiplier = 0.9f;
            weapon.mouseAimAssistMultiplier = 0.6f;
            weapon.dragBreakThresholdTouch = 0.012f;
            weapon.dragBreakThresholdMouse = 0.018f;
            weapon.verticalUnlockDuration = 0.2f;
            weapon.chestSensitivityMultiplier = 0.68f;
            weapon.baseHipFireSpread = cls switch
            {
                WeaponClass.Sniper => 0.35f,
                WeaponClass.Pistol => 0.72f,
                WeaponClass.Rifle => 0.95f,
                WeaponClass.Smg => 1.12f,
                _ => 2.6f
            };
            weapon.firstShotSpreadMultiplier = cls == WeaponClass.Shotgun ? 0.72f : 0.38f;
            weapon.continuousFireSpreadIncrease = cls == WeaponClass.Smg ? 0.42f : 0.34f;
            weapon.modelResource = modelResource;
            return weapon;
        }

        private static ScopeDefinition CreateScope(string weaponName, ScopeType scopeType, float magnification)
        {
            var scope = ScriptableObject.CreateInstance<ScopeDefinition>();
            scope.name = $"Runtime {weaponName} {scopeType}";
            scope.scopeType = scopeType;
            scope.magnification = Mathf.Max(1f, magnification);
            scope.scopeFieldOfView = scopeType switch
            {
                ScopeType.Sniper => 16f,
                ScopeType.Scope4X => 24f,
                ScopeType.Scope2X => 38f,
                _ => 54f
            };
            scope.mainCameraAdsFieldOfView = ScopeController.MainCameraAdsFieldOfView(scopeType);
            scope.sensitivityMultiplier = scopeType switch
            {
                ScopeType.Sniper => 0.36f,
                ScopeType.Scope4X => 0.48f,
                ScopeType.Scope2X => 0.62f,
                _ => 0.78f
            };
            scope.transitionSpeed = scopeType == ScopeType.Sniper ? 7.5f : 11f;
            scope.cameraOffset = new Vector3(0f, 0.015f, 0.08f);
            scope.renderMode = ScopeRenderMode.SingleCameraOverlay;
            scope.useRenderTexture = false;
            scope.useFullScreenOverlay = false;
            return scope;
        }

        private void BuildOriginalTechnicalMap()
        {
            Random.InitState(config.lootSeed);
            var providedMap = CreateProvidedMap();
            var groundMat = BRMaterialFactory.CreateTerrain("Island Grass",
                new Color(0.11f, 0.30f, 0.15f), new Color(0.35f, 0.57f, 0.27f));
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Battle Island - Technical Ground";
            floor.transform.position = new Vector3(PlayableArea.Center.x,
                providedMap ? PlayableArea.Bounds.min.y - 2.5f : -0.55f, PlayableArea.Center.z);
            floor.transform.localScale = new Vector3(config.mapSize.x, 1f, config.mapSize.y);
            floor.GetComponent<Renderer>().sharedMaterial = groundMat;
            if (providedMap) floor.GetComponent<Renderer>().enabled = false;

            if (!providedMap)
            {
                CreateRoad("North Road", new Vector3(0f, -0.02f, 0f), new Vector3(10f, 0.08f, 170f));
                CreateRoad("East Road", new Vector3(0f, -0.01f, 0f), new Vector3(170f, 0.08f, 10f));
                CreateTownBlock();
                CreateEnvironmentDressing();
                if (!CreateImportedLandmark("building-sample-tower-a", "Clock Tower District", new Vector3(-42f, 0f, 38f), Vector3.one * 8f))
                    CreateClockTower(new Vector3(-42f, 0f, 38f));
                if (!CreateImportedLandmark("building-sample-house-a", "North Apartments", new Vector3(34f, 0f, -36f), Vector3.one * 8f))
                    CreateLandmark("Hangar", new Vector3(34f, 3f, -36f), new Vector3(24f, 7f, 16f), new Color(0.38f, 0.43f, 0.44f));
                if (!CreateImportedLandmark("building-sample-house-b", "Signal Depot", new Vector3(48f, 0f, 44f), Vector3.one * 8f))
                    CreateLandmark("Signal Depot", new Vector3(48f, 4f, 44f), new Vector3(18f, 9f, 12f), new Color(0.42f, 0.36f, 0.31f));
                if (!CreateImportedLandmark("building-sample-house-c", "Clinic Block", new Vector3(-46f, 0f, -44f), Vector3.one * 8f))
                    CreateLandmark("Clinic", new Vector3(-46f, 2.4f, -44f), new Vector3(16f, 5.8f, 12f), new Color(0.62f, 0.66f, 0.62f));
            }

            var obstacleMat = BRMaterialFactory.Create("Concrete Cover", new Color(0.38f, 0.42f, 0.43f));
            for (var i = 0; i < (providedMap ? 0 : 48); i++)
            {
                var position = new Vector3(Random.Range(-76f, 76f), 0f, Random.Range(-76f, 76f));
                if (i < 12 && CreateImportedLandmark(i % 2 == 0 ? "Quaternius/Props/Container_Long" : "Quaternius/Props/Container_Small",
                        "Cargo Cover", position, Vector3.one * Random.Range(1f, 1.35f)))
                    continue;
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "Cover Block";
                box.transform.position = new Vector3(position.x, 0.75f, position.z);
                box.transform.localScale = new Vector3(Random.Range(3f, 9f), Random.Range(1.5f, 4f), Random.Range(2f, 7f));
                box.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
                box.GetComponent<Renderer>().sharedMaterial = obstacleMat;
            }

            if (!providedMap)
            {
                var waterMat = BRMaterialFactory.Create("Forbidden Water", new Color(0.05f, 0.22f, 0.32f));
                for (var i = 0; i < 4; i++)
                {
                    var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    water.name = "Forbidden Water";
                    water.transform.position = i switch
                    {
                        0 => new Vector3(0, -0.2f, 93f),
                        1 => new Vector3(0, -0.2f, -93f),
                        2 => new Vector3(93f, -0.2f, 0),
                        _ => new Vector3(-93f, -0.2f, 0)
                    };
                    water.transform.localScale = i < 2 ? new Vector3(190f, 0.25f, 10f) : new Vector3(10f, 0.25f, 190f);
                    water.GetComponent<Renderer>().sharedMaterial = waterMat;
                }
            }

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.68f;
            light.shadowBias = 0.06f;
            light.shadowNormalBias = 0.35f;
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.62f);
        }

        private bool CreateProvidedMap()
        {
            var prefab = Resources.Load<GameObject>(WorldMapGeometry.BermudaResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"Bermuda map is missing at Resources/{WorldMapGeometry.BermudaResourcePath}.");
                return false;
            }
            var map = Instantiate(prefab);
            map.name = WorldMapGeometry.BermudaMapName;
            map.transform.localScale = Vector3.one;
            BRAssetVisuals.PrepareForUrp(map);
            var renderers = map.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Destroy(map);
                return false;
            }
            if (!WorldMapGeometry.TryCalculatePlayableBounds(renderers, out var structures))
            {
                Destroy(map);
                Debug.LogError("Bermuda map has no usable terrain bounds.");
                return false;
            }
            map.transform.localScale *= WorldMapGeometry.RequiredUniformScale(structures);
            WorldMapGeometry.TryCalculatePlayableBounds(renderers, out structures);
            map.transform.position += new Vector3(-structures.center.x, 0f, -structures.center.z);
            WorldMapGeometry.TryCalculatePlayableBounds(renderers, out structures);
            var hiddenWaterMeshes = 0;
            foreach (var renderer in renderers)
            {
                if (!WorldMapGeometry.IsWater(WorldMapGeometry.HierarchyName(renderer.transform))) continue;
                renderer.enabled = false;
                hiddenWaterMeshes++;
            }
            PlayableArea.Configure(structures);
            config.mapSize = PlayableArea.Size;
            var colliders = BuildingCollisionBuilder.BuildWorldMap(map);
            foreach (var item in map.GetComponentsInChildren<Transform>(true)) item.gameObject.isStatic = true;
            Debug.Log($"Bermuda map ready: {renderers.Length} renderers, {hiddenWaterMeshes} redundant water meshes hidden, " +
                      $"{colliders} selective static colliders, " +
                      $"playable {PlayableArea.Size}, scale {map.transform.localScale}.");
            return true;
        }

        private static Bounds FindStructuralBounds(Renderer[] renderers)
        {
            Bounds? result = null;
            foreach (var renderer in renderers)
            {
                var path = renderer.name;
                for (var parent = renderer.transform.parent; parent != null; parent = parent.parent)
                    path += "/" + parent.name;
                if (path.Contains("Plane_0") || path.Contains("Base_02") || path.Contains("Sakura_") ||
                    path.Contains("Strop_Shelf")) continue;
                if (result.HasValue)
                {
                    var value = result.Value;
                    value.Encapsulate(renderer.bounds);
                    result = value;
                }
                else result = renderer.bounds;
            }
            return result ?? renderers[0].bounds;
        }

        private static void CreateRoad(string name, Vector3 position, Vector3 scale)
        {
            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = name;
            road.transform.position = position;
            road.transform.localScale = scale;
            road.GetComponent<Renderer>().sharedMaterial = BRMaterialFactory.Create(name, new Color(0.12f, 0.13f, 0.14f));
            Destroy(road.GetComponent<Collider>());
        }

        private static void CreateTownBlock()
        {
            var spots = new[]
            {
                new Vector3(-18f, 0f, 18f), new Vector3(18f, 0f, 18f), new Vector3(-18f, 0f, -18f),
                new Vector3(18f, 0f, -18f), new Vector3(-58f, 0f, 8f), new Vector3(58f, 0f, -8f),
                new Vector3(8f, 0f, 58f), new Vector3(-8f, 0f, -58f), new Vector3(-38f, 0f, 0f),
                new Vector3(38f, 0f, 0f), new Vector3(0f, 0f, -38f), new Vector3(0f, 0f, 38f)
            };
            for (var i = 0; i < spots.Length; i++)
            {
                var resource = i % 3 == 0 ? "building-sample-house-a" : i % 3 == 1 ? "building-sample-house-b" : "building-sample-house-c";
                if (!CreateImportedLandmark(resource, $"Town Building {i + 1:00}", spots[i], Vector3.one * Random.Range(6.5f, 8.5f)))
                    CreateLandmark($"Town Building {i + 1:00}", spots[i] + Vector3.up * 2.5f,
                        new Vector3(10f, 5f, 10f), new Color(0.54f, 0.48f, 0.39f));
            }
        }

        private static void CreateLandmark(string name, Vector3 position, Vector3 scale, Color color)
        {
            var landmark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            landmark.name = name;
            landmark.transform.position = position;
            landmark.transform.localScale = scale;
            landmark.GetComponent<Renderer>().sharedMaterial = BRMaterialFactory.Create(name, color);
        }

        private static bool CreateImportedLandmark(string resourceName, string instanceName, Vector3 position, Vector3 scale)
        {
            var prefab = Resources.Load<GameObject>(resourceName.Contains("/") ? resourceName : $"KenneyBuildings/{resourceName}");
            if (prefab == null) return false;
            var instance = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            instance.name = instanceName;
            instance.transform.localScale = scale;
            BRAssetVisuals.PrepareForUrp(instance);
            if (!resourceName.Contains("/")) BRAssetVisuals.ApplyBuildingPalette(instance, instanceName.GetHashCode());
            if (resourceName.Contains("/")) BuildingCollisionBuilder.BuildBox(instance);
            else BuildingCollisionBuilder.Build(instance);
            return true;
        }

        private static void CreateEnvironmentDressing()
        {
            var props = new[]
            {
                "Tree_1", "Tree_3", "Barrier_Fixed", "Crate", "ExplodingBarrel"
            };
            for (var i = 0; i < 64; i++)
            {
                var resource = props[i % props.Length];
                var prefab = Resources.Load<GameObject>($"Quaternius/Props/{resource}");
                if (prefab == null) continue;
                var position = new Vector3(Random.Range(-78f, 78f), 0f, Random.Range(-78f, 78f));
                if (Mathf.Abs(position.x) < 8f || Mathf.Abs(position.z) < 8f) continue;
                var prop = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                prop.name = $"World Prop - {resource}";
                BRAssetVisuals.PrepareForUrp(prop);
                var tree = resource.StartsWith("Tree");
                prop.transform.localScale = Vector3.one * (tree ? Random.Range(1.1f, 1.8f) : Random.Range(0.8f, 1.2f));
                if (prop.GetComponentInChildren<Collider>() == null)
                {
                    var collider = prop.AddComponent<BoxCollider>();
                    collider.center = Vector3.up * (tree ? 1.8f : 0.45f);
                    collider.size = tree ? new Vector3(0.7f, 3.6f, 0.7f) : Vector3.one;
                }
            }
        }

        private static void CreateClockTower(Vector3 basePosition)
        {
            var stone = BRMaterialFactory.Create("Clock Tower Stone", new Color(0.55f, 0.53f, 0.47f));
            var dark = BRMaterialFactory.Create("Clock Tower Roof", new Color(0.16f, 0.17f, 0.18f));
            var face = BRMaterialFactory.Create("Clock Face", new Color(0.92f, 0.86f, 0.68f));
            var root = new GameObject("Original Clock Tower");
            root.transform.position = basePosition;
            CreateTowerPart(root.transform, "Tower Base", PrimitiveType.Cube, new Vector3(0f, 1.25f, 0f), new Vector3(15f, 2.5f, 15f), stone);
            CreateTowerPart(root.transform, "Tower Shaft", PrimitiveType.Cube, new Vector3(0f, 9f, 0f), new Vector3(8f, 15f, 8f), stone);
            CreateTowerPart(root.transform, "Observation Deck", PrimitiveType.Cube, new Vector3(0f, 17f, 0f), new Vector3(11f, 2.4f, 11f), dark);
            CreateTowerPart(root.transform, "Clock Roof", PrimitiveType.Cylinder, new Vector3(0f, 20.2f, 0f), new Vector3(4.8f, 3.4f, 4.8f), dark);
            CreateClockFace(root.transform, new Vector3(0f, 14f, 4.08f), Quaternion.Euler(90f, 0f, 0f), face);
            CreateClockFace(root.transform, new Vector3(4.08f, 14f, 0f), Quaternion.Euler(90f, 90f, 0f), face);
            CreateTowerPart(root.transform, "Signal Mast", PrimitiveType.Cylinder, new Vector3(0f, 24f, 0f), new Vector3(0.45f, 5f, 0.45f), dark);
        }

        private static void CreateTowerPart(Transform parent, string name, PrimitiveType type,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateClockFace(Transform parent, Vector3 localPosition, Quaternion localRotation, Material material)
        {
            var clock = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            clock.name = "Clock Face";
            clock.transform.SetParent(parent, false);
            clock.transform.localPosition = localPosition;
            clock.transform.localRotation = localRotation;
            clock.transform.localScale = new Vector3(2.2f, 0.08f, 2.2f);
            clock.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(clock.GetComponent<Collider>());
        }
    }
}
