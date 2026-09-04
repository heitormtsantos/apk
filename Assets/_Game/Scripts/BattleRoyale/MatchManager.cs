using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public static class MatchCursorPolicy
    {
        public static CursorLockMode LockModeFor(MatchState state, bool paused) =>
            paused || state is MatchState.Lobby or MatchState.Complete ? CursorLockMode.None : CursorLockMode.Locked;

        public static bool IsVisible(MatchState state, bool paused) =>
            paused || state is MatchState.Lobby or MatchState.Complete;

        public static void Apply(MatchState state, bool paused)
        {
            Cursor.lockState = LockModeFor(state, paused);
            Cursor.visible = IsVisible(state, paused);
        }
    }

    public static class MatchInputPolicy
    {
        public static bool ShouldReadGameplayInput(MatchState state, bool paused) => !paused
            && state is MatchState.Staging or MatchState.Plane or MatchState.Active;
    }

    public sealed class MatchManager : MonoBehaviour
    {
        public const float AutoPickupInterval = 0.15f;
        public const float AutoPickupRadius = 1.35f;

        private readonly List<BRParticipant> participants = new();
        private readonly List<LootPickup> autoPickupCandidates = new();
        private readonly HashSet<BRParticipant> processedDeaths = new();
        private BRGameConfig config;
        private IReadOnlyList<WeaponDefinition> weapons;
        private LootSpawner lootSpawner;
        private DropSystem drop;
        private ThirdPersonCamera thirdPersonCamera;
        private LocalInputSource input;
        private SafeZoneConfig safeZoneConfig;
        private MatchEconomyConfig economyConfig;
        private MatchShopDatabase shopDatabase;
        private MatchRewardSystem rewardSystem;
        private GameObject lobbyShowcase;
        private float phaseUntil;
        private float startedAt;
        private int reviveDamageSequence;
        private int winningTeamId = -1;
        private int initialTeamCount;
        private int playerPlacement;
        private bool rankedResultApplied;
        private bool globalStateReleased;
        private BattleRoyaleLifeConfig lifeConfig;
        private AirdropManager airdropManager;
        private BattleRoyaleAuthorityGateway authorityGateway;
        private PlayerProfileService accountProfile;
        private string activeMatchId;

        public event System.Action<EliminationInfo> PlayerEliminated;
        public event System.Action<BRParticipant> PlayerKnocked;
        public event System.Action<BRParticipant> PlayerRevived;
        public event System.Action<int, int> AliveCountChanged;
        public event System.Action<int, int> SquadEliminated;
        public event System.Action<int> MatchWinnerDetermined;
        public event System.Action<string> MatchAnnouncement;

        public MatchState State { get; private set; } = MatchState.Lobby;
        public BRParticipant Player { get; private set; }
        public SafeZoneController SafeZone { get; private set; }
        public MatchShopController MatchShop { get; private set; }
        public MatchCurrencyWallet PlayerWallet => Player != null ? Player.Wallet : null;
        public int AliveCount { get; private set; }
        public int AliveTeamCount { get; private set; }
        public BRMatchMode Mode => config != null ? config.selectedMode : BRMatchMode.Solo;
        public BRPlaylist Playlist => config != null ? config.playlist : BRPlaylist.Casual;
        public int TeamSize => BRMatchRules.TeamSize(Mode);
        public int TeamCount => config != null ? Mathf.Max(1, config.participantCount / TeamSize) : 20;
        public string ResultText { get; private set; } = "RESULTADO";
        public bool Paused { get; private set; }
        public bool InventoryOpen { get; private set; }
        public IReadOnlyList<BRParticipant> Participants => participants;
        public IReadOnlyList<WeaponDefinition> WeaponCatalog => weapons;
        public CharacterClothingManager LobbyWardrobe => lobbyShowcase != null
            ? lobbyShowcase.GetComponent<CharacterClothingManager>() : null;
        public Transform LobbyCharacterRoot => lobbyShowcase != null
            ? lobbyShowcase.GetComponent<CharacterVisualAnimator>()?.VisualRoot : null;
        public DropSystem Drop => drop;
        public AirdropManager Airdrops => airdropManager;
        public BattleRoyaleAuthorityGateway Authority => authorityGateway;
        public PlayerProfileService AccountProfile => accountProfile;
        public MatchProgressionResult LastProgressionResult { get; private set; }
        public float PhaseTimeRemaining => Mathf.Max(0f, phaseUntil - Time.time);
        public int PassengersRemaining => drop != null ? drop.CountPassengers(participants) : 0;
        public Vector2 MapSize => config != null ? config.mapSize : new Vector2(180f, 180f);
        public LootPickup NearbyPlayerLoot => Player != null && lootSpawner != null
            ? lootSpawner.FindNearest(Player.transform.position, config.interactRadius)
            : null;
        public MatchShopTerminal NearbyMatchShop => State == MatchState.Active && Player != null && config != null
            ? MatchShopTerminal.FindNearest(Player.transform.position, Mathf.Max(config.interactRadius, 3.2f))
            : null;
        public DeathLootContainer NearbyDeathLoot => Player != null
            ? DeathLootContainer.FindNearest(Player.transform.position, config.interactRadius + 0.7f)
            : null;
        public VehicleSeatManager ActiveVehicle { get; private set; }
        public VehicleSeatManager NearbyVehicle => Player != null && ActiveVehicle == null
            ? VehicleSeatManager.FindNearest(Player.transform.position, Mathf.Max(config.interactRadius, 3.2f))
            : null;
        public DeathLootContainer ActiveDeathLoot { get; private set; }
        public BRParticipant ReviveTarget { get; private set; }
        public BRParticipant NearbyReviveTarget => Player != null && config != null
            ? FindNearestReviveTarget(Player, participants, config.reviveRadius)
            : null;
        public float ReviveProgress { get; private set; }
        public int WinningTeamId => winningTeamId;
        public RankedMatchResult LastRankedResult { get; private set; }
        public RankedProfile RankedProfile { get; private set; }
        public RankedProfile Profile => RankedProfile;
        public RankedTier Tier => RankedProfile != null ? RankedProfile.Tier : RankedTier.Bronze;
        public int PlayerRevives => Player?.Revives ?? 0;
        public string PickupFeedback { get; private set; }
        public bool PickupFeedbackVisible => !string.IsNullOrEmpty(PickupFeedback) && Time.time < pickupFeedbackUntil;
        private float nextHeldInteractTime;
        private float pickupFeedbackUntil;
        private float nextAutoPickupTime;

        private void OnEnable() => globalStateReleased = false;

        private void OnDisable() => ReleaseGlobalState();

        private void OnDestroy()
        {
            if (airdropManager != null) airdropManager.Announcement -= OnMatchAnnouncement;
            ReleaseGlobalState();
        }

        private void ReleaseGlobalState()
        {
            if (globalStateReleased) return;
            globalStateReleased = true;
            Paused = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            input?.ResetTransientState();
        }

        public void Configure(BRGameConfig gameConfig, IReadOnlyList<WeaponDefinition> weaponCatalog,
            LootSpawner spawner, SafeZoneController safeZone, DropSystem dropSystem,
            ThirdPersonCamera cameraController, List<BRParticipant> externalParticipants, GameObject showcase,
            SafeZoneConfig zoneConfig = null, MatchEconomyConfig economySettings = null,
            MatchShopDatabase matchShopDatabase = null, BattleRoyaleLifeConfig lifeSettings = null,
            AirdropConfig airdropSettings = null, PlayerProfileService progressionProfile = null)
        {
            config = gameConfig;
            weapons = weaponCatalog;
            lootSpawner = spawner;
            SafeZone = safeZone;
            drop = dropSystem;
            thirdPersonCamera = cameraController;
            lobbyShowcase = showcase;
            safeZoneConfig = zoneConfig;
            economyConfig = economySettings;
            shopDatabase = matchShopDatabase;
            lifeConfig = lifeSettings != null ? lifeSettings : BattleRoyaleLifeConfig.CreateRuntimeDefault();
            accountProfile = progressionProfile;
            MatchShop = GetComponent<MatchShopController>() ?? gameObject.AddComponent<MatchShopController>();
            rewardSystem = GetComponent<MatchRewardSystem>() ?? gameObject.AddComponent<MatchRewardSystem>();
            rewardSystem.Configure(economyConfig);
            airdropManager = GetComponent<AirdropManager>() ?? gameObject.AddComponent<AirdropManager>();
            airdropManager.Configure(this, weapons, airdropSettings,
                config != null ? config.lootSeed : 1337);
            airdropManager.Announcement += OnMatchAnnouncement;
            authorityGateway = GetComponent<BattleRoyaleAuthorityGateway>()
                ?? gameObject.AddComponent<BattleRoyaleAuthorityGateway>();
            input = gameObject.AddComponent<LocalInputSource>();
            RefreshRankedProfile();
            ReturnToLobby();
        }

        private void OnMatchAnnouncement(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            MatchAnnouncement?.Invoke(message);
            PickupFeedback = message;
            pickupFeedbackUntil = Time.time + 3.5f;
        }

        public bool SetMode(BRMatchMode mode)
        {
            if (State != MatchState.Lobby) return false;
            config ??= new BRGameConfig();
            config.selectedMode = mode;
            config.participantCount = BRMatchRules.NormalizeParticipantCount(mode);
            config.botCount = config.participantCount - 1;
            RefreshRankedProfile();
            return true;
        }

        public bool SetPlaylist(BRPlaylist playlist)
        {
            if (State != MatchState.Lobby) return false;
            config ??= new BRGameConfig();
            config.playlist = playlist;
            RefreshRankedProfile();
            return true;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab) && State == MatchState.Active && !Paused)
                ToggleInventory();
            if (Input.GetKeyDown(KeyCode.Escape) && State is MatchState.Staging or MatchState.Plane or MatchState.Active)
            {
                if (InventoryOpen) CloseInventory();
                else SetPaused(!Paused);
            }
            if (!MatchInputPolicy.ShouldReadGameplayInput(State, Paused))
            {
                if (State == MatchState.Complete && Input.GetKeyDown(KeyCode.R)) StartMatch();
                return;
            }

            var frame = input != null ? input.ReadInput() : default;
            MatchShop?.Tick();
            if (MatchShop != null) frame = MatchShop.ApplyInputRestrictions(frame);
            if (InventoryOpen) frame = default;
            var deploying = Player != null
                && Player.Phase is ParticipantPhase.Freefall or ParticipantPhase.Parachute;
            thirdPersonCamera?.SetDeploymentSteering(deploying && DeploymentSteering.HasInput(frame.Move));
            PreCombatTick(frame);
            if (State == MatchState.Staging)
            {
                TickStaging(frame);
                if (Time.time >= phaseUntil) BeginPlane();
            }
            else if (State == MatchState.Plane)
            {
                drop.TickPlane(participants, Player, frame.Drop);
                TickParticipants(frame, false);
                if (Player != null && Player.Phase == ParticipantPhase.Grounded) BeginActiveMatch();
            }
            else if (State == MatchState.Active)
            {
                TickBleedout();
                if (State == MatchState.Active) TickParticipants(frame, true);
                if (State == MatchState.Active && ActiveDeathLoot != null
                    && (Player == null || !ActiveDeathLoot.gameObject.activeInHierarchy
                    || !Player.IsCombatCapable
                    || Vector3.Distance(Player.transform.position, ActiveDeathLoot.transform.position) > config.interactRadius + 1.2f))
                    CloseDeathLoot();
                if (InventoryOpen && (Player == null || !Player.IsCombatCapable || ActiveVehicle != null
                    || (MatchShop != null && MatchShop.IsShopOpen))) CloseInventory();
                if (State == MatchState.Active) TickAutoPickup(Time.time);
                if (State == MatchState.Active)
                    SafeZone.Tick(participants, Time.deltaTime, () => State == MatchState.Active);
                if (State == MatchState.Active)
                {
                    UpdateAlive();
                    if (AliveTeamCount <= 1) Complete();
                }
            }
            thirdPersonCamera?.PostMovementTick(frame);
        }

        private void PreCombatTick(BRInputFrame frame)
        {
            thirdPersonCamera?.PreCombatTick(frame);
        }

        public static bool IsAimAssistCandidate(BRParticipant owner, BRParticipant candidate) =>
            BRMatchRules.AreEnemies(owner, candidate);

        public void StartMatch()
        {
            CloseInventory();
            ActiveVehicle?.ForceExitAll();
            airdropManager?.ResetRuntime();
            authorityGateway?.ResetSession();
            activeMatchId = System.Guid.NewGuid().ToString("N");
            LastProgressionResult = null;
            input?.ResetTransientState();
            SetPaused(false);
            if (lobbyShowcase != null) lobbyShowcase.SetActive(false);
            ClearParticipants();
            config.Normalize();
            initialTeamCount = Mathf.Max(1, config.participantCount / BRMatchRules.TeamSize(config.selectedMode));
            playerPlacement = 0;
            rankedResultApplied = false;
            processedDeaths.Clear();
            LastRankedResult = null;
            RefreshRankedProfile();
            drop.Configure(config);
            MatchShop?.CloseShop();
            MatchShopTerminal.ResetAllRuntimeStock();
            ConfigureSafeZone(false);
            SpawnParticipants();
            MatchShop?.Configure(this, Player, economyConfig, SafeZone);
            lootSpawner.SpawnLoot(config, weapons, economyConfig);
            DeathLootContainer.ClearAll();
            DamageFeedbackSystem.ClearEvents();
            phaseUntil = Time.time + config.stagingSeconds;
            startedAt = Time.time;
            nextAutoPickupTime = Time.time;
            ResultText = string.Empty;
            winningTeamId = -1;
            State = MatchState.Staging;
            thirdPersonCamera?.ResetViewState();
            thirdPersonCamera?.SetStagingView(true);
            MatchCursorPolicy.Apply(State, Paused);
        }

        private void SpawnParticipants()
        {
            for (var i = 0; i < config.participantCount; i++)
            {
                var isPlayer = i == 0;
                var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                obj.name = isPlayer ? "Player - Raven Unit" : $"Bot - Nomad {i:00}";
                obj.transform.position = new Vector3(0f, config.dropHeight, 0f);
                Object.Destroy(obj.GetComponent<CapsuleCollider>());
                var controller = obj.AddComponent<CharacterController>();
                CharacterPresentationProfile.Apply(controller);
                obj.AddComponent<BRCharacterMotor>();
                obj.AddComponent<HealthArmorSystem>();
                obj.AddComponent<InventorySystem>();
                obj.AddComponent<WeaponAudioEmitter>();
                obj.AddComponent<WeaponSystem>();
                obj.AddComponent<CombatReaction>();
                obj.AddComponent<DeploymentVisibility>();
                var wallet = obj.AddComponent<MatchCurrencyWallet>();
                var participant = obj.AddComponent<BRParticipant>();
                obj.AddComponent<DamageContributorTracker>();
                obj.AddComponent<MatchPlayerStats>();
                participant.Configure(isPlayer ? "Raven Unit" : $"Nomad {i:00}", isPlayer, i,
                    BRMatchRules.AssignTeamId(i, config.selectedMode));
                participant.Health.InitializeFull();
                wallet.SetCurrency(economyConfig != null ? economyConfig.startingCurrency : 0);
                participant.Health.ConfigureLifeCycle(config.bleedoutSeconds, config.reviveHealth,
                    () => lifeConfig.EnableKnockdown
                        && BRMatchRules.ShouldDownOnLethal(config.selectedMode, participant, participants));
                participant.Health.ConfigureKnockdownRules(lifeConfig);
                participant.Health.SetInvulnerable(isPlayer ? 12f : 3f);
                participant.Health.Downed += OnParticipantDowned;
                participant.Health.Revived += OnParticipantRevived;
                participant.Health.Died += OnParticipantDied;
                participant.Stats?.ResetStats();
                BuildParticipantVisual(obj, isPlayer);
                obj.AddComponent<FootstepAudioEmitter>();
                participants.Add(participant);
                if (isPlayer)
                {
                    Player = participant;
                    thirdPersonCamera.SetTarget(obj.transform);
                }
                else
                {
                    obj.AddComponent<BotController>().Configure(participant, lootSpawner, SafeZone, config);
                }
            }
            PlaceParticipantsAtStaging();
            AliveCount = participants.Count;
            AliveTeamCount = BRMatchRules.CountSurvivingTeams(participants);
        }

        private void PlaceParticipantsAtStaging()
        {
            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                if (participant == null) continue;
                var angle = i * Mathf.PI * 2f / Mathf.Max(1, participants.Count);
                var radius = participant.IsPlayer ? 0f : 5.5f + (i % 3) * 1.4f;
                var position = config.stagingCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (PlayableArea.TryGround(position, out var grounded, 0.25f)) position = grounded;
                participant.Motor.Teleport(position);
                participant.SetPhase(ParticipantPhase.Grounded);
                if (!participant.IsPlayer)
                    participant.GetComponent<BotController>()?.BeginStaging(config.stagingCenter, i);
            }
        }

        private static void BuildParticipantVisual(GameObject obj, bool player)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
            var profile = CharacterVisualCatalog.ResolveAvailable(player
                ? CharacterVisualCatalog.Player
                : CharacterVisualCatalog.Bot);
            var characterPrefab = profile.LoadPrefab();
            if (characterPrefab != null)
            {
                var character = Instantiate(characterPrefab, obj.transform, false);
                character.name = player ? $"Animated Player Model [{profile.Id}]" : $"Animated Bot Model [{profile.Id}]";
                character.transform.localPosition = Vector3.zero;
                character.transform.localRotation = Quaternion.identity;
                character.transform.localScale = Vector3.one;
                BRAssetVisuals.PrepareForUrp(character);
                DisableEmbeddedWeapons(character);
                BRAssetVisuals.NormalizeHeight(character, profile.TargetHeight);
                AlignModelToGround(character, obj.transform.position.y);
                obj.AddComponent<CharacterVisualAnimator>().Configure(character, profile);
                obj.AddComponent<ParticipantWeaponVisual>().Configure(character);
                CreateHeadHitbox(obj.transform);
                return;
            }
            var suit = BRMaterialFactory.Create(player ? "Player Suit" : "Bot Suit",
                player ? new Color(0.08f, 0.55f, 0.85f) : new Color(0.62f, 0.20f, 0.15f));
            var dark = BRMaterialFactory.Create("Combat Gear", new Color(0.08f, 0.09f, 0.10f));
            var skin = BRMaterialFactory.Create("Visor", new Color(0.9f, 0.78f, 0.55f));
            const float sourceFeetY = 0.01f;
            const float sourceTopY = 1.88f;
            var fallbackVisual = new GameObject("Fallback Character Visual").transform;
            fallbackVisual.SetParent(obj.transform, false);
            var visualScale = CharacterPresentationProfile.VisualHeight / (sourceTopY - sourceFeetY);
            fallbackVisual.localPosition = Vector3.down * (sourceFeetY * visualScale);
            fallbackVisual.localScale = Vector3.one * visualScale;
            CreateVisualPart(fallbackVisual, "Torso", PrimitiveType.Cube, new Vector3(0f, 1.02f, 0f), new Vector3(0.62f, 0.78f, 0.32f), suit);
            CreateVisualPart(fallbackVisual, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0f), new Vector3(0.36f, 0.32f, 0.36f), skin);
            CreateVisualPart(fallbackVisual, "Backpack", PrimitiveType.Cube, new Vector3(0f, 1.0f, -0.26f), new Vector3(0.42f, 0.62f, 0.18f), dark);
            if (!player) CreateVisualPart(fallbackVisual, "Rifle", PrimitiveType.Cube, new Vector3(0.34f, 1.12f, 0.34f), new Vector3(0.12f, 0.12f, 0.78f), dark);
            CreateVisualPart(fallbackVisual, "Arm L", PrimitiveType.Cube, new Vector3(-0.46f, 1.08f, 0f), new Vector3(0.16f, 0.68f, 0.16f), suit);
            CreateVisualPart(fallbackVisual, "Arm R", PrimitiveType.Cube, new Vector3(0.46f, 1.08f, 0f), new Vector3(0.16f, 0.68f, 0.16f), suit);
            CreateVisualPart(fallbackVisual, "Leg L", PrimitiveType.Cube, new Vector3(-0.18f, 0.32f, 0f), new Vector3(0.18f, 0.62f, 0.18f), dark);
            CreateVisualPart(fallbackVisual, "Leg R", PrimitiveType.Cube, new Vector3(0.18f, 0.32f, 0f), new Vector3(0.18f, 0.62f, 0.18f), dark);
            CreateHeadHitbox(obj.transform);
        }

        private static void DisableEmbeddedWeapons(GameObject character)
        {
            var weaponNames = new[]
            {
                "AK", "GrenadeLauncher", "Knife_1", "Knife_2", "Pistol", "Revolver", "Revolver_Small",
                "RocketLauncher", "ShortCannon", "Shotgun", "Shovel", "SMG", "Sniper", "Sniper_2"
            };
            foreach (var renderer in character.GetComponentsInChildren<Renderer>(true))
            foreach (var weaponName in weaponNames)
            {
                if (!renderer.name.Equals(weaponName, System.StringComparison.OrdinalIgnoreCase)) continue;
                renderer.enabled = false;
                break;
            }
        }

        private static void AlignModelToGround(GameObject model, float rootY)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            model.transform.localPosition += Vector3.up * (rootY - bounds.min.y + 0.015f);
        }

        private static void CreateHeadHitbox(Transform parent)
        {
            var head = new GameObject("Head Hitbox");
            head.transform.SetParent(parent, false);
            head.transform.localPosition = Vector3.up * CharacterPresentationProfile.HeadHeight;
            var collider = head.AddComponent<SphereCollider>();
            collider.radius = CharacterPresentationProfile.HeadRadius;
        }

        private static void CreateVisualPart(Transform parent, string name, PrimitiveType type,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var collider = part.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            var renderer = part.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private void TickStaging(BRInputFrame frame)
        {
            foreach (var participant in participants)
            {
                if (participant == null) continue;
                if (participant.IsPlayer)
                    participant.Motor.Move(frame, Camera.main != null ? Camera.main.transform : null, config, true);
                else
                    participant.GetComponent<BotController>()?.TickStaging(config.stagingCenter, 9.5f);
            }
        }

        private void TickParticipants(BRInputFrame frame, bool combatEnabled)
        {
            foreach (var participant in participants)
            {
                if (combatEnabled && State != MatchState.Active) break;
                if (participant == null || participant.Health.IsDead) continue;
                if (participant.Phase is ParticipantPhase.Freefall or ParticipantPhase.Parachute)
                {
                    var move = participant.IsPlayer ? frame.Move : Random.insideUnitCircle * 0.12f;
                    if (participant.IsPlayer && thirdPersonCamera != null)
                        drop.TickFalling(participant, move, frame.Drop,
                            thirdPersonCamera.PlanarForward, thirdPersonCamera.PlanarRight);
                    else
                        drop.TickFalling(participant, move, false);
                    continue;
                }
                if (participant.Phase is not (ParticipantPhase.Grounded or ParticipantPhase.Downed)) continue;
                if (participant.IsPlayer)
                {
                    if (ActiveVehicle != null && ActiveVehicle.GetSeat(participant) != null)
                    {
                        ActiveVehicle.GetComponent<VehicleInputAdapter>()?.Tick(frame, participant);
                        if (ActiveVehicle != null && ActiveVehicle.GetSeat(participant) != null) continue;
                    }
                    if (!participant.IsCombatCapable)
                    {
                        CancelPlayerRevive();
                        participant.Motor.Move(frame, Camera.main != null ? Camera.main.transform : null, config, false);
                        continue;
                    }
                    if (combatEnabled && TickPlayerRevive(frame.Interact || frame.InteractHeld,
                            Time.deltaTime, participant)) continue;
                    participant.Motor.Move(frame, Camera.main != null ? Camera.main.transform : null, config, true);
                    if (combatEnabled)
                    {
                        if (frame.Interact)
                        {
                            TryInteract(participant);
                            nextHeldInteractTime = Time.time + 0.24f;
                        }
                        else if (frame.InteractHeld && ActiveDeathLoot == null && Time.time >= nextHeldInteractTime)
                        {
                            TryInteract(participant);
                            nextHeldInteractTime = Time.time + 0.24f;
                        }
                        if (ActiveDeathLoot == null && (MatchShop == null || !MatchShop.IsShopOpen))
                        {
                            participant.Weapon.TickTrigger(Camera.main, frame.Aim,
                                frame.Fire, frame.FirePressed, participant.gameObject);
                            if (frame.Reload) participant.Weapon.TryReload();
                            if (frame.Heal)
                            {
                                participant.Weapon.CancelReloadForAction();
                                participant.Inventory.TryUseMedKit(participant.Health);
                            }
                            if (frame.WeaponSlot >= 0) participant.Weapon.SelectWeapon(frame.WeaponSlot);
                            else if (frame.Swap) participant.Weapon.SwapWeapon();
                        }
                    }
                }
                else
                {
                    participant.GetComponent<BotController>()?.Tick(participants);
                }
            }
        }

        private bool TickPlayerRevive(bool holding, float deltaTime, BRParticipant reviver)
        {
            if (ReviveTarget != null)
            {
                var eligible = State == MatchState.Active
                    && BRMatchRules.CanContinueRevive(reviver, ReviveTarget, config.reviveRadius,
                        reviveDamageSequence);
                var step = BRMatchRules.AdvanceReviveHold(ReviveProgress, deltaTime,
                    config.reviveSeconds, holding, eligible);
                if (step.Status == ReviveHoldStatus.Cancelled)
                {
                    CancelPlayerRevive();
                    return false;
                }
                reviver.Weapon.CancelReloadForAction();
                reviver.Motor.MoveWorld(Vector3.zero, 0f);
                ReviveProgress = step.Progress;
                if (step.Status == ReviveHoldStatus.Completed)
                {
                    ReviveTarget.Health.Revive();
                    if (reviver.Stats != null) reviver.Stats.RecordRevive();
                    else reviver.Revives++;
                    rewardSystem?.RewardRevive(reviver);
                    CancelPlayerRevive();
                }
                return true;
            }
            if (!holding || State != MatchState.Active) return false;
            var candidate = FindNearestReviveTarget(reviver, participants, config.reviveRadius);
            var hasLootTarget = ActiveDeathLoot != null || NearbyDeathLoot != null || NearbyPlayerLoot != null;
            if (BRMatchRules.ResolveInteractionPriority(holding, candidate != null, hasLootTarget)
                != InteractionPriority.Revive) return false;
            CloseDeathLoot();
            ReviveTarget = candidate;
            ReviveProgress = 0f;
            reviveDamageSequence = reviver.Health.DamageSequence;
            reviver.Weapon.CancelReloadForAction();
            reviver.Motor.MoveWorld(Vector3.zero, 0f);
            return true;
        }

        public static BRParticipant FindNearestReviveTarget(BRParticipant reviver,
            IReadOnlyList<BRParticipant> candidates, float radius)
        {
            if (reviver == null || candidates == null) return null;
            BRParticipant nearest = null;
            var bestSqr = radius * radius;
            foreach (var candidate in candidates)
            {
                if (!BRMatchRules.CanRevive(reviver, candidate, radius)) continue;
                var sqr = (candidate.transform.position - reviver.transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                nearest = candidate;
            }
            return nearest;
        }

        private void CancelPlayerRevive()
        {
            ReviveTarget = null;
            ReviveProgress = 0f;
        }

        private void BeginPlane()
        {
            thirdPersonCamera?.SetStagingView(false);
            foreach (var participant in participants)
                if (participant != null) participant.SetPhase(ParticipantPhase.WaitingPlane);
            drop.PreparePassengers(participants);
            drop.BeginRoute();
            State = MatchState.Plane;
        }

        private void BeginActiveMatch()
        {
            if (State == MatchState.Active) return;
            drop.EjectRemaining(participants);
            ConfigureSafeZone(true);
            var zoneEffects = SafeZone.GetComponent<SafeZonePlayerEffects>()
                ?? SafeZone.gameObject.AddComponent<SafeZonePlayerEffects>();
            zoneEffects.Configure(SafeZone, Player != null ? Player.transform : null, SafeZone.Config);
            if (Player != null) lootSpawner.SpawnStarterLoot(Player.transform.position, weapons, economyConfig);
            State = MatchState.Active;
        }

        private void ConfigureSafeZone(bool start)
        {
            if (safeZoneConfig != null) SafeZone.Configure(safeZoneConfig, config.lootSeed, start);
            else
            {
                SafeZone.Configure(config);
                if (!start) SafeZone.StopSafeZone();
            }
        }

        public void ReturnToLobby()
        {
            CloseInventory();
            ActiveVehicle?.ForceExitAll();
            airdropManager?.ResetRuntime();
            input?.ResetTransientState();
            SetPaused(false);
            MatchShop?.CloseShop();
            ClearParticipants();
            SafeZone?.StopSafeZone();
            State = MatchState.Lobby;
            ResultText = string.Empty;
            winningTeamId = -1;
            LastRankedResult = null;
            RefreshRankedProfile();
            MatchCursorPolicy.Apply(State, Paused);
            thirdPersonCamera?.SetStagingView(false);
            var cameraAnchor = config != null ? config.stagingCenter + Vector3.up * 0.62f : Vector3.up * 0.62f;
            var characterCenter = config != null ? config.stagingCenter + Vector3.up * 0.5f : Vector3.up * 0.5f;
            var cameraPosition = FindLobbyCameraPosition(cameraAnchor);
            var cameraForward = Vector3.ProjectOnPlane(characterCenter - cameraPosition, Vector3.up).normalized;
            var cameraRight = Vector3.Cross(Vector3.up, cameraForward).normalized;
            var lookAt = characterCenter - cameraRight * 0.18f;
            thirdPersonCamera?.SetStaticView(cameraPosition, lookAt, 28f);
            if (lobbyShowcase != null)
            {
                var facing = Vector3.ProjectOnPlane(cameraPosition - characterCenter, Vector3.up);
                if (facing.sqrMagnitude > 0.001f)
                {
                    lobbyShowcase.transform.rotation = Quaternion.LookRotation(facing.normalized);
                    var hangar = lobbyShowcase.transform.Find("Raven Lobby Hangar");
                    if (hangar != null) hangar.rotation = Quaternion.LookRotation(-facing.normalized);
                }
                lobbyShowcase.SetActive(true);
            }
        }

        private static Vector3 FindLobbyCameraPosition(Vector3 lookAt)
        {
            var best = lookAt + new Vector3(1.8f, 0.72f, -2.45f);
            var bestClearance = 0f;
            for (var i = 0; i < 16; i++)
            {
                var angle = i * Mathf.PI * 2f / 16f;
                var direction = new Vector3(Mathf.Cos(angle), 0.05f, Mathf.Sin(angle)).normalized;
                var clearance = Physics.SphereCast(lookAt, 0.22f, direction, out var hit, 3.4f, ~0,
                    QueryTriggerInteraction.Ignore) ? hit.distance : 3.4f;
                var candidate = lookAt + direction * Mathf.Min(2.85f, Mathf.Max(1.35f, clearance - 0.25f));
                if (Physics.Raycast(candidate, Vector3.up, 7f, ~0, QueryTriggerInteraction.Ignore)) clearance -= 8f;
                if (clearance <= bestClearance) continue;
                bestClearance = clearance;
                best = candidate;
            }
            return best;
        }

        public void SetPaused(bool paused)
        {
            var next = paused && State is MatchState.Staging or MatchState.Plane or MatchState.Active;
            if (Paused != next) input?.ResetTransientState();
            Paused = next;
            Time.timeScale = Paused ? 0f : 1f;
            MatchCursorPolicy.Apply(State, Paused);
        }

        private void TryInteract(BRParticipant participant)
        {
            if (InventoryOpen)
            {
                CloseInventory();
                return;
            }
            if (MatchShop != null && MatchShop.IsShopOpen)
            {
                MatchShop.CloseShop();
                return;
            }
            if (ActiveDeathLoot != null)
            {
                CloseDeathLoot();
                return;
            }
            var vehicle = VehicleSeatManager.FindNearest(participant.transform.position,
                Mathf.Max(config.interactRadius, 3.2f));
            if (vehicle != null && vehicle.RequestEnterVehicle(participant))
            {
                SetActiveVehicle(vehicle);
                return;
            }
            var deathLoot = DeathLootContainer.FindNearest(participant.transform.position, config.interactRadius + 0.7f);
            if (deathLoot != null)
            {
                ActiveDeathLoot = deathLoot;
                deathLoot.SetOpen(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }
            var shop = MatchShopTerminal.FindNearest(participant.transform.position,
                Mathf.Max(config.interactRadius, 3.2f));
            if (shop != null && MatchShop != null)
            {
                CloseDeathLoot();
                MatchShop.OpenShop(shop);
                return;
            }
            var pickup = lootSpawner.FindNearest(participant.transform.position, config.interactRadius);
            if (pickup == null) return;
            var itemName = PickupItemName(pickup);
            var result = pickup.CollectQuantitative(participant);
            if (result.Success) ShowPickupFeedback(itemName, result.AcceptedAmount);
            else if (!string.IsNullOrEmpty(result.FailureReason)) ShowPickupFailure(result.FailureReason);
        }

        public void TryInteractPlayer()
        {
            if (State != MatchState.Active || Paused || Player == null || !Player.IsCombatCapable) return;
            TryInteract(Player);
        }

        private void SetActiveVehicle(VehicleSeatManager vehicle)
        {
            if (ActiveVehicle == vehicle) return;
            if (ActiveVehicle != null) ActiveVehicle.OccupantExited -= OnVehicleOccupantExited;
            ActiveVehicle = vehicle;
            ActiveVehicle.OccupantExited += OnVehicleOccupantExited;
            ActiveVehicle.GetComponent<VehicleCameraController>()?.Activate(thirdPersonCamera);
            CloseDeathLoot();
            CloseInventory();
        }

        private void OnVehicleOccupantExited(BRParticipant participant, VehicleSeat seat)
        {
            if (participant == null || !participant.IsPlayer || ActiveVehicle == null) return;
            var previous = ActiveVehicle;
            previous.OccupantExited -= OnVehicleOccupantExited;
            ActiveVehicle = null;
            previous.GetComponent<VehicleCameraController>()?.Deactivate(Player != null ? Player.transform : null);
        }

        public static bool IsAutoPickupKind(LootKind kind) =>
            kind is LootKind.Ammo or LootKind.Heal or LootKind.Attachment or LootKind.Currency
                or LootKind.Backpack;

        public static bool IsAutoPickupEligible(MatchState state, BRParticipant participant,
            bool reviveActive, bool deathLootOpen) => state == MatchState.Active
            && participant != null && participant.Phase == ParticipantPhase.Grounded
            && participant.IsCombatCapable && !reviveActive && !deathLootOpen;

        public bool TickAutoPickup(float now)
        {
            if (now < nextAutoPickupTime) return false;
            nextAutoPickupTime = now + AutoPickupInterval;
            if (!IsAutoPickupEligible(State, Player, ReviveTarget != null, ActiveDeathLoot != null)
                || lootSpawner == null) return false;
            autoPickupCandidates.Clear();
            var radiusSqr = AutoPickupRadius * AutoPickupRadius;
            foreach (var pickup in lootSpawner.Pickups)
            {
                if (pickup == null || pickup.Collected || !IsAutoPickupKind(pickup.Kind)) continue;
                if ((pickup.transform.position - Player.transform.position).sqrMagnitude <= radiusSqr)
                    autoPickupCandidates.Add(pickup);
            }
            autoPickupCandidates.Sort((a, b) =>
                ((a.transform.position - Player.transform.position).sqrMagnitude)
                .CompareTo((b.transform.position - Player.transform.position).sqrMagnitude));
            foreach (var pickup in autoPickupCandidates)
            {
                var itemName = PickupItemName(pickup);
                var result = pickup.CollectQuantitative(Player);
                if (!result.Success) continue;
                ShowPickupFeedback(itemName, result.AcceptedAmount);
                return true;
            }
            return false;
        }

        private static string PickupItemName(LootPickup pickup) => pickup.Kind switch
        {
            LootKind.Ammo => $"Municao {pickup.AmmoKind}",
            LootKind.Heal => "Pulse Patch",
            LootKind.Attachment => "Stabilizer Mod",
            LootKind.Currency => "Creditos",
            LootKind.Backpack => pickup.DisplayName,
            _ => pickup.DisplayName
        };

        public bool TakeDeathLootEntry(int index)
        {
            if (ActiveDeathLoot == null || index < 0 || index >= ActiveDeathLoot.Entries.Count) return false;
            return TakeDeathLootEntryById(ActiveDeathLoot.Entries[index].EntryId);
        }

        public bool TakeDeathLootEntryById(int entryId)
        {
            if (ActiveDeathLoot == null || Player == null || !Player.IsCombatCapable) return false;
            var displayName = string.Empty;
            foreach (var entry in ActiveDeathLoot.Entries)
            {
                if (entry.EntryId != entryId) continue;
                displayName = entry.DisplayName;
                break;
            }
            var result = ActiveDeathLoot.TakeById(entryId, Player);
            if (result.Success) ShowPickupFeedback(displayName);
            if (ActiveDeathLoot == null || !ActiveDeathLoot.gameObject.activeInHierarchy) CloseDeathLoot();
            return result.Success;
        }

        public int TakeAllDeathLoot()
        {
            if (ActiveDeathLoot == null || Player == null || !Player.IsCombatCapable) return 0;
            var count = ActiveDeathLoot.TakeAll(Player);
            if (count > 0) ShowPickupFeedback($"{count} ITENS");
            if (ActiveDeathLoot == null || !ActiveDeathLoot.gameObject.activeInHierarchy) CloseDeathLoot();
            return count;
        }

        private void ShowPickupFeedback(string itemName)
        {
            PickupFeedback = $"COLETADO  {itemName}";
            pickupFeedbackUntil = Time.time + 1.8f;
        }

        private void ShowPickupFeedback(string itemName, int amount)
        {
            PickupFeedback = $"COLETADO  {itemName}  x{Mathf.Max(1, amount)}";
            pickupFeedbackUntil = Time.time + 1.8f;
        }

        private void ShowPickupFailure(string reason)
        {
            PickupFeedback = reason;
            pickupFeedbackUntil = Time.time + 1.25f;
        }

        public void OpenDeathLoot(DeathLootContainer container)
        {
            CloseInventory();
            CloseDeathLoot();
            if (container == null || container.Empty) return;
            ActiveDeathLoot = container;
            container.SetOpen(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseActiveDeathLoot() => CloseDeathLoot();

        public bool OpenInventory()
        {
            if (InventoryOpen) return true;
            if (State != MatchState.Active || Paused || Player == null || !Player.IsCombatCapable
                || ActiveVehicle != null || ActiveDeathLoot != null || (MatchShop != null && MatchShop.IsShopOpen))
                return false;
            InventoryOpen = true;
            input?.ResetTransientState();
            Player.Weapon?.CancelReloadForAction();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }

        public void CloseInventory()
        {
            if (!InventoryOpen) return;
            InventoryOpen = false;
            input?.ResetTransientState();
            MatchCursorPolicy.Apply(State, Paused);
        }

        public void ToggleInventory()
        {
            if (InventoryOpen) CloseInventory();
            else OpenInventory();
        }

        public void FillNearbyInventoryLoot(List<LootPickup> results)
        {
            if (results == null) return;
            if (Player == null || lootSpawner == null)
            {
                results.Clear();
                return;
            }
            lootSpawner.FillCollectibleCandidates(Player.transform.position,
                Mathf.Max(config != null ? config.interactRadius + 1.5f : 4f, 4f), results);
        }

        public bool CollectNearbyInventoryLoot(LootPickup pickup)
        {
            if (!InventoryOpen || pickup == null || Player == null || lootSpawner == null) return false;
            var maxDistance = Mathf.Max(config != null ? config.interactRadius + 1.5f : 4f, 4f);
            if ((pickup.transform.position - Player.transform.position).sqrMagnitude > maxDistance * maxDistance)
                return false;
            var itemName = PickupItemName(pickup);
            var result = pickup.CollectQuantitative(Player);
            if (result.Success) ShowPickupFeedback(itemName, result.AcceptedAmount);
            else ShowPickupFailure(result.FailureReason);
            return result.Success;
        }

        public bool DropAmmo(AmmoKind kind, int amount = 30)
        {
            if (!CanDropInventoryItem() || !Player.Inventory.TryRemoveAmmo(kind, amount, out var removed)) return false;
            lootSpawner.SpawnAmmoPickup(InventoryDropPoint(), kind, removed);
            return true;
        }

        public bool DropMedKit()
        {
            if (!CanDropInventoryItem() || !Player.Inventory.TryRemoveMedKits(1, out var removed)) return false;
            lootSpawner.SpawnHealPickup(InventoryDropPoint(), removed);
            return true;
        }

        public bool DropGlooWall()
        {
            if (!CanDropInventoryItem() || !Player.Inventory.TryRemoveGlooWalls(1, out var removed)) return false;
            lootSpawner.SpawnGlooWallPickup(InventoryDropPoint(), removed);
            return true;
        }

        public bool DropGrenade()
        {
            if (!CanDropInventoryItem() || !Player.Inventory.TryRemoveGrenades(1, out var removed)) return false;
            lootSpawner.SpawnGrenadePickup(InventoryDropPoint(), removed);
            return true;
        }

        public bool DropBackpack()
        {
            if (!CanDropInventoryItem() || Player.Backpack == null) return false;
            var removed = Player.Backpack.RemoveForDrop(true);
            if (removed == null)
            {
                ShowPickupFailure("REMOVA ITENS ANTES DE LARGAR A MOCHILA");
                return false;
            }
            lootSpawner.SpawnBackpackPickup(InventoryDropPoint(), removed);
            return true;
        }

        private bool CanDropInventoryItem() => InventoryOpen && Player != null && Player.IsCombatCapable
            && lootSpawner != null;

        private Vector3 InventoryDropPoint()
        {
            var desired = Player.transform.position + Player.transform.forward * 1.15f + Vector3.up * 1.2f;
            return Physics.Raycast(desired, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * 0.16f
                : Player.transform.position + Player.transform.forward * 1.15f + Vector3.up * 0.16f;
        }

        private void CloseDeathLoot()
        {
            if (ActiveDeathLoot != null) ActiveDeathLoot.SetOpen(false);
            ActiveDeathLoot = null;
            if (State == MatchState.Active)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnParticipantDied(HealthArmorSystem health)
        {
            if (State != MatchState.Active) return;
            var victim = health.GetComponent<BRParticipant>();
            if (victim == null || !processedDeaths.Add(victim)) return;
            if (victim != null)
            {
                var tracker = victim.GetComponent<DamageContributorTracker>();
                var elimination = tracker != null
                    ? tracker.Resolve(lifeConfig != null ? lifeConfig.AssistWindow : 10f)
                    : new EliminationInfo(victim,
                        health.LastDamageSource != null
                            ? health.LastDamageSource.GetComponent<BRParticipant>() : null,
                        null, health.LastDamageSource != null
                            && health.LastDamageSource.GetComponent<SafeZoneController>() != null
                                ? DamageType.SafeZone : DamageType.Unknown,
                        string.Empty, false, victim.transform.position, 0f, false, false,
                        string.Empty, string.Empty, Time.time);
                DeathLootContainer.Create(victim, lifeConfig);
                var killer = elimination.Killer;
                if (killer != null && killer != victim)
                {
                    if (killer.Stats != null) killer.Stats.RecordKill();
                    else killer.Eliminations++;
                    rewardSystem?.RewardElimination(killer);
                }
                if (elimination.Assistant != null)
                {
                    if (elimination.Assistant.Stats != null) elimination.Assistant.Stats.RecordAssist();
                    else elimination.Assistant.Assists++;
                }
                victim.Stats?.RecordDeath();
                DamageFeedbackSystem.ReportElimination(elimination);
                PlayerEliminated?.Invoke(elimination);
                victim.SetPhase(ParticipantPhase.Eliminated);
                var teamsBefore = AliveTeamCount;
                EliminateDownedWithoutActiveTeammates(victim.TeamId);
                if (BRMatchRules.CountSurvivingTeams(participants) < teamsBefore)
                    SquadEliminated?.Invoke(victim.TeamId, Mathf.Max(1, teamsBefore));
            }
            UpdateAlive();
            if (Player != null && victim != null && victim.TeamId == Player.TeamId
                && !BRMatchRules.TeamHasActiveMember(Player.TeamId, participants))
                playerPlacement = Mathf.Max(1, AliveTeamCount + 1);
            var localTeamEliminated = Player != null && victim != null && victim.TeamId == Player.TeamId
                && !BRMatchRules.TeamHasActiveMember(Player.TeamId, participants);
            if (localTeamEliminated) CompleteLocalElimination();
            else
            {
                if (victim != null && victim.IsPlayer)
                {
                    foreach (var participant in participants)
                    {
                        if (participant == null || participant.TeamId != victim.TeamId
                            || !participant.IsCombatCapable) continue;
                        thirdPersonCamera?.SetTarget(participant.transform);
                        break;
                    }
                }
                if (AliveTeamCount <= 1) Complete();
            }
        }

        private void OnParticipantDowned(HealthArmorSystem health)
        {
            if (State != MatchState.Active) return;
            var participant = health.GetComponent<BRParticipant>();
            if (participant != null) PlayerKnocked?.Invoke(participant);
            UpdateAlive();
        }

        private void OnParticipantRevived(HealthArmorSystem health)
        {
            var participant = health.GetComponent<BRParticipant>();
            if (participant != null) PlayerRevived?.Invoke(participant);
        }

        private void TickBleedout()
        {
            foreach (var participant in participants)
            {
                if (State != MatchState.Active) break;
                if (participant != null && participant.Health != null)
                    participant.Health.TickBleedout(Time.deltaTime);
            }
        }

        private void EliminateDownedWithoutActiveTeammates(int teamId)
        {
            if (BRMatchRules.TeamHasActiveMember(teamId, participants)) return;
            var downed = new List<BRParticipant>();
            foreach (var participant in participants)
                if (participant != null && participant.TeamId == teamId
                    && BRMatchRules.ShouldEliminateDownedTeamMember(participant, participants))
                    downed.Add(participant);
            foreach (var participant in downed) participant.Health.Eliminate();
        }

        private void UpdateAlive()
        {
            if (State == MatchState.Complete) return;
            var previousAlive = AliveCount;
            var previousTeams = AliveTeamCount;
            var alive = 0;
            foreach (var participant in participants)
                if (participant != null && !participant.Health.IsDead) alive++;
            AliveCount = alive;
            AliveTeamCount = BRMatchRules.CountSurvivingTeams(participants);
            if (previousAlive != AliveCount || previousTeams != AliveTeamCount)
                AliveCountChanged?.Invoke(AliveCount, AliveTeamCount);
        }

        private void Complete() => CompleteInternal(false);

        private void CompleteLocalElimination() => CompleteInternal(true);

        private void CompleteInternal(bool forceLocalElimination)
        {
            if (State == MatchState.Complete) return;
            UpdateAlive();
            if (AliveTeamCount > 1 && !forceLocalElimination) return;
            winningTeamId = -1;
            if (!forceLocalElimination)
            {
                foreach (var participant in participants)
                {
                    if (participant == null || participant.Health.IsDead) continue;
                    winningTeamId = participant.TeamId;
                    participant.Health.SetInvulnerable(float.PositiveInfinity);
                    participant.Stats?.FinalizeSurvival();
                }
                if (winningTeamId >= 0) MatchWinnerDetermined?.Invoke(winningTeamId);
            }
            CancelPlayerRevive();
            CloseInventory();
            ActiveVehicle?.ForceExitAll();
            MatchShop?.CloseShop();
            airdropManager?.ResetRuntime();
            SafeZone?.StopSafeZone();
            State = MatchState.Complete;
            MatchCursorPolicy.Apply(State, Paused);
            var placement = playerPlacement > 0
                ? playerPlacement
                : BRMatchRules.CalculateTeamPlacement(Player, participants);
            var kills = Player?.Eliminations ?? 0;
            var assists = Player?.Assists ?? 0;
            var damage = Player?.DamageDealt ?? 0;
            var revives = Player?.Revives ?? 0;
            if (!rankedResultApplied && accountProfile != null
                && accountProfile.State == ProfileLoadState.Loaded)
            {
                LastProgressionResult = accountProfile.Progression.CompleteMatch(new MatchCompletionRequest
                {
                    operationId = "match_completion:" + activeMatchId,
                    matchId = activeMatchId,
                    playerId = accountProfile.Data.playerId,
                    mode = Mode,
                    playlist = Playlist,
                    placement = placement,
                    teamCount = Mathf.Max(1, initialTeamCount),
                    kills = kills,
                    assists = assists,
                    damage = damage,
                    revives = revives,
                    survivalSeconds = Mathf.Max(0, Mathf.RoundToInt(Time.time - startedAt))
                });
                LastRankedResult = LastProgressionResult.RankedResult;
                rankedResultApplied = true;
                RefreshRankedProfile();
            }
            else if (Playlist == BRPlaylist.Ranked && !rankedResultApplied)
            {
                LastRankedResult = RankedProgression.ApplyResult(Mode, Playlist, placement,
                    Mathf.Max(1, initialTeamCount), kills, damage, revives);
                rankedResultApplied = true;
                RefreshRankedProfile();
            }
            ResultText = BuildResultText(placement, kills, assists, damage, revives, LastRankedResult,
                Time.time - startedAt);
            if (LastProgressionResult != null && LastProgressionResult.Success)
                ResultText += $"\nCONTA +{LastProgressionResult.AccountXPGained} XP"
                    + $"  |  COINS +{LastProgressionResult.CoinsGained}"
                    + $"\nPASSE +{LastProgressionResult.BattlePassXPGained} XP"
                    + $"  |  NIVEL {LastProgressionResult.PreviousLevel} > {LastProgressionResult.Level}";
        }

        public static string BuildResultText(int placement, int kills, int damage, int revives,
            RankedMatchResult rankedResult, float elapsedSeconds) =>
            BuildResultText(placement, kills, 0, damage, revives, rankedResult, elapsedSeconds);

        public static string BuildResultText(int placement, int kills, int assists, int damage, int revives,
            RankedMatchResult rankedResult, float elapsedSeconds)
        {
            var text = $"COLOCAÇÃO #{Mathf.Max(1, placement)}\n"
                + $"Eliminações: {Mathf.Max(0, kills)}  |  Assistências: {Mathf.Max(0, assists)}"
                + $"  |  Dano: {Mathf.Max(0, damage)}  |  Revives: {Mathf.Max(0, revives)}";
            if (rankedResult != null)
            {
                var score = rankedResult.Breakdown;
                text += $"\nColocação {Signed(score.PlacementPoints)}  Kills {Signed(score.KillPoints)}"
                    + $"  Dano {Signed(score.DamagePoints)}  Revives {Signed(score.RevivePoints)}"
                    + $"\nVitória {Signed(score.VictoryBonus)}  Entrada -{score.EntryCost}"
                    + $"  |  DELTA {Signed(score.Delta)}"
                    + $"\n{rankedResult.Tier.ToString().ToUpperInvariant()}  {rankedResult.Rating} RP";
            }
            return text + $"\nTempo: {Mathf.Max(0f, elapsedSeconds):0}s";
        }

        private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

        private void RefreshRankedProfile()
        {
            if (accountProfile != null && accountProfile.State == ProfileLoadState.Loaded)
            {
                var ranked = accountProfile.Ranked.Get(Mode);
                RankedProfile = new RankedProfile
                {
                    mode = AccountRankedService.NormalizeMode(Mode),
                    rating = ranked.rating,
                    matches = ranked.matches,
                    victories = ranked.victories,
                    kills = ranked.kills,
                    damage = ranked.damage,
                    revives = ranked.revives,
                    bestPlacement = ranked.bestPlacement
                };
                return;
            }
            RankedProfile = GamePreferences.LoadRankedProfile(Mode);
        }

        private void ClearParticipants()
        {
            CancelPlayerRevive();
            CloseDeathLoot();
            DeathLootContainer.ClearAll();
            foreach (var participant in participants)
                if (participant != null)
                {
                    participant.Health.Downed -= OnParticipantDowned;
                    participant.Health.Revived -= OnParticipantRevived;
                    participant.Health.Died -= OnParticipantDied;
                    participant.gameObject.SetActive(false);
                    Destroy(participant.gameObject);
                }
            participants.Clear();
            Player = null;
        }
    }
}
