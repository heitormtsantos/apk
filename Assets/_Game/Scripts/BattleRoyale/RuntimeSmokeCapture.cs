#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BattleRoyale
{
    public static class RuntimeSmokeAcceptance
    {
        public const string ImportedMapName = WorldMapGeometry.BermudaMapName;
        public const string IndoorFixturePath =
            "User Supplied Clock Tower Map/root/GLTF_SceneRootNode/Plane_0/Object_4";
        public const string DoorStructurePath =
            "User Supplied Clock Tower Map/root/GLTF_SceneRootNode/mansion_LOD0_0_7/Object_18";
        public const string DoorGroundPath =
            "User Supplied Clock Tower Map/root/GLTF_SceneRootNode/mansion_LOD0_1_8/Object_20";
        public const string WindowFramePath =
            "User Supplied Clock Tower Map/root/GLTF_SceneRootNode/Zone_A_House_Huge_A_LOD0_0_29/Object_62";
        public const string WindowSidePath =
            "User Supplied Clock Tower Map/root/GLTF_SceneRootNode/Zone_A_House_Huge_A_LOD0_3_32/Object_68";
        public const string WindowGroundPath =
            "User Supplied Clock Tower Map/root/GLTF_SceneRootNode/Base_02_LOD0_0_1/Object_6";

        public static bool HealingIncreased(bool started, float before, float after, bool active) =>
            started && after > before && !active;

        public static bool IsIndoorFixturePath(string hierarchyPath) =>
            string.Equals(hierarchyPath, IndoorFixturePath, StringComparison.Ordinal);
    }

    public sealed class RuntimeSmokeCapture : MonoBehaviour
    {
        private static readonly string[] RequiredMarkers =
        {
            "LOBBY", "LOBBY_PRESENTATION", "LOBBY_ICONS", "STAGING", "PLANE", "EJECT", "PARACHUTE",
            "PARACHUTE_STEERING", "HEAL", "PRESENTATION",
            "BERMUDA_GEOMETRY", "BERMUDA_TRAVERSAL", "BERMUDA_CAMERA",
            "FREE_LOOK", "DRAG_FIRE", "HIP_FIRE_OVERRIDE", "HIP_FIRE_HEAD_SLOWDOWN", "DESKTOP_INPUT",
            "ADS", "OPTIC_VARIANTS", "SOFT_ADS", "COVERED_TARGET", "INDOOR_FIXTURE", "INDOOR_CAMERA", "SHOT", "SHOTGUN", "DEATH_LOOT", "LOOT_SELECT",
            "ACTIVE", "MAP", "BOT_LOOT", "PAUSE", "PRE_RESTART", "RESTART", "RESTART_INPUT",
            "MATCH_CONFIG", "RETURN_LOBBY"
        };

        private readonly HashSet<string> observedMarkers = new();
        private readonly HashSet<string> failureReasons = new();
        private readonly Dictionary<Behaviour, bool> originalBehaviourStates = new();
        private readonly Dictionary<CharacterController, ControllerState> originalControllerStates = new();
        private readonly List<GameObject> temporaryObjects = new();
        private MatchManager trackedMatch;
        private MatchHUD trackedHud;
        private bool originalMatchEnabled;
        private bool matchStateTracked;
        private bool cleanupComplete;
        private bool resultLogged;
        private bool logHandlerRegistered;
        private float originalTimeScale;

        private IEnumerator Start()
        {
            Application.logMessageReceived += OnRuntimeLog;
            logHandlerRegistered = true;
            originalTimeScale = Time.timeScale;
            var routines = new Stack<IEnumerator>();
            routines.Push(RunSmoke());
            while (routines.Count > 0)
            {
                object current = null;
                bool moved;
                try
                {
                    moved = routines.Peek().MoveNext();
                    if (moved) current = routines.Peek().Current;
                }
                catch (Exception exception)
                {
                    Fail($"UNHANDLED_EXCEPTION:{exception.GetType().Name}:{exception.Message}");
                    Debug.LogException(exception);
                    break;
                }

                if (!moved)
                {
                    routines.Pop();
                    continue;
                }
                if (current is IEnumerator nested)
                {
                    routines.Push(nested);
                    continue;
                }
                yield return current;
            }

            FinishAndQuit();
        }

        private IEnumerator RunSmoke()
        {
            yield return new WaitForSeconds(0.6f);
            var match = FindAnyObjectByType<MatchManager>();
            TrackMatch(match);
            var lobbyAnimator = FindObjectsByType<CharacterVisualAnimator>()
                .FirstOrDefault(animator => animator.name.Contains("Showcase"));
            var lobbyWardrobe = lobbyAnimator != null
                ? lobbyAnimator.GetComponent<CharacterClothingManager>() : null;
            Debug.Log($"BR_SMOKE_LOBBY=STATE={match?.State};PARTICIPANTS={match?.Participants.Count};REGISTRY={ParticipantRegistry.Count};ANIM={lobbyAnimator?.CurrentState};PROFILE={lobbyAnimator?.ProfileId}");
            Mark("LOBBY");
            Check(match != null, "LOBBY:NO_MATCH_MANAGER");
            Check(match != null && match.State == MatchState.Lobby, $"LOBBY:STATE={match?.State}");
            Check(lobbyAnimator != null && lobbyAnimator.ProfileId == CharacterVisualCatalog.Lobby.Id,
                $"LOBBY:CHARACTER_PROFILE={lobbyAnimator?.ProfileId}");
            Check(lobbyWardrobe != null && lobbyWardrobe.Initialized,
                $"LOBBY:WARDROBE_READY={lobbyWardrobe?.Initialized}");
            Check(lobbyWardrobe != null && lobbyWardrobe.EquippedItems.Count >= 4,
                $"LOBBY:WARDROBE_ITEMS={lobbyWardrobe?.EquippedItems.Count ?? 0}");
            Check(match != null && match.Participants.Count == 0 && ParticipantRegistry.Count == 0,
                $"LOBBY:DIRTY_PARTICIPANTS={match?.Participants.Count}:REGISTRY={ParticipantRegistry.Count}");
            if (match == null) yield break;
            var lobbyCharacter = GameObject.Find("Main Lobby Character Showcase");
            var lobbyModel = lobbyCharacter != null
                ? lobbyCharacter.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(candidate => candidate.name.StartsWith("Lobby Raven Unit"))?.gameObject
                : null;
            var lobbyRenderers = lobbyModel != null
                ? lobbyModel.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            var lobbyCamera = Camera.main;
            var renderedHeight = 0f;
            var horizontalOffset = 0f;
            if (lobbyRenderers.Length > 0 && lobbyCamera != null)
            {
                var bounds = lobbyRenderers[0].bounds;
                for (var i = 1; i < lobbyRenderers.Length; i++) bounds.Encapsulate(lobbyRenderers[i].bounds);
                renderedHeight = Mathf.Abs(lobbyCamera.WorldToScreenPoint(bounds.max).y
                    - lobbyCamera.WorldToScreenPoint(bounds.min).y);
                horizontalOffset = lobbyCamera.WorldToScreenPoint(bounds.center).x - Screen.width * 0.5f;
            }
            var lobbyFacing = lobbyCharacter != null && lobbyCamera != null
                ? Vector3.Dot(lobbyCharacter.transform.forward,
                    Vector3.ProjectOnPlane(lobbyCamera.transform.position - lobbyCharacter.transform.position,
                        Vector3.up).normalized)
                : -1f;
            var lobbyLayout = MatchLobbyLayout.Balanced(new Rect(0f, 0f, Screen.width, Screen.height),
                match.TeamSize);
            var importedBackdrop = GameObject.Find(RuntimeSmokeAcceptance.ImportedMapName) != null;
            var lobbyPlatform = GameObject.Find("Lobby Staging Platform") != null;
            var lobbyHangarObject = GameObject.Find("Raven Lobby Hangar");
            var lobbyHangar = lobbyHangarObject != null;
            var premiumHangar = GameObject.Find("Premium Operations Hangar") != null;
            var panoramicWindows = GameObject.Find("Panoramic Window Array") != null;
            var equipmentProps = GameObject.Find("Lobby Equipment Props") != null;
            var companionDrone = GameObject.Find("Raven Companion Drone") != null;
            var backgroundVehicle = GameObject.Find("Background Armored Vehicle Bay") != null;
            var ambientParticles = GameObject.Find("Lobby Ambient Particles") != null;
            var heldWeapon = GameObject.Find("Lobby Held SCAR-H") != null;
            var lobbyLights = lobbyHangarObject != null
                ? lobbyHangarObject.GetComponentsInChildren<Light>(true).Length : 0;
            Debug.Log($"BR_SMOKE_LOBBY_PRESENTATION=HEIGHT={renderedHeight:0.0};OFFSET={horizontalOffset:0.0};FOV={lobbyCamera?.fieldOfView:0.0};FACING={lobbyFacing:0.00};MAP={importedBackdrop};PLATFORM={lobbyPlatform};HANGAR={lobbyHangar};PREMIUM={premiumHangar};WINDOWS={panoramicWindows};PROPS={equipmentProps};DRONE={companionDrone};VEHICLE={backgroundVehicle};PARTICLES={ambientParticles};WEAPON={heldWeapon};LIGHTS={lobbyLights};FOCUS={lobbyLayout.CharacterFocus}");
            Mark("LOBBY_PRESENTATION");
            Check(renderedHeight >= Screen.height * 0.60f && renderedHeight <= Screen.height * 0.75f,
                $"LOBBY_PRESENTATION:CHARACTER_SCALE={renderedHeight:0.0}");
            Check(horizontalOffset >= Screen.width * 0.03f && horizontalOffset <= Screen.width * 0.12f,
                $"LOBBY_PRESENTATION:CHARACTER_OFFSET={horizontalOffset:0.0}");
            Check(lobbyCamera != null && lobbyCamera.fieldOfView <= 50f,
                $"LOBBY_PRESENTATION:FOV={lobbyCamera?.fieldOfView:0.0}");
            Check(lobbyFacing > 0.7f, $"LOBBY_PRESENTATION:FACING={lobbyFacing:0.00}");
            Check(importedBackdrop, "LOBBY_PRESENTATION:IMPORTED_MAP_MISSING");
            Check(lobbyPlatform, "LOBBY_PRESENTATION:PLATFORM_MISSING");
            Check(lobbyHangar, "LOBBY_PRESENTATION:HANGAR_MISSING");
            Check(premiumHangar && panoramicWindows && equipmentProps,
                "LOBBY_PRESENTATION:PREMIUM_ENVIRONMENT_INCOMPLETE");
            Check(companionDrone && backgroundVehicle && ambientParticles && heldWeapon,
                "LOBBY_PRESENTATION:CINEMATIC_DETAILS_INCOMPLETE");
            Check(lobbyLights is > 0 and <= 3, $"LOBBY_PRESENTATION:DYNAMIC_LIGHTS={lobbyLights}");
            var lobbyIconsReady = LobbyIconCatalog.AllAvailable();
            Debug.Log($"BR_SMOKE_LOBBY_ICONS=READY={lobbyIconsReady};COUNT={LobbyIconCatalog.Count}");
            Mark("LOBBY_ICONS");
            Check(lobbyIconsReady, "LOBBY_ICONS:MISSING_RESOURCE");
            var hudIconsReady = HudIconCatalog.AllAvailable();
            Debug.Log($"BR_SMOKE_HUD_ICONS=READY={hudIconsReady};COUNT={HudIconCatalog.Count}");
            Check(hudIconsReady, "HUD_ICONS:MISSING_RESOURCE");
            var teamSmoke = Environment.GetCommandLineArgs().Any(argument =>
                string.Equals(argument, "-brTeamSmoke", StringComparison.OrdinalIgnoreCase));
            if (teamSmoke)
            {
                Check(match.SetMode(BRMatchMode.Duo), "MATCH_CONFIG:DUO_REJECTED");
                Check(match.SetPlaylist(BRPlaylist.Ranked), "MATCH_CONFIG:RANKED_REJECTED");
            }
            Debug.Log($"BR_SMOKE_MATCH_CONFIG=MODE={match.Mode};PLAYLIST={match.Playlist};TEAM_SIZE={match.TeamSize}");
            Mark("MATCH_CONFIG");
            Check(!teamSmoke || (match.Mode == BRMatchMode.Duo && match.Playlist == BRPlaylist.Ranked
                    && match.TeamSize == 2),
                $"MATCH_CONFIG:MODE={match.Mode}:PLAYLIST={match.Playlist}:TEAM_SIZE={match.TeamSize}");
            yield return Capture("br-01-lobby.png");
            var smokeHud = FindAnyObjectByType<MatchHUD>();
            trackedHud = smokeHud;
            smokeHud?.OpenWardrobe();
            yield return Capture("br-01-wardrobe.png");
            smokeHud?.CloseLobbyDrawer();
            smokeHud?.OpenHudEditor(false);
            yield return Capture("br-01-hud-editor-desktop.png");
            smokeHud?.CancelHudEditor();
            smokeHud?.OpenHudEditor(true);
            yield return Capture("br-01-hud-editor-mobile.png");
            smokeHud?.CancelHudEditor();

            match?.StartMatch();
            Debug.Log($"BR_SMOKE_STAGING_SPAWN=ROOT={match?.Player?.transform.position}");
            yield return new WaitForSeconds(1.4f);
            var runCount = match?.Participants.Count(participant => participant != null
                && participant.GetComponent<CharacterVisualAnimator>()?.CurrentState == CharacterAnimationState.Run) ?? 0;
            var groundedCount = match?.Participants.Count(participant => participant != null && participant.Motor.IsGrounded) ?? 0;
            Debug.Log($"BR_SMOKE_STAGING=STATE={match?.State};PARTICIPANTS={match?.Participants.Count};REGISTRY={ParticipantRegistry.Count};GROUNDED={groundedCount};RUNNING={runCount};TIME={match?.PhaseTimeRemaining:0.0}");
            Mark("STAGING");
            Check(match.State == MatchState.Staging, $"STAGING:STATE={match.State}");
            Check(match.Player != null && match.Participants.Count > 1, "STAGING:PLAYER_OR_PARTICIPANTS_MISSING");
            if (teamSmoke && match.Player != null)
            {
                var teammateCount = match.Participants.Count(participant => participant != null
                    && participant != match.Player && participant.TeamId == match.Player.TeamId);
                Debug.Log($"BR_SMOKE_TEAM_ASSIGNMENT=PLAYER_TEAM={match.Player.TeamId};TEAMMATES={teammateCount};TEAMS={match.AliveTeamCount}");
                Check(teammateCount == 1, $"TEAM_ASSIGNMENT:TEAMMATES={teammateCount}");
                Check(match.AliveTeamCount == match.Participants.Count / 2,
                    $"TEAM_ASSIGNMENT:TEAMS={match.AliveTeamCount}:PARTICIPANTS={match.Participants.Count}");
            }
            Check(groundedCount == match.Participants.Count,
                $"STAGING:NOT_GROUNDED={groundedCount}/{match.Participants.Count}");
            var stagingPlayer = match?.Player;
            var stagingMeshes = stagingPlayer != null ? stagingPlayer.GetComponentsInChildren<SkinnedMeshRenderer>(true) : null;
            var renderedMinimum = stagingMeshes != null && stagingMeshes.Length > 0
                ? stagingMeshes.Min(renderer => renderer.bounds.min.y) : float.NaN;
            var stagingIsland = GameObject.Find("Pre-Match Staging Island")?.GetComponent<Renderer>();
            Debug.Log($"BR_SMOKE_GROUNDING=ROOT_Y={stagingPlayer?.transform.position.y:0.000};RENDER_MIN_Y={renderedMinimum:0.000};ISLAND_TOP_Y={stagingIsland?.bounds.max.y:0.000};MODEL_LOCAL_Y={stagingMeshes?.FirstOrDefault()?.transform.root.position.y:0.000}");
            if (stagingPlayer != null)
            {
                var rayOrigin = new Vector3(stagingPlayer.transform.position.x, 100f, stagingPlayer.transform.position.z);
                var surfaces = Physics.RaycastAll(rayOrigin, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore)
                    .OrderByDescending(hit => hit.point.y)
                    .Select(hit => $"{hit.collider.name}@{hit.point.y:0.000}");
                Debug.Log($"BR_SMOKE_STAGING_SURFACES={string.Join(",", surfaces)}");
            }
            yield return Capture("br-02-staging.png");

            var stagingDeadline = Time.time + 15f;
            while (match != null && match.State != MatchState.Plane && Time.time < stagingDeadline) yield return null;
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"BR_SMOKE_PLANE=STATE={match?.State};PASSENGERS={match?.PassengersRemaining};VISIBLE_WAITING={match?.Drop.VisibleWaitingPassengerCount(match.Participants)};ALIGN={match?.Drop.PlaneForwardAlignment:0.000};MODEL={GameObject.Find("Imported Deployment Cargo Plane") != null}");
            Mark("PLANE");
            Check(match.State == MatchState.Plane, $"PLANE:STATE={match.State}");
            Check(GameObject.Find("Imported Deployment Cargo Plane") != null, "PLANE:MODEL_MISSING");
            yield return Capture("br-03-plane.png");

            var player = match?.Player;
            if (match != null && player != null) match.Drop.TickPlane(match.Participants, player, true);
            yield return new WaitForSeconds(0.2f);
            var playerVisible = player != null && player.GetComponent<DeploymentVisibility>()?.Visible == true;
            Debug.Log($"BR_SMOKE_EJECT=PHASE={player?.Phase};VISIBLE={playerVisible};BELOW_PLANE={(player != null ? match.Drop.PlanePosition.y - player.transform.position.y : 0f):0.0}");
            Mark("EJECT");
            Check(player != null
                && (player.Phase is ParticipantPhase.Freefall or ParticipantPhase.Parachute)
                && playerVisible,
                $"EJECT:PHASE={player?.Phase}:VISIBLE={playerVisible}");
            var deploymentCamera = Camera.main != null ? Camera.main.GetComponent<ThirdPersonCamera>() : null;
            var steeringStart = player != null ? player.transform.position : Vector3.zero;
            var steeringDirection = deploymentCamera != null
                ? DeploymentSteering.CameraRelativeTravel(Vector2.up, deploymentCamera.PlanarForward,
                    deploymentCamera.PlanarRight, player != null ? player.transform.forward : Vector3.forward)
                : Vector3.forward;
            if (match != null && player != null)
                match.Drop.TickFalling(player, Vector2.up, true,
                    deploymentCamera != null ? deploymentCamera.PlanarForward : Vector3.forward,
                    deploymentCamera != null ? deploymentCamera.PlanarRight : Vector3.right);
            var steeringDelta = player != null ? player.transform.position - steeringStart : Vector3.zero;
            yield return new WaitForSeconds(0.3f);
            var importedCanopy = GameObject.Find("Imported Parachute Canopy");
            var parachuteTextured = HasPbrTextures(importedCanopy);
            Debug.Log($"BR_SMOKE_PARACHUTE=PHASE={player?.Phase};MODEL={importedCanopy != null};TEXTURED={parachuteTextured}");
            Mark("PARACHUTE");
            Check(player != null && player.Phase == ParticipantPhase.Parachute,
                $"PARACHUTE:PHASE={player?.Phase}");
            Check(importedCanopy != null, "PARACHUTE:IMPORTED_MODEL_MISSING");
            Check(parachuteTextured, "PARACHUTE:PBR_TEXTURES_MISSING");
            var cameraBehind = false;
            var cameraDistance = 0f;
            if (player != null && deploymentCamera != null)
            {
                var playerToCamera = deploymentCamera.transform.position - player.transform.position;
                var planarCamera = Vector3.ProjectOnPlane(playerToCamera, Vector3.up);
                cameraDistance = playerToCamera.magnitude;
                cameraBehind = planarCamera.sqrMagnitude > 0.0001f
                    && Vector3.Dot(planarCamera.normalized, player.transform.forward) < -0.7f
                    && deploymentCamera.transform.position.y > player.transform.position.y;
            }
            var steeringAligned = steeringDirection.sqrMagnitude > 0.0001f
                && Vector3.Dot(Vector3.ProjectOnPlane(steeringDelta, Vector3.up).normalized,
                    steeringDirection.normalized) > 0.9f;
            Debug.Log($"BR_SMOKE_PARACHUTE_STEERING=ALIGNED={steeringAligned};BEHIND={cameraBehind};DISTANCE={cameraDistance:0.00};DELTA={FormatVector(steeringDelta)}");
            Mark("PARACHUTE_STEERING");
            Check(steeringAligned, $"PARACHUTE_STEERING:DIRECTION={FormatVector(steeringDelta)}");
            Check(cameraBehind && cameraDistance >= 5.5f && cameraDistance <= 8.5f,
                $"PARACHUTE_STEERING:CAMERA_BEHIND={cameraBehind}:DISTANCE={cameraDistance:0.00}");
            yield return Capture("br-04-parachute.png");

            if (player != null) player.Motor.Teleport(new Vector3(0f, 3f, 0f));
            var landingDeadline = Time.time + 4f;
            while (match != null && match.State != MatchState.Active && Time.time < landingDeadline) yield return null;
            Check(match.State == MatchState.Active, $"ACTIVE_ENTRY:STATE={match.State}");
            var shopTerminal = FindAnyObjectByType<MatchShopTerminal>();
            var shopItem = shopTerminal?.shopDatabase?.GetById("gloo-wall");
            var shopOpened = false;
            var shopResult = PurchaseResult.ShopUnavailable;
            var creditsBeforeShop = player?.Wallet.CurrentCurrency ?? 0;
            var wallsBeforeShop = player?.Inventory.GlooWalls ?? 0;
            if (player != null && shopTerminal != null && shopItem != null && match.MatchShop != null)
            {
                player.Motor.Teleport(shopTerminal.InteractionPosition);
                Physics.SyncTransforms();
                shopOpened = match.MatchShop.OpenShop(shopTerminal);
                if (shopOpened) shopResult = match.MatchShop.RequestPurchase(shopItem, Time.time);
                match.MatchShop.CloseShop();
            }
            var creditsAfterShop = player?.Wallet.CurrentCurrency ?? 0;
            var wallsAfterShop = player?.Inventory.GlooWalls ?? 0;
            Debug.Log($"BR_SMOKE_MATCH_SHOP=TERMINAL={shopTerminal != null};ITEM={shopItem != null};" +
                      $"OPENED={shopOpened};RESULT={shopResult};CREDITS={creditsBeforeShop}->{creditsAfterShop};" +
                      $"GLOO={wallsBeforeShop}->{wallsAfterShop}");
            Mark("MATCH_SHOP");
            Check(shopOpened && shopResult == PurchaseResult.Success
                && creditsAfterShop == creditsBeforeShop - shopItem.price
                && wallsAfterShop == wallsBeforeShop + shopItem.quantityGranted,
                $"MATCH_SHOP:OPENED={shopOpened}:RESULT={shopResult}:CREDITS={creditsBeforeShop}->{creditsAfterShop}:" +
                $"GLOO={wallsBeforeShop}->{wallsAfterShop}");
            foreach (var pickup in FindObjectsByType<LootPickup>())
            {
                if (pickup.Kind != LootKind.Weapon || pickup.Weapon == null
                    || pickup.Weapon.weaponClass == WeaponClass.Shotgun) continue;
                pickup.Collect(player);
                break;
            }
            var target = match?.Participants.FirstOrDefault(participant => participant != null && !participant.IsPlayer
                && !participant.Health.IsDead && BRMatchRules.AreEnemies(player, participant));
            if (player != null && target != null && player.Inventory.ActiveWeapon != null)
            {
                SetMatchEnabled(match, false);
                var automaticWeapon = match.WeaponCatalog.FirstOrDefault(candidate => candidate != null && candidate.automatic);
                if (automaticWeapon != null && player.Inventory.ActiveWeapon != automaticWeapon)
                    player.Weapon.EquipInitial(automaticWeapon);
                var weapon = player.Inventory.ActiveWeapon;
                player.Inventory.AddAmmo(weapon.ammoKind, weapon.magazineSize * 2);
                player.Health.SetInvulnerable(30f);
                FindCombatLane(out var laneStart, out var laneEnd);
                player.Motor.Teleport(laneStart + Vector3.up * 0.08f);
                player.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(laneEnd - laneStart, Vector3.up));
                target.gameObject.SetActive(true);
                target.GetComponent<DeploymentVisibility>()?.SetVisible(true);
                target.SetPhase(ParticipantPhase.Grounded);
                SetBehaviourEnabled(target.GetComponent<BotController>(), false);
                var smokeController = target.GetComponent<CharacterController>();
                SetControllerRadius(smokeController, 0.65f);
                var playerWeaponVisual = player.GetComponent<ParticipantWeaponVisual>();
                target.Motor.Teleport(laneEnd + Vector3.up * 0.08f);
                target.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(laneStart - laneEnd, Vector3.up));
                target.Health.InitializeFull();
                target.Health.EquipVest(0);
                target.Health.EquipHelmet(0);
                target.Weapon.EquipInitial(weapon);
                target.Inventory.AddAmmo(weapon.ammoKind, 48);
                target.Inventory.AddMedKit(2);
                target.Health.ApplyDamage(40f, false, player.gameObject);
                var healthBeforeHealing = target.Health.Health;
                var healingStarted = target.Inventory.TryUseMedKit(target.Health);
                yield return new WaitForSeconds(3.15f);
                var healthAfterHealing = target.Health.Health;
                Debug.Log($"BR_SMOKE_HEAL=STARTED={healingStarted};BEFORE={healthBeforeHealing:0};AFTER={healthAfterHealing:0};KITS={target.Inventory.MedKits};ACTIVE={target.Inventory.UsingMedKit}");
                Mark("HEAL");
                Check(RuntimeSmokeAcceptance.HealingIncreased(healingStarted, healthBeforeHealing,
                    healthAfterHealing, target.Inventory.UsingMedKit),
                    $"HEAL:INVALID:STARTED={healingStarted}:BEFORE={healthBeforeHealing:0.00}:AFTER={healthAfterHealing:0.00}:ACTIVE={target.Inventory.UsingMedKit}");
                target.Health.InitializeFull();
                target.Health.EquipVest(2);
                target.Health.EquipHelmet(1);
                player.Motor.Teleport(laneStart + Vector3.up * 0.08f);
                player.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(laneEnd - laneStart, Vector3.up));
                target.Motor.Teleport(laneEnd + Vector3.up * 0.08f);
                target.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(laneStart - laneEnd, Vector3.up));
                Physics.SyncTransforms();

                var gameplayCamera = Camera.main != null ? Camera.main.GetComponent<ThirdPersonCamera>() : null;
                if (gameplayCamera != null)
                {
                    gameplayCamera.SetTarget(player.transform);
                    var hipInput = new BRInputFrame(Vector2.zero, Vector2.zero, false, false, false, false,
                        false, false, false, false, false, false, -1, false, false);
                    var presentationFound = TryFindPresentationLocation(out var presentationPosition,
                        out var presentationLookAt, out var presentationSelection);
                    if (presentationFound)
                    {
                        player.Motor.Teleport(presentationPosition + Vector3.up * 0.08f);
                        var facing = Vector3.ProjectOnPlane(presentationLookAt - presentationPosition, Vector3.up);
                        if (facing.sqrMagnitude > 0.001f) player.transform.rotation = Quaternion.LookRotation(facing);
                        gameplayCamera.SetTarget(player.transform);
                    }
                    Debug.Log($"BR_SMOKE_PRESENTATION_SELECTION=FOUND={presentationFound};SELECTED={presentationSelection};POSITION={player.transform.position}");
                    Check(presentationFound, $"PRESENTATION:LOCATION_NOT_FOUND:{presentationSelection}");
                    for (var cameraFrame = 0; cameraFrame < 18; cameraFrame++)
                    {
                        gameplayCamera.Tick(hipInput);
                        yield return null;
                    }
                    LogPresentation(player, gameplayCamera);
                    yield return Capture("br-05-character-camera.png");

                    yield return RunImportedMapTraversal(player, gameplayCamera);

                    player.Motor.Teleport(laneStart + Vector3.up * 0.08f);
                    player.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(laneEnd - laneStart, Vector3.up));
                    target.Motor.Teleport(laneEnd + Vector3.up * 0.08f);
                    target.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(laneStart - laneEnd, Vector3.up));
                    Physics.SyncTransforms();
                    gameplayCamera.SetTarget(player.transform);
                    gameplayCamera.Tick(hipInput);
                    yield return Capture("br-05-hipfire.png");

                    var pointer = new MobilePointerState();
                    const int fireFinger = 17;
                    var fireRect = new Rect(900f, 80f, 100f, 100f);
                    var controls = new MobileControlRects(new Vector2(100f, 100f), 60f, fireRect,
                        new Rect(760f, 160f, 80f, 80f), new Rect(), new Rect(), new Rect(), new Rect(),
                        new Rect(), 500f);
                    var beganTouch = new[]
                    {
                        new MobileTouchSample(fireFinger, fireRect.center, Vector2.zero, TouchPhase.Began)
                    };
                    var routed = pointer.RouteTouches(beganTouch, controls, GamePreferences.FireDragSensitivity);

                    const int lookFinger = 18;
                    var freeLookStart = new Vector2(700f, 500f);
                    pointer.RouteTouches(new[]
                    {
                        new MobileTouchSample(lookFinger, freeLookStart, Vector2.zero, TouchPhase.Began)
                    }, controls, GamePreferences.FireDragSensitivity);
                    var freeLookYaw = gameplayCamera.CurrentYaw;
                    var shotsBeforeFreeLook = WeaponSystem.TotalShotsFired;
                    var freeLookRouted = pointer.RouteTouches(new[]
                    {
                        new MobileTouchSample(lookFinger, freeLookStart + new Vector2(75f, 0f),
                            new Vector2(75f, 0f), TouchPhase.Moved)
                    }, controls, GamePreferences.FireDragSensitivity);
                    var freeLookFrame = new BRInputFrame(Vector2.zero, freeLookRouted.Look, false, false,
                        false, false, freeLookRouted.Fire, freeLookRouted.FirePressed, false, false,
                        false, false, -1, false, false);
                    gameplayCamera.Tick(freeLookFrame);
                    var freeLookDelta = Mathf.Abs(Mathf.DeltaAngle(freeLookYaw, gameplayCamera.CurrentYaw));
                    var freeLookShots = WeaponSystem.TotalShotsFired - shotsBeforeFreeLook;
                    Debug.Log($"BR_SMOKE_FREE_LOOK=YAW_DELTA={freeLookDelta:0.0};FIRE={freeLookRouted.Fire};SHOTS={freeLookShots};ROUTED=MobilePointerState");
                    Mark("FREE_LOOK");
                    Check(freeLookDelta > 1f, $"FREE_LOOK:YAW_DELTA={freeLookDelta:0.0}");
                    Check(!freeLookRouted.Fire && freeLookShots == 0,
                        $"FREE_LOOK:FIRE={freeLookRouted.Fire}:SHOTS={freeLookShots}");
                    pointer.End(lookFinger);

                    var yawBeforeDrag = gameplayCamera.CurrentYaw;
                    var acceptedDragShots = 0;
                    target.Health.SetInvulnerable(1.4f);
                    var dragPixels = 110f / Mathf.Max(0.01f, 10f * MobilePointerState.LookScale
                        * GamePreferences.FireDragSensitivity * 2.2f * GamePreferences.LookSensitivity);
                    for (var dragFrame = 0; dragFrame < 10; dragFrame++)
                    {
                        var outsidePosition = new Vector2(fireRect.xMax + 40f + dragFrame * dragPixels, fireRect.center.y);
                        var movedTouch = new[]
                        {
                            new MobileTouchSample(fireFinger, outsidePosition, new Vector2(dragPixels, 0f), TouchPhase.Moved)
                        };
                        routed = pointer.RouteTouches(movedTouch, controls, GamePreferences.FireDragSensitivity);
                        var routedFrame = new BRInputFrame(routed.Move, routed.Look, routed.Jump, false,
                            routed.Sprint, routed.Aim, routed.Fire, routed.FirePressed, routed.Reload,
                            routed.Interact, routed.Heal, routed.Swap, -1, false, routed.Drop,
                            routed.InteractHeld, routed.FireDragDelta, AimInputDevice.Touch);
                        gameplayCamera.Tick(routedFrame);
                        if (routedFrame.Fire && (weapon.automatic || routedFrame.FirePressed)
                            && player.Weapon.TryFire(Camera.main, routedFrame.Aim, player.gameObject))
                            acceptedDragShots++;
                        yield return new WaitForSeconds(weapon.fireInterval + 0.015f);
                    }
                    var yawDelta = Mathf.Abs(Mathf.DeltaAngle(yawBeforeDrag, gameplayCamera.CurrentYaw));
                    var fireHeldOutside = pointer.IsFiring(fireFinger);
                    var pointerOutside = !fireRect.Contains(new Vector2(fireRect.xMax + 40f + 9f * dragPixels,
                        fireRect.center.y));
                    Debug.Log($"BR_SMOKE_DRAG_FIRE=YAW_DELTA={yawDelta:0.0};SHOTS={acceptedDragShots};FIRE_HELD={fireHeldOutside};OUTSIDE={pointerOutside};AUTOMATIC={weapon.automatic};DRAG={gameplayCamera.LastFireDragDelta};H={gameplayCamera.HorizontalAssistStrength:0.000};V={gameplayCamera.VerticalAssistStrength:0.000};STATE={gameplayCamera.VerticalAimState}");
                    Mark("DRAG_FIRE");
                    Check(yawDelta >= 70f, $"DRAG_FIRE:YAW_DELTA={yawDelta:0.0}");
                    Check(player.Weapon.Magazine >= 0 && acceptedDragShots >= 2,
                        $"DRAG_FIRE:SHOTS={acceptedDragShots}");
                    Check(fireHeldOutside && pointerOutside, "DRAG_FIRE:CAPTURE_LOST_OUTSIDE");
                    pointer.End(fireFinger);

                    player.Motor.Teleport(laneStart + Vector3.up * 0.08f);
                    player.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(laneEnd - laneStart, Vector3.up));
                    target.Motor.Teleport(laneEnd + Vector3.up * 0.08f);
                    Physics.SyncTransforms();
                    gameplayCamera.SetTarget(player.transform);
                    var hipSettleInput = new BRInputFrame(Vector2.zero, Vector2.zero, false, false,
                        false, false, true, true, false, false, false, false, -1, false, false,
                        false, Vector2.zero, AimInputDevice.Touch);
                    for (var settleFrame = 0; settleFrame < 3; settleFrame++)
                    {
                        gameplayCamera.Tick(hipSettleInput);
                        yield return null;
                    }
                    var upwardDrag = new Vector2(0f, weapon.dragBreakThresholdTouch * 1.25f);
                    var hipOverrideInput = new BRInputFrame(Vector2.zero, new Vector2(0f, 0.35f),
                        false, false, false, false, true, false, false, false, false, false, -1,
                        false, false, false, upwardDrag, AimInputDevice.Touch);
                    gameplayCamera.Tick(hipOverrideInput);
                    Debug.Log($"BR_SMOKE_HIP_FIRE_OVERRIDE=LOCKED={gameplayCamera.AimAssistLocked};STATE={gameplayCamera.VerticalAimState};H={gameplayCamera.HorizontalAssistStrength:0.000};V={gameplayCamera.VerticalAssistStrength:0.000};DRAG={gameplayCamera.LastFireDragDelta}");
                    Mark("HIP_FIRE_OVERRIDE");
                    Check(gameplayCamera.AimAssistLocked, "HIP_FIRE_OVERRIDE:NO_TARGET");
                    Check(gameplayCamera.VerticalAimState == VerticalAimState.VerticalOverride,
                        $"HIP_FIRE_OVERRIDE:STATE={gameplayCamera.VerticalAimState}");
                    Check(gameplayCamera.HorizontalAssistStrength > 0f
                        && Mathf.Approximately(gameplayCamera.VerticalAssistStrength, 0f),
                        $"HIP_FIRE_OVERRIDE:H={gameplayCamera.HorizontalAssistStrength:0.000}:V={gameplayCamera.VerticalAssistStrength:0.000}");
                    var pitchBeforeHeadDrag = gameplayCamera.CurrentPitch;
                    var minimumVerticalScale = 1f;
                    var passedHeadZone = false;
                    for (var headDragFrame = 0; headDragFrame < 45; headDragFrame++)
                    {
                        gameplayCamera.Tick(hipOverrideInput);
                        minimumVerticalScale = Mathf.Min(minimumVerticalScale,
                            gameplayCamera.LastAimSensitivityScale.y);
                        if (gameplayCamera.HeadScreenOffset.y < -weapon.headSlowdownRadiusNormalized * 1.7f)
                            passedHeadZone = true;
                        yield return null;
                    }
                    var headDragPitchDelta = Mathf.Abs(Mathf.DeltaAngle(pitchBeforeHeadDrag,
                        gameplayCamera.CurrentPitch));
                    Debug.Log($"BR_SMOKE_HIP_FIRE_HEAD_DRAG=SLOWEST={minimumVerticalScale:0.000};PASSED={passedHeadZone};LOCKED={gameplayCamera.AimAssistLocked};PITCH_DELTA={headDragPitchDelta:0.00};HEAD_OFFSET={gameplayCamera.HeadScreenOffset}");
                    Mark("HIP_FIRE_HEAD_SLOWDOWN");
                    Check(minimumVerticalScale >= 0.98f,
                        $"HIP_FIRE_HEAD_DRAG:SCALE={minimumVerticalScale:0.000}");
                    Check(passedHeadZone && headDragPitchDelta >= 18f && gameplayCamera.AimAssistLocked,
                        $"HIP_FIRE_HEAD_DRAG:PASSED={passedHeadZone}:PITCH_DELTA={headDragPitchDelta:0.00}:LOCKED={gameplayCamera.AimAssistLocked}:HEAD={gameplayCamera.HeadScreenOffset}");
                    gameplayCamera.SetTarget(player.transform);
                    for (var recenterFrame = 0; recenterFrame < 4; recenterFrame++)
                    {
                        gameplayCamera.Tick(hipSettleInput);
                        yield return null;
                    }
                    var adsInput = new BRInputFrame(Vector2.zero, Vector2.zero, false, false, false, true,
                        false, false, false, false, false, false, -1, false, false);
                    for (var aimFrame = 0; aimFrame < 26; aimFrame++)
                    {
                        gameplayCamera.Tick(adsInput);
                        yield return null;
                    }
                    var smokeScope = Camera.main.GetComponent<ScopeController>();
                    var scopeReady = smokeScope != null && smokeScope.ScopeActive
                        && smokeScope.CanvasOverlayReady && smokeScope.ActiveOverlay != null;
                    var adsCameraDistance = Vector3.Distance(gameplayCamera.transform.position,
                        player.transform.position + Vector3.up * CharacterPresentationProfile.CameraPivotHeight);
                    var localVisibility = player.GetComponent<LocalAdsVisibility>();
                    Debug.Log($"BR_SMOKE_ADS=LOCKED={gameplayCamera.AimAssistLocked};TARGET={gameplayCamera.AimAssistTarget?.DisplayName};INDOORS={gameplayCamera.Indoors};FOV={Camera.main.fieldOfView:0.0};MODE={gameplayCamera.ActiveScopeType};PRESENTATION={gameplayCamera.PresentationMode};DISTANCE={adsCameraDistance:0.000};PLAYER_HIDDEN={localVisibility?.Hidden};PROGRESS={gameplayCamera.AdsProgress:0.00};SCOPE_READY={scopeReady};PNG={smokeScope?.ActiveOverlay?.name}");
                    Mark("ADS");
                    Check(gameplayCamera.Aiming, "ADS:NOT_AIMING");
                    Check(gameplayCamera.AimAssistLocked, "ADS:AIM_ASSIST_NOT_LOCKED");
                    Check(gameplayCamera.AdsProgress >= 0.99f, $"ADS:TRANSITION={gameplayCamera.AdsProgress:0.00}");
                    Check(gameplayCamera.ActiveScopeType == ScopeType.Scope2X, $"ADS:SCOPE={gameplayCamera.ActiveScopeType}");
                    Check(gameplayCamera.FullFirstPersonAds && adsCameraDistance < 0.16f,
                        $"ADS:PRESENTATION={gameplayCamera.PresentationMode}:DISTANCE={adsCameraDistance:0.000}");
                    Check(localVisibility != null && localVisibility.Hidden, "ADS:LOCAL_PLAYER_VISIBLE");
                    Check(scopeReady, "ADS:SINGLE_CAMERA_SCOPE_NOT_READY");
                    Check(Camera.main != null && Camera.main.fieldOfView >= 39f && Camera.main.fieldOfView <= 41f,
                        $"ADS:FOV={Camera.main?.fieldOfView:0.0}");
                    yield return Capture("br-05-ads.png");

                    var redDotWeapon = match.WeaponCatalog.FirstOrDefault(candidate =>
                        candidate != null && candidate.scope.scopeType == ScopeType.RedDot);
                    Check(redDotWeapon != null, "OPTIC_VARIANTS:RED_DOT_WEAPON_MISSING");
                    if (redDotWeapon != null)
                    {
                        player.Weapon.EquipInitial(redDotWeapon);
                        for (var opticFrame = 0; opticFrame < 18; opticFrame++)
                        {
                            gameplayCamera.Tick(adsInput);
                            yield return null;
                        }
                        Check(smokeScope.ScopeActive && smokeScope.CanvasOverlayReady
                            && !smokeScope.UsingRenderTexture,
                            $"OPTIC_VARIANTS:RED_DOT_ACTIVE={smokeScope.ScopeActive}:CANVAS={smokeScope.CanvasOverlayReady}:RT={smokeScope.UsingRenderTexture}");
                        Check(gameplayCamera.ActiveScopeType == ScopeType.RedDot,
                            $"OPTIC_VARIANTS:RED_DOT_TYPE={gameplayCamera.ActiveScopeType}");
                        Check(Camera.main.fieldOfView >= 51f && Camera.main.fieldOfView <= 53f,
                            $"OPTIC_VARIANTS:RED_DOT_FOV={Camera.main.fieldOfView:0.0}");
                        yield return Capture("br-05-red-dot.png");
                    }

                    var sniperWeapon = match.WeaponCatalog.FirstOrDefault(candidate =>
                        candidate != null && candidate.scope.scopeType == ScopeType.Sniper);
                    Check(sniperWeapon != null, "OPTIC_VARIANTS:SNIPER_WEAPON_MISSING");
                    if (sniperWeapon != null)
                    {
                        player.Weapon.EquipInitial(sniperWeapon);
                        for (var opticFrame = 0; opticFrame < 38; opticFrame++)
                        {
                            gameplayCamera.Tick(adsInput);
                            yield return null;
                        }
                        Check(smokeScope.ScopeActive && smokeScope.CanvasOverlayReady
                            && !smokeScope.UsingRenderTexture && smokeScope.ActiveOverlay != null,
                            $"OPTIC_VARIANTS:SNIPER_ACTIVE={smokeScope.ScopeActive}:CANVAS={smokeScope.CanvasOverlayReady}:RT={smokeScope.UsingRenderTexture}");
                        Check(gameplayCamera.ActiveScopeType == ScopeType.Sniper,
                            $"OPTIC_VARIANTS:SNIPER_TYPE={gameplayCamera.ActiveScopeType}");
                        Check(Camera.main.fieldOfView >= 16f && Camera.main.fieldOfView <= 18.5f,
                            $"OPTIC_VARIANTS:SNIPER_OUTER_FOV={Camera.main.fieldOfView:0.0}");
                        yield return Capture("br-05-sniper.png");
                    }
                    Mark("OPTIC_VARIANTS");

                    yield return new WaitForSeconds(weapon.fireInterval + 0.02f);
                    player.Weapon.EquipInitial(weapon);
                    var desktopYawBefore = gameplayCamera.CurrentYaw;
                    var desktopMapped = DesktopInputMapper.Map(new Vector2(8f, -1f), true, true, true, false);
                    var desktopFrame = new BRInputFrame(Vector2.zero, desktopMapped.Look, false, false,
                        false, desktopMapped.Aim, desktopMapped.Fire, desktopMapped.FirePressed,
                        false, false, false, false, -1, false, false);
                    gameplayCamera.Tick(desktopFrame);
                    var desktopAccepted = player.Weapon.TryFire(Camera.main, desktopFrame.Aim, player.gameObject);
                    for (var desktopAdsFrame = 0; desktopAdsFrame < 20; desktopAdsFrame++)
                    {
                        var held = DesktopInputMapper.Map(Vector2.zero, false, false, true, false);
                        gameplayCamera.Tick(new BRInputFrame(Vector2.zero, held.Look, false, false, false,
                            held.Aim, held.Fire, held.FirePressed, false, false, false, false, -1,
                            false, false));
                        yield return null;
                    }
                    var desktopYawDelta = Mathf.Abs(Mathf.DeltaAngle(desktopYawBefore, gameplayCamera.CurrentYaw));
                    Debug.Log($"BR_SMOKE_DESKTOP_INPUT=YAW_DELTA={desktopYawDelta:0.0};FIRE_ACCEPTED={desktopAccepted};ADS={gameplayCamera.Aiming};FOV={Camera.main.fieldOfView:0.0};MAPPER=DesktopInputMapper");
                    Mark("DESKTOP_INPUT");
                    Check(desktopYawDelta > 1f, $"DESKTOP_INPUT:YAW_DELTA={desktopYawDelta:0.0}");
                    Check(desktopAccepted, "DESKTOP_INPUT:FIRE_NOT_ACCEPTED");
                    Check(gameplayCamera.Aiming && Camera.main.fieldOfView >= 39f
                        && Camera.main.fieldOfView <= 41f,
                        $"DESKTOP_INPUT:ADS={gameplayCamera.Aiming}:FOV={Camera.main.fieldOfView:0.0}");

                    yield return new WaitForSeconds(weapon.fireInterval + 0.02f);
                    player.Weapon.EquipInitial(weapon);
                    player.Motor.Teleport(laneStart + Vector3.up * 0.08f);
                    player.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(laneEnd - laneStart,
                        Vector3.up));
                    target.Motor.Teleport(laneEnd + Vector3.up * 0.08f);
                    target.Health.InitializeFull();
                    Physics.SyncTransforms();
                    gameplayCamera.SetTarget(player.transform);
                    var coverCenter = Vector3.Lerp(player.transform.position, target.transform.position, 0.5f);
                    coverCenter.y = Mathf.Max(player.transform.position.y, target.transform.position.y) + 1f;
                    var cover = CreateFixtureCube("BR Smoke Covered Target Wall", coverCenter,
                        Quaternion.LookRotation(Vector3.ProjectOnPlane(laneEnd - laneStart, Vector3.up)),
                        new Vector3(3f, 2f, 0.24f));
                    Physics.SyncTransforms();
                    for (var coverFrame = 0; coverFrame < 8; coverFrame++)
                    {
                        gameplayCamera.Tick(adsInput);
                        yield return null;
                    }
                    var coverCameraObject = TrackTemporary(new GameObject("BR Smoke Covered Target Camera"));
                    var coverCamera = coverCameraObject.AddComponent<Camera>();
                    coverCamera.enabled = false;
                    var coverMuzzle = playerWeaponVisual != null
                        ? playerWeaponVisual.MuzzlePosition : CharacterPresentationProfile.FallbackMuzzle(player.transform);
                    coverCamera.transform.SetPositionAndRotation(coverMuzzle,
                        Quaternion.LookRotation(CharacterPresentationProfile.AimPoint(target.transform) - coverMuzzle));
                    var coveredHealthBefore = target.Health.Health;
                    var coveredAccepted = player.Weapon.TryFire(coverCamera, true, player.gameObject);
                    var coveredHealthAfter = target.Health.Health;
                    Debug.Log($"BR_SMOKE_COVERED_TARGET=CAMERA_LOCK={gameplayCamera.AimAssistLocked};WEAPON_LOCK={player.Weapon.AimAssistLocked};FIRE_ACCEPTED={coveredAccepted};HEALTH_BEFORE={coveredHealthBefore:0.0};HEALTH_AFTER={coveredHealthAfter:0.0};MUZZLE_BLOCKED={!player.Weapon.LastShotHit};FIXTURE=True");
                    Mark("COVERED_TARGET");
                    Check(!gameplayCamera.AimAssistLocked, "COVERED_TARGET:CAMERA_AIM_LOCK");
                    Check(!player.Weapon.AimAssistLocked, "COVERED_TARGET:WEAPON_AIM_LOCK");
                    Check(coveredAccepted && Mathf.Approximately(coveredHealthBefore, coveredHealthAfter),
                        $"COVERED_TARGET:FIRE={coveredAccepted}:HEALTH={coveredHealthBefore:0.0}->{coveredHealthAfter:0.0}");
                    Destroy(cover);
                    Destroy(coverCameraObject);
                    yield return null;

                    var indoorFound = TryFindIndoorLocation(out var indoorPosition, out var indoorSelection);
                    Debug.Log($"BR_SMOKE_INDOOR_FIXTURE=FOUND={indoorFound};SELECTED={indoorSelection};POSITION={indoorPosition}");
                    Mark("INDOOR_FIXTURE");
                    var indoorDistance = float.NaN;
                    var settledDelta = float.PositiveInfinity;
                    var wallCross = true;
                    if (indoorFound)
                    {
                        player.Motor.Teleport(indoorPosition + Vector3.up * 0.08f);
                        player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                        Physics.SyncTransforms();
                        gameplayCamera.SetTarget(player.transform);
                        var previousCameraPosition = gameplayCamera.transform.position;
                        for (var indoorFrame = 0; indoorFrame < 36; indoorFrame++)
                        {
                            gameplayCamera.Tick(hipInput);
                            if (indoorFrame >= 34)
                                settledDelta = Vector3.Distance(previousCameraPosition, gameplayCamera.transform.position);
                            previousCameraPosition = gameplayCamera.transform.position;
                            yield return null;
                        }
                        var pivot = player.transform.position + Vector3.up * CharacterPresentationProfile.CameraPivotHeight;
                        indoorDistance = Vector3.Distance(pivot, gameplayCamera.transform.position);
                        wallCross = HasBlockingGeometryBetween(pivot, gameplayCamera.transform.position, player);
                    }
                    Debug.Log($"BR_SMOKE_INDOOR_CAMERA=FOUND={indoorFound};SELECTED={indoorSelection};POSITION={indoorPosition};INDOORS={gameplayCamera.Indoors};DISTANCE={indoorDistance:0.00};WALL_CROSS={wallCross};SETTLED_DELTA={settledDelta:0.000}");
                    Mark("INDOOR_CAMERA");
                    if (indoorFound)
                    {
                        Check(gameplayCamera.Indoors, "INDOOR:CLASSIFICATION_FALSE");
                        Check(indoorDistance <= 2f, $"INDOOR:DISTANCE={indoorDistance:0.00}");
                        Check(!wallCross, "INDOOR:WALL_CROSS=True");
                        Check(settledDelta < 0.03f, $"INDOOR:SETTLED_DELTA={settledDelta:0.000}");
                    }
                    yield return Capture("br-05-indoor-camera.png");

                    player.Motor.Teleport(laneStart + Vector3.up * 0.08f);
                    player.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(laneEnd - laneStart, Vector3.up));
                    target.Motor.Teleport(laneEnd + Vector3.up * 0.08f);
                    target.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(laneStart - laneEnd, Vector3.up));
                    Physics.SyncTransforms();
                    gameplayCamera.SetTarget(player.transform);
                    gameplayCamera.Tick(hipInput);
                }
                else Fail("CAMERA:MISSING_THIRD_PERSON_CAMERA");
                SetMatchEnabled(match, true);

                SetMatchEnabled(match, false);
                var smokeCameraObject = TrackTemporary(new GameObject("Smoke Aim Camera"));
                var smokeCamera = smokeCameraObject.AddComponent<Camera>();
                smokeCamera.enabled = false;
                var smokeMuzzle = playerWeaponVisual != null
                    ? playerWeaponVisual.MuzzlePosition : CharacterPresentationProfile.FallbackMuzzle(player.transform);
                smokeCamera.transform.SetPositionAndRotation(smokeMuzzle,
                    Quaternion.LookRotation(CharacterPresentationProfile.AimPoint(target.transform) - smokeMuzzle));
                var accepted = player.Weapon.TryFire(smokeCamera, true, player.gameObject);
                yield return new WaitForSeconds(0.045f);
                var bullets = FindObjectsByType<BulletVisual>();
                var bulletCount = bullets.Length;
                var maxStreak = bullets.Length > 0 ? bullets.Max(bullet => bullet.VisibleLength) : 0f;
                var fullTracers = FindObjectsByType<LineRenderer>()
                    .Count(line => line.gameObject.name == "Shot Tracer");
                yield return new WaitForSeconds(0.05f);
                Debug.Log($"BR_SMOKE_SHOT=ACCEPTED={accepted};HIT={player.Weapon.LastShotHit};DAMAGE={player.Weapon.LastShotDamage:0};FEEDBACK={DamageFeedbackSystem.Instance?.Events.Count ?? 0};BULLETS={bulletCount};MAX_STREAK={maxStreak:0.00};FULL_TRACERS={fullTracers};AUDIO_SOURCES={FindObjectsByType<AudioSource>().Length};IMPACT_POOL={GameObject.Find("Combat Audio Pool") != null};END={player.Weapon.LastShotEndPoint}");
                Mark("SHOT");
                Check(accepted, "SHOT:NOT_ACCEPTED");
                Check(player.Weapon.LastShotHit, "SHOT:NO_HIT");
                Check(player.Weapon.LastShotDamage > 0f, $"SHOT:DAMAGE={player.Weapon.LastShotDamage:0.00}");
                Check((DamageFeedbackSystem.Instance?.Events.Count ?? 0) > 0, "SHOT:NO_DAMAGE_FEEDBACK");
                Check(fullTracers == 0, $"SHOT:FULL_TRACERS={fullTracers}");
                Check(bulletCount > 0 && maxStreak > 0f && maxStreak <= 1.1f,
                    $"SHOT:BULLET_VISUALS={bulletCount}:MAX_STREAK={maxStreak:0.00}");
                yield return Capture("br-05-damage.png");

                var shotgun = match.WeaponCatalog.FirstOrDefault(candidate => candidate.weaponClass == WeaponClass.Shotgun);
                if (shotgun != null)
                {
                    yield return new WaitForSeconds(weapon.fireInterval + 0.08f);
                    target.Health.InitializeFull();
                    target.Health.EquipVest(0);
                    target.Health.EquipHelmet(0);
                    player.Weapon.EquipInitial(shotgun);
                    player.Inventory.AddAmmo(shotgun.ammoKind, shotgun.magazineSize * 2);
                    var softAdsInput = new BRInputFrame(Vector2.zero, Vector2.zero, false, false,
                        false, true, false, false, false, false, false, false, -1, false, false);
                    for (var softFrame = 0; softFrame < 12; softFrame++)
                    {
                        gameplayCamera.Tick(softAdsInput);
                        yield return null;
                    }
                    var softDistance = Vector3.Distance(gameplayCamera.transform.position,
                        player.transform.position + Vector3.up * CharacterPresentationProfile.CameraPivotHeight);
                    var softScopeActive = Camera.main.GetComponent<ScopeController>()?.ScopeActive == true;
                    var softVisibility = player.GetComponent<LocalAdsVisibility>();
                    Debug.Log($"BR_SMOKE_SOFT_ADS=PRESENTATION={gameplayCamera.PresentationMode};DISTANCE={softDistance:0.000};PLAYER_HIDDEN={softVisibility?.Hidden};SCOPE_ACTIVE={softScopeActive};FOV={Camera.main.fieldOfView:0.0}");
                    Mark("SOFT_ADS");
                    Check(gameplayCamera.SoftShoulderAds && softDistance > 1f && softDistance < 2f,
                        $"SOFT_ADS:PRESENTATION={gameplayCamera.PresentationMode}:DISTANCE={softDistance:0.000}");
                    Check(softVisibility != null && !softVisibility.Hidden && !softScopeActive,
                        $"SOFT_ADS:PLAYER_HIDDEN={softVisibility?.Hidden}:SCOPE={softScopeActive}");
                    var shotgunAccepted = player.Weapon.TryFire(smokeCamera, true, player.gameObject);
                    yield return new WaitForSeconds(0.045f);
                    var activePellets = FindObjectsByType<BulletVisual>().Length;
                    Debug.Log($"BR_SMOKE_SHOTGUN=ACCEPTED={shotgunAccepted};HIT={player.Weapon.LastShotHit};DAMAGE={player.Weapon.LastShotDamage:0};PELLETS={activePellets};BULLETS_CREATED={BulletVisual.TotalCreated};CASINGS_CREATED={ShellCasingVisual.TotalCreated};EFFECTS_CREATED={CombatEffectPool.TotalCreated}");
                    Mark("SHOTGUN");
                    Check(shotgunAccepted && player.Weapon.LastShotHit && player.Weapon.LastShotDamage > 0f,
                        $"SHOTGUN:ACCEPTED={shotgunAccepted}:HIT={player.Weapon.LastShotHit}:DAMAGE={player.Weapon.LastShotDamage:0.00}");
                    Check(activePellets > 0, "SHOTGUN:NO_PELLET_VISUALS");
                    player.Weapon.EquipInitial(weapon);
                    player.Inventory.AddAmmo(weapon.ammoKind, weapon.magazineSize * 2);
                    yield return new WaitForSeconds(shotgun.fireInterval + 0.04f);
                }

                target.Health.InitializeFull();
                target.Health.EquipVest(2);
                target.Health.EquipHelmet(1);
                if (!target.Health.IsDead && target.Health.Health > 1f)
                {
                    var rawToOneHealth = (target.Health.Health - 1f) / HealthArmorSystem.VestMultiplier(target.Health.VestLevel);
                    target.Health.ApplyDamage(rawToOneHealth, false, player.gameObject);
                }

                while (!target.Health.IsDead && player.Weapon.Magazine > 0)
                {
                    yield return new WaitForSeconds(weapon.fireInterval + 0.02f);
                    player.Weapon.TryFire(smokeCamera, true, player.gameObject);
                }
                Destroy(smokeCameraObject);
                yield return new WaitForSeconds(0.1f);
                var container = DeathLootContainer.FindNearest(target.transform.position, 5f);
                var before = container?.Entries.Count ?? 0;
                if (container != null)
                {
                    player.Motor.Teleport(container.transform.position + Vector3.back * 1.2f);
                    match.OpenDeathLoot(container);
                }
                var recordedKiller = target.Health.LastDamageSource != null
                    ? target.Health.LastDamageSource.GetComponent<BRParticipant>() : null;
                Debug.Log($"BR_SMOKE_DEATH_LOOT=KILLED={target.Health.IsDead};CONTAINER={container != null};ENTRIES={before};KILLS={player.Eliminations};SOURCE={target.Health.LastDamageSource?.name};KILLER={recordedKiller?.DisplayName}");
                Mark("DEATH_LOOT");
                Check(target.Health.IsDead && container != null && before > 0 && player.Eliminations > 0,
                    $"DEATH_LOOT:KILLED={target.Health.IsDead}:CONTAINER={container != null}:ENTRIES={before}:KILLS={player.Eliminations}");
                Check(container != null && HasPbrTextures(container.transform.Find("Imported Futuristic Death Crate")?.gameObject),
                    "DEATH_LOOT:IMPORTED_TEXTURED_CRATE_MISSING");
                yield return Capture("br-06-death-loot.png");
                var took = false;
                if (container != null)
                {
                    var entryIds = container.Entries.Select(entry => entry.EntryId).ToArray();
                    foreach (var entryId in entryIds)
                    {
                        if (!match.TakeDeathLootEntryById(entryId)) continue;
                        took = true;
                        break;
                    }
                }
                Debug.Log($"BR_SMOKE_LOOT_SELECT=TAKEN={took};BEFORE={before};AFTER={container?.Entries.Count ?? 0}");
                Mark("LOOT_SELECT");
                Check(took && container != null && container.Entries.Count <= before,
                    $"LOOT_SELECT:TAKEN={took}:BEFORE={before}:AFTER={container?.Entries.Count ?? 0}");
            }
            else Fail($"COMBAT_SETUP:PLAYER={player != null}:TARGET={target != null}:WEAPON={player?.Inventory.ActiveWeapon != null}");
            SetMatchEnabled(match, true);
            yield return null;
            var animator = player != null ? player.GetComponent<CharacterVisualAnimator>() : null;
            var weaponVisual = player != null ? player.GetComponent<ParticipantWeaponVisual>() : null;
            var botStates = match?.Participants.Where(candidate => candidate != null && !candidate.IsPlayer && !candidate.Health.IsDead)
                .Select(candidate => candidate.GetComponent<BotController>()?.State.ToString()).Where(state => state != null).Distinct();
            var armedBots = match?.Participants.Count(candidate => candidate != null && !candidate.IsPlayer
                && !candidate.Health.IsDead && candidate.Inventory.ActiveWeapon != null) ?? 0;
            var allocatedMb = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
            Debug.Log($"BR_SMOKE_ACTIVE=STATE={match?.State};ALIVE={match?.AliveCount};REGISTRY={ParticipantRegistry.Count};HEALTH={player?.Health.Health:0};LOOT={FindObjectsByType<LootPickup>().Length};WEAPON={player?.Inventory.ActiveWeapon?.displayName};MAG={player?.Weapon.Magazine};PROFILE={animator?.ProfileId};ANIM={animator?.Ready}:{animator?.CurrentState}:{animator?.ActiveClips};MOUNT={weaponVisual?.MountName};ARMED_BOTS={armedBots};BOT_STATES={string.Join(",", botStates ?? Enumerable.Empty<string>())};MESH_COLLIDERS={FindObjectsByType<MeshCollider>().Length};MEMORY_MB={allocatedMb:0.0}");
            Mark("ACTIVE");
            Check(match.State == MatchState.Active && player != null && !player.Health.IsDead,
                $"ACTIVE:STATE={match.State}:PLAYER_ALIVE={player != null && !player.Health.IsDead}");
            Check(match.SafeZone != null && match.SafeZone.CurrentState is SafeZoneState.Waiting or SafeZoneState.Shrinking,
                $"ACTIVE:SAFE_ZONE_STATE={match.SafeZone?.CurrentState}");
            Check(match.SafeZone != null && match.SafeZone.HasNextZone,
                "ACTIVE:SAFE_ZONE_NEXT_MISSING");
            Check(match.SafeZone != null && SafeZoneGenerator.ContainsCircle(match.SafeZone.CurrentCenter,
                    match.SafeZone.CurrentRadius, match.SafeZone.NextCenter, match.SafeZone.NextRadius, 0.01f),
                "ACTIVE:SAFE_ZONE_NOT_CONTAINED");
            Check(match.SafeZone != null && match.SafeZone.GetComponentInChildren<SafeZoneWorldVisual>() != null,
                "ACTIVE:SAFE_ZONE_WORLD_VISUAL_MISSING");
            Check(animator != null && animator.Ready, "ACTIVE:ANIMATOR_NOT_READY");
            Check(animator != null && animator.ProfileId == CharacterVisualCatalog.Player.Id,
                $"ACTIVE:PLAYER_PROFILE={animator?.ProfileId}");
            Check(GameObject.Find($"Equipped {player?.Inventory.ActiveWeapon?.displayName}") != null,
                "ACTIVE:IMPORTED_WEAPON_MODEL_MISSING");
            LogMapVisuals();
            yield return Capture("br-05-active.png");
            yield return Capture("battle-royale-smoke.png");

            yield return new WaitForSeconds(10f);
            var armedAfterLooting = match?.Participants.Count(candidate => candidate != null && !candidate.IsPlayer
                && !candidate.Health.IsDead && candidate.Inventory.ActiveWeapon != null) ?? 0;
            var groundedAfterLooting = match?.Participants.Count(candidate => candidate != null && !candidate.IsPlayer
                && !candidate.Health.IsDead && candidate.Phase == ParticipantPhase.Grounded) ?? 0;
            var remainingLoot = FindObjectsByType<LootPickup>().Length;
            Debug.Log($"BR_SMOKE_BOT_LOOT=GROUNDED={groundedAfterLooting};ARMED={armedAfterLooting};REMAINING_LOOT={remainingLoot}");
            Mark("BOT_LOOT");
            Check(groundedAfterLooting > 0 && (armedAfterLooting > 0 || remainingLoot >= 200),
                $"BOT_LOOT:GROUNDED={groundedAfterLooting}:ARMED={armedAfterLooting}:REMAINING={remainingLoot}");

            match?.SetPaused(true);
            Debug.Log($"BR_SMOKE_PAUSE=PAUSED={match?.Paused};TIME_SCALE={Time.timeScale:0.0};CURSOR={Cursor.lockState}");
            Mark("PAUSE");
            Check(match.Paused && Mathf.Approximately(Time.timeScale, 0f),
                $"PAUSE:PAUSED={match.Paused}:TIME_SCALE={Time.timeScale:0.0}");
            match?.SetPaused(false);

            if (match != null && player != null)
            {
                foreach (var opponent in match.Participants.Where(candidate => candidate != null
                    && candidate != player && !candidate.Health.IsDead).ToArray())
                    opponent.Health.ApplyDamage(1000f, false, player.gameObject);
                yield return null;
                yield return null;
                Debug.Log($"BR_SMOKE_PRE_RESTART=STATE={match.State};ALIVE={match.AliveCount}");
                Mark("PRE_RESTART");
                Check(match.State == MatchState.Complete, $"PRE_RESTART:STATE={match.State}");
            }
            MobileControlLayout.SetVisualState(Vector2.zero, true, true);
            var restartCamera = Camera.main != null ? Camera.main.GetComponent<ThirdPersonCamera>() : null;
            restartCamera?.AddRecoil(1f, 0.5f);
            match?.StartMatch();
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"BR_SMOKE_RESTART=STATE={match?.State};PARTICIPANTS={match?.Participants.Count}");
            Mark("RESTART");
            Check(match.State == MatchState.Staging && match.Participants.Count > 1,
                $"RESTART:STATE={match.State}:PARTICIPANTS={match.Participants.Count}");
            Debug.Log($"BR_SMOKE_RESTART_INPUT=FIRE_HELD={MobileControlLayout.FireHeld};AIM_LOCK={restartCamera?.AimAssistLocked};RECOIL_SETTLED={restartCamera?.RecoilSettled};STATE={match?.State}");
            Mark("RESTART_INPUT");
            Check(!MobileControlLayout.FireHeld, "RESTART_INPUT:FIRE_HELD");
            Check(restartCamera != null && !restartCamera.AimAssistLocked, "RESTART_INPUT:AIM_LOCKED");
            Check(restartCamera != null && restartCamera.RecoilSettled, "RESTART_INPUT:RECOIL_NOT_SETTLED");
            Check(match.State == MatchState.Staging, $"RESTART_INPUT:STATE={match.State}");
            match?.ReturnToLobby();
            yield return new WaitForSeconds(0.2f);
            Debug.Log($"BR_SMOKE_RETURN_LOBBY=STATE={match?.State}");
            Mark("RETURN_LOBBY");
            Check(match.State == MatchState.Lobby, $"RETURN_LOBBY:STATE={match.State}");
        }

        private static IEnumerator Capture(string fileName)
        {
            var path = Path.Combine(Application.persistentDataPath, fileName);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"BR_SMOKE_SCREENSHOT={path}");
            CaptureCameraFrame(fileName);
            yield return new WaitForSeconds(0.35f);
        }

        private static void CaptureCameraFrame(string fileName)
        {
            var camera = Camera.main;
            if (camera == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return;

            var width = Mathf.Max(640, Screen.width);
            var height = Mathf.Max(360, Screen.height);
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply(false, false);
                var path = Path.Combine(Application.persistentDataPath, $"camera-{fileName}");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log($"BR_SMOKE_CAMERA_SCREENSHOT={path}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Destroy(texture);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private IEnumerator RunImportedMapTraversal(BRParticipant player, ThirdPersonCamera gameplayCamera)
        {
            var map = GameObject.Find(RuntimeSmokeAcceptance.ImportedMapName);
            Check(map != null, "IMPORTED_OPENINGS:MAP_MISSING");
            if (map == null) yield break;
            yield return RunBermudaMapTraversal(map, player, gameplayCamera);
            yield break;
#pragma warning disable CS0162
            var mapRoot = map.transform;
            var doorA = new ImportedDoor("MANSION_DOOR_A", new Vector3(4.880f, 3.724f, -26.200f),
                new Vector3(4.330f, 3.724f, -26.200f), new Vector3(5.430f, 3.724f, -26.200f));
            var doorB = new ImportedDoor("MANSION_DOOR_B", new Vector3(9.680f, 3.724f, -26.200f),
                new Vector3(9.130f, 3.724f, -26.200f), new Vector3(10.230f, 3.724f, -26.200f));
            var window = new ImportedWindow("HUGE_HOUSE_WINDOW_A", new Vector3(14.832f, 5.644f, -0.528f),
                new Vector3(14.183f, 4.594f, -1.177f), new Vector3(15.301f, 4.594f, -0.059f));

            var previousBackfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            var doorAValid = MeasureImportedDoor(mapRoot, ref doorA);
            var doorBValid = MeasureImportedDoor(mapRoot, ref doorB);
            var windowValid = MeasureImportedWindow(mapRoot, ref window);
            Physics.queriesHitBackfaces = previousBackfaces;

            Debug.Log($"BR_SMOKE_IMPORTED_DOOR_OPENINGS=COUNT=2;FIXTURE=False;" +
                      $"A_ID={doorA.Id};A_PATH={RuntimeSmokeAcceptance.DoorStructurePath};" +
                      $"A_GROUND_PATH={RuntimeSmokeAcceptance.DoorGroundPath};A_OUTSIDE={FormatVector(doorA.Outside)};" +
                      $"A_INSIDE={FormatVector(doorA.Inside)};A_WIDTH={doorA.Width:0.000};" +
                      $"A_CLEAR_HEIGHT={doorA.ClearHeight:0.000};A_RATIO={doorA.Ratio:0.0};" +
                      $"B_ID={doorB.Id};B_PATH={RuntimeSmokeAcceptance.DoorStructurePath};" +
                      $"B_GROUND_PATH={RuntimeSmokeAcceptance.DoorGroundPath};B_OUTSIDE={FormatVector(doorB.Outside)};" +
                      $"B_INSIDE={FormatVector(doorB.Inside)};B_WIDTH={doorB.Width:0.000};" +
                      $"B_CLEAR_HEIGHT={doorB.ClearHeight:0.000};B_RATIO={doorB.Ratio:0.0}");
            Mark("IMPORTED_DOOR_OPENINGS");
            Check(doorAValid && doorBValid, $"IMPORTED_DOOR_OPENINGS:VALID_A={doorAValid}:VALID_B={doorBValid}");
            Check(doorA.Ratio >= 60f && doorA.Ratio <= 64f && doorB.Ratio >= 60f && doorB.Ratio <= 64f,
                $"IMPORTED_DOOR_OPENINGS:RATIOS={doorA.Ratio:0.0},{doorB.Ratio:0.0}");

            Debug.Log($"BR_SMOKE_IMPORTED_WINDOW_GEOMETRY=ID={window.Id};FIXTURE=False;" +
                      $"FRAME_PATH={RuntimeSmokeAcceptance.WindowFramePath};SIDE_PATH={RuntimeSmokeAcceptance.WindowSidePath};" +
                      $"GROUND_PATH={RuntimeSmokeAcceptance.WindowGroundPath};OUTSIDE={FormatVector(window.Outside)};" +
                      $"INSIDE={FormatVector(window.Inside)};WIDTH={window.Width:0.000};SILL={window.Sill:0.000};" +
                      $"TOP={window.Top:0.000};CLEAR_HEIGHT={window.ClearHeight:0.000};VALID={windowValid}");
            Check(windowValid, "IMPORTED_WINDOW:GEOMETRY_INVALID");

            var directionObject = TrackTemporary(new GameObject("BR Smoke Imported Opening Direction"));
            var config = new BRGameConfig();
            config.Normalize();
            var walkFrame = new BRInputFrame(Vector2.up, Vector2.zero, false, false, false, false,
                false, false, false, false, false, false, -1, false, false);
            var allDoorsPassed = true;
            foreach (var door in new[] { doorA, doorB })
            {
                var forward = (door.Inside - door.Outside).normalized;
                directionObject.transform.SetPositionAndRotation(door.Outside, Quaternion.LookRotation(forward));
                player.Motor.Teleport(door.Outside + Vector3.up * 0.05f);
                player.transform.rotation = Quaternion.LookRotation(forward);
                gameplayCamera.SetTarget(player.transform);
                Physics.SyncTransforms();
                for (var settle = 0; settle < 8; settle++)
                {
                    player.Motor.Move(default, directionObject.transform, config, true);
                    gameplayCamera.Tick(default);
                    yield return null;
                }
                var start = player.transform.position;
                var cameraCoherent = true;
                for (var frame = 0; frame < 150
                    && Vector3.Dot(player.transform.position - start, forward) < 1.0f; frame++)
                {
                    player.Motor.Move(walkFrame, directionObject.transform, config, true);
                    gameplayCamera.Tick(walkFrame);
                    cameraCoherent &= CameraCoherent(player, gameplayCamera);
                    yield return null;
                }
                var progress = Vector3.Dot(player.transform.position - start, forward);
                var snag = HasParticipantGeometryOverlap(player);
                var wallCross = HasBlockingGeometryBetween(
                    player.transform.position + Vector3.up * CharacterPresentationProfile.CameraPivotHeight,
                    gameplayCamera.transform.position, player);
                var passed = progress >= 0.95f && !snag && cameraCoherent && !wallCross;
                allDoorsPassed &= passed;
                Debug.Log($"BR_SMOKE_IMPORTED_DOOR_WALK=ID={door.Id};PASSED={passed};PROGRESS={progress:0.000};" +
                          $"SNAG={snag};CAMERA_COHERENT={cameraCoherent};CAMERA_WALL_CROSS={wallCross};FIXTURE=False");
            }
            Mark("IMPORTED_DOOR_WALK");
            Check(allDoorsPassed, "IMPORTED_DOOR_WALK:ONE_OR_MORE_FAILED");

            var windowForward = (window.Inside - window.Outside).normalized;
            directionObject.transform.SetPositionAndRotation(window.Outside, Quaternion.LookRotation(windowForward));
            player.Motor.Teleport(window.Outside);
            player.transform.rotation = Quaternion.LookRotation(windowForward);
            gameplayCamera.SetTarget(player.transform);
            Physics.SyncTransforms();
            var vaultFrame = new BRInputFrame(Vector2.up, Vector2.zero, true, false, false, false,
                false, false, false, false, false, false, -1, false, false);
            var windowStart = player.transform.position;
            var windowStartY = windowStart.y;
            var windowPeakY = windowStartY;
            gameplayCamera.Tick(default);
            var previousCameraPosition = gameplayCamera.transform.position;
            var minCameraDistance = float.PositiveInfinity;
            var maxCameraDistance = 0f;
            var maxCameraStep = 0f;
            var lowFound = TryImportedRay(mapRoot,
                windowStart + Vector3.up * BRCharacterMotor.LowVaultProbeHeight, windowForward,
                BRCharacterMotor.VaultForwardProbeDistance, out var runtimeLowHit);
            var highFound = TryImportedRay(mapRoot,
                windowStart + Vector3.up * BRCharacterMotor.HighVaultProbeHeight, windowForward,
                BRCharacterMotor.VaultForwardProbeDistance, out var runtimeHighHit);
            Debug.Log($"BR_SMOKE_IMPORTED_WINDOW_PROBES=START={FormatVector(windowStart)};" +
                      $"LOW_FOUND={lowFound};LOW_PATH={(lowFound ? HierarchyPath(runtimeLowHit.collider.transform) : "NONE")};" +
                      $"LOW_DISTANCE={(lowFound ? runtimeLowHit.distance : -1f):0.000};HIGH_FOUND={highFound};" +
                      $"HIGH_PATH={(highFound ? HierarchyPath(runtimeHighHit.collider.transform) : "NONE")};FIXTURE=False");
            player.Motor.Move(vaultFrame, directionObject.transform, config, true);
            var startedVault = player.Motor.Vaulting;
            var compactVault = player.Motor.CompactVaulting;
            var cameraCoherentDuringVault = true;
            for (var frame = 0; frame < 120 && player.Motor.Vaulting; frame++)
            {
                player.Motor.Move(walkFrame, directionObject.transform, config, true);
                gameplayCamera.Tick(walkFrame);
                cameraCoherentDuringVault &= CameraCoherent(player, gameplayCamera);
                var cameraDistance = Vector3.Distance(gameplayCamera.transform.position,
                    player.transform.position + Vector3.up * CharacterPresentationProfile.CameraPivotHeight);
                minCameraDistance = Mathf.Min(minCameraDistance, cameraDistance);
                maxCameraDistance = Mathf.Max(maxCameraDistance, cameraDistance);
                maxCameraStep = Mathf.Max(maxCameraStep,
                    Vector3.Distance(previousCameraPosition, gameplayCamera.transform.position));
                previousCameraPosition = gameplayCamera.transform.position;
                windowPeakY = Mathf.Max(windowPeakY, player.transform.position.y);
                yield return null;
            }
            var windowProgress = Vector3.Dot(player.transform.position - windowStart, windowForward);
            var windowSnag = HasParticipantGeometryOverlap(player);
            var windowWallCross = HasBlockingGeometryBetween(
                player.transform.position + Vector3.up * CharacterPresentationProfile.CameraPivotHeight,
                gameplayCamera.transform.position, player);
            var windowPassed = startedVault && compactVault && !player.Motor.Vaulting && windowProgress > 1.3f
                && !windowSnag && cameraCoherentDuringVault && !windowWallCross && maxCameraStep < 1.5f;
            Debug.Log($"BR_SMOKE_IMPORTED_WINDOW_VAULT=ID={window.Id};MODE=VAULT;COMPACT={compactVault};" +
                      $"STARTED={startedVault};COMPLETED={!player.Motor.Vaulting};PEAK_DELTA={windowPeakY - windowStartY:0.000};" +
                      $"PROGRESS={windowProgress:0.000};SNAG={windowSnag};CAMERA_COHERENT={cameraCoherentDuringVault};" +
                      $"CAMERA_MIN_DISTANCE={minCameraDistance:0.000};CAMERA_MAX_DISTANCE={maxCameraDistance:0.000};" +
                      $"CAMERA_MAX_STEP={maxCameraStep:0.000};CAMERA_WALL_CROSS={windowWallCross};" +
                      $"WIDTH={window.Width:0.000};SILL={window.Sill:0.000};TOP={window.Top:0.000};" +
                      $"CLEAR_HEIGHT={window.ClearHeight:0.000};FIXTURE=False");
            Mark("IMPORTED_WINDOW_VAULT");
            Check(windowPassed,
                $"IMPORTED_WINDOW_VAULT:STARTED={startedVault}:COMPACT={compactVault}:ACTIVE={player.Motor.Vaulting}:" +
                $"PROGRESS={windowProgress:0.000}:SNAG={windowSnag}:CAMERA={cameraCoherentDuringVault}:" +
                $"CAMERA_STEP={maxCameraStep:0.000}:WALL_CROSS={windowWallCross}");
            Destroy(directionObject);
            yield return null;
#pragma warning restore CS0162
        }

        private IEnumerator RunBermudaMapTraversal(GameObject map, BRParticipant player,
            ThirdPersonCamera gameplayCamera)
        {
            var renderers = map.GetComponentsInChildren<Renderer>(true);
            var colliders = map.GetComponentsInChildren<Collider>(true);
            var waterColliders = colliders.Count(collider =>
                WorldMapGeometry.IsWater(WorldMapGeometry.HierarchyName(collider.transform)));
            var visibleWaterMeshes = renderers.Count(renderer => renderer.enabled
                && WorldMapGeometry.IsWater(WorldMapGeometry.HierarchyName(renderer.transform)));
            var collidableRoles = colliders.Count(collider =>
                WorldMapGeometry.IsCollidable(WorldMapGeometry.HierarchyName(collider.transform)));
            var boundsReady = WorldMapGeometry.TryCalculatePlayableBounds(renderers, out var bounds);
            Debug.Log($"BR_SMOKE_BERMUDA_GEOMETRY=RENDERERS={renderers.Length};COLLIDERS={colliders.Length};" +
                      $"COLLIDABLE_ROLES={collidableRoles};WATER_COLLIDERS={waterColliders};" +
                      $"VISIBLE_REDUNDANT_WATER={visibleWaterMeshes};" +
                      $"BOUNDS={bounds.size};CENTER={bounds.center}");
            Mark("BERMUDA_GEOMETRY");
            Check(boundsReady && Mathf.Max(bounds.size.x, bounds.size.z) >= 1900f,
                $"BERMUDA_GEOMETRY:BOUNDS={bounds.size}");
            Check(waterColliders == 0, $"BERMUDA_GEOMETRY:WATER_COLLIDERS={waterColliders}");
            Check(visibleWaterMeshes == 0,
                $"BERMUDA_GEOMETRY:VISIBLE_REDUNDANT_WATER={visibleWaterMeshes}");
            Check(collidableRoles >= 5, $"BERMUDA_GEOMETRY:COLLIDERS={collidableRoles}");

            var samples = new[]
            {
                new Vector2(0.50f, 0.50f), new Vector2(0.30f, 0.42f),
                new Vector2(0.70f, 0.58f), new Vector2(0.42f, 0.70f)
            };
            var grounded = 0;
            Vector3 traversalStart = default;
            foreach (var sample in samples)
            {
                var candidate = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, sample.x),
                    bounds.max.y + 2f, Mathf.Lerp(bounds.min.z, bounds.max.z, sample.y));
                if (!PlayableArea.TryGround(candidate, out var point, 0.1f)) continue;
                traversalStart = point;
                grounded++;
            }
            Debug.Log($"BR_SMOKE_BERMUDA_TRAVERSAL=GROUNDED_SAMPLES={grounded}/{samples.Length};" +
                      $"START={traversalStart}");
            Mark("BERMUDA_TRAVERSAL");
            Check(grounded >= 2, $"BERMUDA_TRAVERSAL:GROUNDED={grounded}");

            if (grounded > 0)
            {
                var directionObject = TrackTemporary(new GameObject("BR Smoke Bermuda Walk Direction"));
                var walkConfig = new BRGameConfig();
                walkConfig.Normalize();
                var walkFrame = new BRInputFrame(Vector2.up, Vector2.zero, false, false, false, false,
                    false, false, false, false, false, false, -1, false, false);
                var capturedWalks = 0;
                for (var index = 0; index < samples.Length; index++)
                {
                    var sample = samples[index];
                    var candidate = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, sample.x),
                        bounds.max.y + 2f, Mathf.Lerp(bounds.min.z, bounds.max.z, sample.y));
                    if (!PlayableArea.TryGround(candidate, out var point, 0.1f)) continue;

                    var towardCenter = Vector3.ProjectOnPlane(bounds.center - point, Vector3.up);
                    if (towardCenter.sqrMagnitude < 0.01f) towardCenter = Vector3.forward;
                    towardCenter.Normalize();
                    directionObject.transform.SetPositionAndRotation(point, Quaternion.LookRotation(towardCenter));
                    player.Motor.Teleport(point + Vector3.up * 0.08f);
                    player.transform.rotation = Quaternion.LookRotation(towardCenter);
                    gameplayCamera.SetTarget(player.transform);
                    Physics.SyncTransforms();
                    for (var settle = 0; settle < 12; settle++)
                    {
                        gameplayCamera.Tick(default);
                        yield return null;
                    }

                    var start = player.transform.position;
                    for (var frame = 0; frame < 75; frame++)
                    {
                        player.Motor.Move(walkFrame, directionObject.transform, walkConfig, true);
                        gameplayCamera.Tick(walkFrame);
                        yield return null;
                    }
                    capturedWalks++;
                    Debug.Log($"BR_SMOKE_BERMUDA_WALK=INDEX={index + 1};START={start};" +
                              $"END={player.transform.position};DISTANCE={Vector3.Distance(start, player.transform.position):0.00}");
                    yield return Capture($"br-map-walk-{index + 1:00}.png");
                }
                Check(capturedWalks >= 3, $"BERMUDA_WALK_CAPTURES:COUNT={capturedWalks}");
            }
            var cameraCoherent = grounded > 0 && CameraCoherent(player, gameplayCamera);
            Debug.Log($"BR_SMOKE_BERMUDA_CAMERA=COHERENT={cameraCoherent};" +
                      $"PLAYER={player.transform.position};CAMERA={gameplayCamera.transform.position}");
            Mark("BERMUDA_CAMERA");
            Check(cameraCoherent, "BERMUDA_CAMERA:INCOHERENT");
        }

        private static bool MeasureImportedDoor(Transform mapRoot, ref ImportedDoor door)
        {
            var structure = FindImportedCollider(mapRoot, RuntimeSmokeAcceptance.DoorStructurePath);
            var expectedGround = FindImportedCollider(mapRoot, RuntimeSmokeAcceptance.DoorGroundPath);
            if (structure == null || expectedGround == null) return false;
            var forward = (door.Inside - door.Outside).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            if (!TryImportedGround(mapRoot, door.Outside, out var outsideGround)
                || !TryImportedGround(mapRoot, door.Inside, out var insideGround)
                || outsideGround.collider != expectedGround || insideGround.collider != expectedGround
                || Mathf.Abs(outsideGround.point.y - door.Outside.y) > 0.08f
                || Mathf.Abs(insideGround.point.y - door.Inside.y) > 0.08f) return false;
            var midpoint = new Vector3(door.Midpoint.x,
                (outsideGround.point.y + insideGround.point.y) * 0.5f, door.Midpoint.z);
            if (!TryImportedRay(mapRoot, midpoint + Vector3.up * 0.9f, right, 1.15f, out var rightHit)
                || !TryImportedRay(mapRoot, midpoint + Vector3.up * 0.9f, -right, 1.15f, out var leftHit)
                || !TryImportedRay(mapRoot, midpoint + Vector3.up * 0.25f, Vector3.up, 2.7f, out var topHit)
                || rightHit.collider != structure || leftHit.collider != structure || topHit.collider != structure)
                return false;
            door.Width = rightHit.distance + leftHit.distance;
            door.ClearHeight = topHit.point.y - midpoint.y;
            door.Ratio = CharacterPresentationProfile.OpeningOccupancy(door.ClearHeight) * 100f;
            return door.Width >= 0.76f && door.Width <= 0.88f
                && door.ClearHeight >= 1.31f && door.ClearHeight <= 1.39f
                && ImportedStandingClear(mapRoot, door.Outside)
                && ImportedStandingClear(mapRoot, door.Inside)
                && ImportedCapsulePathClear(mapRoot, door.Outside, door.Inside);
        }

        private static bool MeasureImportedWindow(Transform mapRoot, ref ImportedWindow window)
        {
            var frame = FindImportedCollider(mapRoot, RuntimeSmokeAcceptance.WindowFramePath);
            var sides = FindImportedCollider(mapRoot, RuntimeSmokeAcceptance.WindowSidePath);
            var expectedGround = FindImportedCollider(mapRoot, RuntimeSmokeAcceptance.WindowGroundPath);
            if (frame == null || sides == null || expectedGround == null) return false;
            var forward = (window.Inside - window.Outside).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            if (!TryImportedGround(mapRoot, window.Outside, out var outsideGround)
                || !TryImportedGround(mapRoot, window.Inside, out var insideGround)
                || outsideGround.collider != expectedGround || insideGround.collider != expectedGround
                || Mathf.Abs(outsideGround.point.y - window.Outside.y) > 0.08f
                || Mathf.Abs(insideGround.point.y - window.Inside.y) > 0.08f) return false;
            if (!TryImportedRay(mapRoot, window.Outside + Vector3.up * BRCharacterMotor.LowVaultProbeHeight,
                    forward, BRCharacterMotor.VaultForwardProbeDistance, out var obstacle)
                || obstacle.collider != frame
                || TryImportedRay(mapRoot, window.Outside + Vector3.up * BRCharacterMotor.HighVaultProbeHeight,
                    forward, BRCharacterMotor.VaultForwardProbeDistance, out _)) return false;
            if (!TryImportedRay(mapRoot, window.Aperture, Vector3.down, 1f, out var sillHit)
                || !TryImportedRay(mapRoot, window.Aperture, Vector3.up, 1.3f, out var topHit)
                || !TryImportedRay(mapRoot, window.Aperture, right, 3.5f, out var rightHit)
                || !TryImportedRay(mapRoot, window.Aperture, -right, 3.5f, out var leftHit)
                || sillHit.collider != frame || topHit.collider != frame
                || rightHit.collider != sides || leftHit.collider != sides) return false;
            window.Width = rightHit.distance + leftHit.distance;
            window.Sill = sillHit.point.y - outsideGround.point.y;
            window.Top = topHit.point.y - outsideGround.point.y;
            window.ClearHeight = window.Top - window.Sill;
            return window.Width >= 2.65f && window.Width <= 3.05f
                && window.Sill >= 0.18f && window.Sill <= 0.32f
                && window.Top >= 1.38f && window.Top <= 1.52f
                && window.ClearHeight >= 1.12f && window.ClearHeight <= 1.30f
                && ImportedStandingClear(mapRoot, window.Outside)
                && ImportedStandingClear(mapRoot, window.Inside);
        }

        private static Collider FindImportedCollider(Transform mapRoot, string fullPath)
        {
            if (mapRoot == null || !fullPath.StartsWith(mapRoot.name + "/", StringComparison.Ordinal)) return null;
            var relativePath = fullPath.Substring(mapRoot.name.Length + 1);
            var child = mapRoot.Find(relativePath);
            var collider = child != null ? child.GetComponent<Collider>() : null;
            return collider != null && collider.transform.IsChildOf(mapRoot) ? collider : null;
        }

        private static bool TryImportedRay(Transform mapRoot, Vector3 origin, Vector3 direction, float distance,
            out RaycastHit mapHit)
        {
            foreach (var hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore)
                         .OrderBy(hit => hit.distance))
            {
                if (hit.collider == null || !hit.collider.transform.IsChildOf(mapRoot)) continue;
                mapHit = hit;
                return true;
            }
            mapHit = default;
            return false;
        }

        private static bool TryImportedGround(Transform mapRoot, Vector3 expected, out RaycastHit groundHit)
        {
            foreach (var hit in Physics.RaycastAll(expected + Vector3.up * 3f, Vector3.down, 4f, ~0,
                         QueryTriggerInteraction.Ignore).OrderBy(hit => Mathf.Abs(hit.point.y - expected.y)))
            {
                if (hit.collider == null || !hit.collider.transform.IsChildOf(mapRoot) || hit.normal.y < 0.45f
                    || Mathf.Abs(hit.point.y - expected.y) > 0.12f) continue;
                groundHit = hit;
                return true;
            }
            groundHit = default;
            return false;
        }

        private static bool ImportedStandingClear(Transform mapRoot, Vector3 feet)
        {
            var radius = CharacterPresentationProfile.ControllerRadius * 0.82f;
            var bottom = feet + Vector3.up * (CharacterPresentationProfile.ControllerRadius + 0.035f);
            var top = feet + Vector3.up * (CharacterPresentationProfile.ControllerHeight
                - CharacterPresentationProfile.ControllerRadius);
            return Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore)
                .All(collider => collider == null || !collider.transform.IsChildOf(mapRoot));
        }

        private static bool ImportedCapsulePathClear(Transform mapRoot, Vector3 from, Vector3 to)
        {
            var radius = CharacterPresentationProfile.ControllerRadius * 0.82f;
            var bottom = from + Vector3.up * (CharacterPresentationProfile.ControllerRadius + 0.035f);
            var top = from + Vector3.up * (CharacterPresentationProfile.ControllerHeight
                - CharacterPresentationProfile.ControllerRadius);
            var delta = to - from;
            return Physics.CapsuleCastAll(bottom, top, radius, delta.normalized, delta.magnitude, ~0,
                    QueryTriggerInteraction.Ignore)
                .All(hit => hit.collider == null || !hit.collider.transform.IsChildOf(mapRoot));
        }

        private static bool CameraCoherent(BRParticipant player, ThirdPersonCamera camera)
        {
            if (player == null || camera == null) return false;
            var position = camera.transform.position;
            var pivot = player.transform.position + Vector3.up * CharacterPresentationProfile.CameraPivotHeight;
            var distance = Vector3.Distance(position, pivot);
            return float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z)
                && float.IsFinite(distance) && distance >= 0.05f && distance <= 4.2f;
        }

        private static string FormatVector(Vector3 value) => FormattableString.Invariant(
            $"({value.x:0.000},{value.y:0.000},{value.z:0.000})");

        private (GameObject Header, List<GameObject> Parts) CreateDoorFixture(Vector3 center,
            Quaternion rotation, float openingHeight, string label)
        {
            const float openingWidth = 1.4f;
            const float sideWidth = 1.25f;
            const float wallHeight = 2.45f;
            const float thickness = 0.18f;
            var right = rotation * Vector3.right;
            var parts = new List<GameObject>();
            parts.Add(CreateFixtureCube($"BR Smoke {label} Left", center - right * (openingWidth + sideWidth) * 0.5f
                + Vector3.up * wallHeight * 0.5f, rotation, new Vector3(sideWidth, wallHeight, thickness)));
            parts.Add(CreateFixtureCube($"BR Smoke {label} Right", center + right * (openingWidth + sideWidth) * 0.5f
                + Vector3.up * wallHeight * 0.5f, rotation, new Vector3(sideWidth, wallHeight, thickness)));
            var headerHeight = wallHeight - openingHeight;
            var header = CreateFixtureCube($"BR Smoke {label} Header",
                center + Vector3.up * (openingHeight + headerHeight * 0.5f), rotation,
                new Vector3(openingWidth, headerHeight, thickness));
            parts.Add(header);
            return (header, parts);
        }

        private (float ClearHeight, List<GameObject> Parts) CreateWindowFixture(Vector3 center,
            Quaternion rotation)
        {
            const float openingWidth = 0.9f;
            const float sideWidth = 1.2f;
            const float wallHeight = 3.0f;
            const float sillHeight = 0.6f;
            const float openingTop = 2.65f;
            const float thickness = 0.18f;
            var right = rotation * Vector3.right;
            var parts = new List<GameObject>
            {
                CreateFixtureCube("BR Smoke Window Left", center - right * (openingWidth + sideWidth) * 0.5f
                    + Vector3.up * wallHeight * 0.5f, rotation, new Vector3(sideWidth, wallHeight, thickness)),
                CreateFixtureCube("BR Smoke Window Right", center + right * (openingWidth + sideWidth) * 0.5f
                    + Vector3.up * wallHeight * 0.5f, rotation, new Vector3(sideWidth, wallHeight, thickness)),
                CreateFixtureCube("BR Smoke Window Sill", center + Vector3.up * sillHeight * 0.5f,
                    rotation, new Vector3(openingWidth, sillHeight, thickness)),
                CreateFixtureCube("BR Smoke Window Header",
                    center + Vector3.up * (openingTop + (wallHeight - openingTop) * 0.5f), rotation,
                    new Vector3(openingWidth, wallHeight - openingTop, thickness))
            };
            return (openingTop - sillHeight, parts);
        }

        private GameObject CreateFixtureCube(string name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var fixture = TrackTemporary(GameObject.CreatePrimitive(PrimitiveType.Cube));
            fixture.name = name;
            fixture.transform.SetPositionAndRotation(position, rotation);
            fixture.transform.localScale = scale;
            return fixture;
        }

        private static bool HasParticipantGeometryOverlap(BRParticipant participant)
        {
            var controller = participant != null ? participant.GetComponent<CharacterController>() : null;
            if (controller == null) return true;
            var radius = Mathf.Max(0.01f, controller.radius - 0.015f);
            var center = participant.transform.TransformPoint(controller.center);
            var segment = Mathf.Max(0f, controller.height * 0.5f - radius - 0.015f);
            var overlaps = Physics.OverlapCapsule(center - Vector3.up * segment, center + Vector3.up * segment,
                radius, ~0, QueryTriggerInteraction.Ignore);
            return overlaps.Any(collider => collider != null && collider != controller
                && !collider.transform.IsChildOf(participant.transform));
        }

        private static void DestroyFixture(IEnumerable<GameObject> parts)
        {
            foreach (var part in parts)
                if (part != null) Destroy(part);
        }

        private void LogMapVisuals()
        {
            var map = GameObject.Find(RuntimeSmokeAcceptance.ImportedMapName);
            var renderers = map != null ? map.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
            var materialCount = 0;
            var texturedMaterials = 0;
            var gltfMaterials = 0;
            var textures = new System.Collections.Generic.HashSet<Texture2D>();
            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                materialCount++;
                if (material.shader != null && material.shader.name.Contains("glTF")) gltfMaterials++;
                foreach (var property in material.GetTexturePropertyNames())
                {
                    if (material.GetTexture(property) == null) continue;
                    texturedMaterials++;
                    if (material.GetTexture(property) is Texture2D texture) textures.Add(texture);
                    break;
                }
            }
            var hud = FindAnyObjectByType<MatchHUD>();
            var minTexture = textures.Count > 0 ? textures.Min(texture => Mathf.Min(texture.width, texture.height)) : 0;
            var maxTexture = textures.Count > 0 ? textures.Max(texture => Mathf.Max(texture.width, texture.height)) : 0;
            var mipmapped = textures.Count(texture => texture.mipmapCount > 1);
            var trilinear = textures.Count(texture => texture.filterMode == FilterMode.Trilinear);
            var anisotropic = textures.Count(texture => texture.anisoLevel >= 4);
            Debug.Log($"BR_SMOKE_MAP=PRESENT={map != null};RENDERERS={renderers.Length};MATERIALS={materialCount};TEXTURED={texturedMaterials};GLTF={gltfMaterials};UNIQUE_TEXTURES={textures.Count};TEXTURE_RANGE={minTexture}-{maxTexture};MIPMAPPED={mipmapped};TRILINEAR={trilinear};ANISO4={anisotropic};MINIMAP={hud != null && hud.MinimapReady}");
            Mark("MAP");
            Check(map != null && renderers.Length > 0, $"MAP:PRESENT={map != null}:RENDERERS={renderers.Length}");
            Check(materialCount > 0 && texturedMaterials > 0 && textures.Count > 0,
                $"MAP:MATERIALS={materialCount}:TEXTURED={texturedMaterials}:TEXTURES={textures.Count}");
            Check(hud != null && hud.MinimapReady, "MAP:MINIMAP_NOT_READY");
            if (hud != null && hud.MinimapTexture != null)
            {
                var path = Path.Combine(Application.persistentDataPath, "br-minimap.png");
                File.WriteAllBytes(path, hud.MinimapTexture.EncodeToPNG());
                Debug.Log($"BR_SMOKE_MINIMAP_FILE={path};BYTES={new FileInfo(path).Length}");
            }
        }

        private static float FindGroundHeight(Vector3 position)
        {
            var hits = Physics.RaycastAll(position + Vector3.up * 30f, Vector3.down, 48f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.normal.y < 0.65f) continue;
                var objectName = hit.collider.gameObject.name;
                if (objectName.Contains("Water") || objectName.Contains("Technical Ground") || hit.point.y > 6.5f) continue;
                return hit.point.y;
            }
            return 0f;
        }

        private void LogPresentation(BRParticipant player, ThirdPersonCamera gameplayCamera)
        {
            var renderers = player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
            var visualBounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(player.transform.position, Vector3.zero);
            for (var index = 1; index < renderers.Length; index++) visualBounds.Encapsulate(renderers[index].bounds);
            var controller = player.GetComponent<CharacterController>();
            var pivot = player.transform.position + Vector3.up * CharacterPresentationProfile.CameraPivotHeight;
            var cameraDistance = Vector3.Distance(gameplayCamera.transform.position, pivot);
            var cameraAboveHead = gameplayCamera.transform.position.y
                - (player.transform.position.y + CharacterPresentationProfile.HeadHeight);
            var reticleOccluded = RayIntersectsRenderers(Camera.main, renderers);
            Debug.Log($"BR_SMOKE_PRESENTATION=VISUAL_HEIGHT={visualBounds.size.y:0.00};CONTROLLER_HEIGHT={controller?.height:0.00};HEAD={CharacterPresentationProfile.HeadHeight:0.00};CAMERA_DISTANCE={cameraDistance:0.00};CAMERA_ABOVE_HEAD={cameraAboveHead:0.00};RETICLE_OCCLUDED={reticleOccluded}");
            Mark("PRESENTATION");
            Check(visualBounds.size.y >= 0.78f && visualBounds.size.y <= 0.90f,
                $"PRESENTATION:VISUAL_HEIGHT={visualBounds.size.y:0.000}");
            Check(controller != null && Mathf.Abs(controller.height - CharacterPresentationProfile.ControllerHeight) <= 0.005f,
                $"PRESENTATION:CONTROLLER_HEIGHT={controller?.height:0.000}");
            Check(Mathf.Abs(CharacterPresentationProfile.HeadHeight - 0.75f) <= 0.005f,
                $"PRESENTATION:HEAD_PROFILE={CharacterPresentationProfile.HeadHeight:0.00}");
            Check(float.IsFinite(cameraDistance) && cameraDistance > 0f,
                $"PRESENTATION:CAMERA_DISTANCE={cameraDistance:0.000}");
            Check(cameraAboveHead >= 0.08f && cameraAboveHead <= 0.25f,
                $"PRESENTATION:CAMERA_ABOVE_HEAD={cameraAboveHead:0.000}");
            Check(!reticleOccluded, "PRESENTATION:RETICLE_OCCLUDED");
        }

        private static bool TryFindPresentationLocation(out Vector3 position, out Vector3 lookAt,
            out string selection)
        {
            var renderers = FindObjectsByType<Renderer>()
                .Where(renderer => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                .Select(renderer => new { Renderer = renderer, Path = HierarchyPath(renderer.transform) })
                .Where(candidate => candidate.Path.ToLowerInvariant().Contains("door")
                    || candidate.Path.ToLowerInvariant().Contains("entrance")
                    || candidate.Path.ToLowerInvariant().Contains("gate"))
                .OrderBy(candidate => candidate.Path).ToArray();
            foreach (var candidate in renderers)
            {
                if (TryFindGroundBeside(candidate.Renderer.bounds, out position))
                {
                    lookAt = candidate.Renderer.bounds.center;
                    selection = candidate.Path;
                    return true;
                }
            }

            var map = GameObject.Find(RuntimeSmokeAcceptance.ImportedMapName);
            var fallbackRenderers = map != null ? map.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled && renderer.bounds.size.y >= 1.5f)
                .OrderBy(renderer => HierarchyPath(renderer.transform)).ToArray() : System.Array.Empty<Renderer>();
            foreach (var renderer in fallbackRenderers)
            {
                if (!TryFindGroundBeside(renderer.bounds, out position)) continue;
                lookAt = renderer.bounds.center;
                selection = $"MAP_BUILDING:{HierarchyPath(renderer.transform)}";
                return true;
            }

            if (!PlayableArea.TryFindOpenGround(PlayableArea.Center, out position))
            {
                lookAt = PlayableArea.Center + Vector3.forward;
                selection = "OPEN_GROUND_MISSING";
                return false;
            }
            lookAt = position + Vector3.forward;
            selection = "OPEN_GROUND_FALLBACK";
            return true;
        }

        private static bool TryFindGroundBeside(Bounds bounds, out Vector3 position)
        {
            var horizontal = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            foreach (var direction in horizontal)
            {
                var extent = Mathf.Abs(direction.x) > 0.5f ? bounds.extents.x : bounds.extents.z;
                var sample = bounds.center + direction * (extent + 1.1f);
                if (!PlayableArea.TryGround(sample, out var candidate, 0f)) continue;
                if (!HasStandingClearance(candidate)) continue;
                position = candidate;
                return true;
            }
            position = default;
            return false;
        }

        private static bool TryFindIndoorLocation(out Vector3 position, out string selection)
        {
            var expectedPosition = new Vector3(-0.63f, -0.60f, 2.85f);
            var sample = expectedPosition + Vector3.up * 12f;
            var hits = Physics.RaycastAll(sample, Vector3.down, 24f, ~0, QueryTriggerInteraction.Ignore)
                .OrderBy(hit => hit.distance);
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.normal.y < 0.68f) continue;
                var path = HierarchyPath(hit.collider.transform);
                if (!RuntimeSmokeAcceptance.IsIndoorFixturePath(path)) continue;
                if (Mathf.Abs(hit.point.y - expectedPosition.y) > 0.2f) continue;
                var candidate = hit.point + Vector3.up * 0.05f;
                if (!HasStandingClearance(candidate))
                {
                    position = default;
                    selection = $"FIXTURE_BLOCKED:{path}";
                    return false;
                }
                position = candidate;
                selection = $"FIXED_FIXTURE:{path}";
                return true;
            }
            position = default;
            selection = $"FIXTURE_NOT_FOUND:{RuntimeSmokeAcceptance.IndoorFixturePath}";
            return false;
        }

        private static bool HasStandingClearance(Vector3 feet)
        {
            var bottom = feet + Vector3.up * CharacterPresentationProfile.ControllerRadius;
            var top = feet + Vector3.up * (CharacterPresentationProfile.ControllerHeight
                - CharacterPresentationProfile.ControllerRadius);
            var overlaps = Physics.OverlapCapsule(bottom, top, CharacterPresentationProfile.ControllerRadius * 0.82f,
                ~0, QueryTriggerInteraction.Ignore);
            return overlaps.All(collider => collider == null || collider.GetComponentInParent<BRParticipant>() != null
                || collider.GetComponentInParent<LootPickup>() != null
                || collider.GetComponentInParent<DeathLootContainer>() != null);
        }

        private static bool HasBlockingGeometryBetween(Vector3 start, Vector3 end, BRParticipant owner)
        {
            var direction = end - start;
            if (direction.sqrMagnitude <= 0.0001f) return false;
            return TryBlockingRay(start, direction.normalized, direction.magnitude - 0.02f, owner, out _);
        }

        private static bool TryBlockingRay(Vector3 origin, Vector3 direction, float distance,
            BRParticipant owner, out RaycastHit blockingHit)
        {
            var hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore)
                .OrderBy(hit => hit.distance);
            foreach (var hit in hits)
            {
                if (hit.collider == null || (owner != null && hit.collider.GetComponentInParent<BRParticipant>() == owner)
                    || hit.collider.GetComponentInParent<BRParticipant>() != null
                    || hit.collider.GetComponentInParent<LootPickup>() != null
                    || hit.collider.GetComponentInParent<DeathLootContainer>() != null
                    || hit.collider.GetComponentInParent<ParticipantWeaponVisual>() != null) continue;
                blockingHit = hit;
                return true;
            }
            blockingHit = default;
            return false;
        }

        private static bool RayIntersectsRenderers(Camera camera, SkinnedMeshRenderer[] renderers)
        {
            if (camera == null || renderers == null) return false;
            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            foreach (var renderer in renderers)
            {
                if (!renderer.bounds.IntersectRay(ray)) continue;
                var mesh = new Mesh();
                renderer.BakeMesh(mesh);
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                var localToWorld = renderer.transform.localToWorldMatrix;
                for (var index = 0; index < triangles.Length; index += 3)
                {
                    var a = localToWorld.MultiplyPoint3x4(vertices[triangles[index]]);
                    var b = localToWorld.MultiplyPoint3x4(vertices[triangles[index + 1]]);
                    var c = localToWorld.MultiplyPoint3x4(vertices[triangles[index + 2]]);
                    if (!RayIntersectsTriangle(ray, a, b, c)) continue;
                    Destroy(mesh);
                    return true;
                }
                Destroy(mesh);
            }
            return false;
        }

        private static bool RayIntersectsTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c)
        {
            var edge1 = b - a;
            var edge2 = c - a;
            var cross = Vector3.Cross(ray.direction, edge2);
            var determinant = Vector3.Dot(edge1, cross);
            if (Mathf.Abs(determinant) < 0.000001f) return false;
            var inverse = 1f / determinant;
            var offset = ray.origin - a;
            var u = Vector3.Dot(offset, cross) * inverse;
            if (u < 0f || u > 1f) return false;
            var q = Vector3.Cross(offset, edge1);
            var v = Vector3.Dot(ray.direction, q) * inverse;
            if (v < 0f || u + v > 1f) return false;
            return Vector3.Dot(edge2, q) * inverse > 0f;
        }

        private static string HierarchyPath(Transform transform)
        {
            var path = transform != null ? transform.name : "NONE";
            for (var parent = transform != null ? transform.parent : null; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }

        private static void FindCombatLane(out Vector3 start, out Vector3 end)
        {
            var directions = new[] { Vector3.forward, Vector3.right };
            var area = PlayableArea.Bounds;
            var center = area.center;
            var searchRadius = Mathf.Min(area.size.x, area.size.z) * 0.32f;
            for (var ring = 0f; ring <= searchRadius; ring += 8f)
            for (var sampleIndex = 0; sampleIndex < (ring < 1f ? 1 : 16); sampleIndex++)
            foreach (var direction in directions)
            {
                var angle = sampleIndex * Mathf.PI * 2f / (ring < 1f ? 1 : 16);
                var x = center.x + Mathf.Cos(angle) * ring;
                var z = center.z + Mathf.Sin(angle) * ring;
                if (!PlayableArea.TryGround(new Vector3(x, 0f, z), out var first, 0f)) continue;
                var secondSample = first + direction * 8f;
                if (!PlayableArea.TryGround(secondSample, out var second, 0f)) continue;
                if (Mathf.Abs(first.y - second.y) > 0.35f || !StaticLaneClear(first, second)) continue;
                start = first;
                end = second;
                return;
            }
            PlayableArea.TryFindOpenGround(PlayableArea.Center, out start);
            end = start + Vector3.forward * 6f;
            if (PlayableArea.TryGround(end, out var groundedEnd, 0f)) end = groundedEnd;
        }

        private static bool StaticLaneClear(Vector3 start, Vector3 end)
        {
            var origin = start + Vector3.up * 1.25f;
            var destination = end + Vector3.up * 1.25f;
            var direction = destination - origin;
            var hits = Physics.RaycastAll(origin, direction.normalized, direction.magnitude - 0.35f, ~0,
                QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.GetComponentInParent<BRParticipant>() != null
                    || hit.collider.GetComponentInParent<LootPickup>() != null) continue;
                return false;
            }
            return true;
        }

        private void TrackMatch(MatchManager match)
        {
            if (match == null || matchStateTracked) return;
            trackedMatch = match;
            originalMatchEnabled = match.enabled;
            matchStateTracked = true;
        }

        private void SetMatchEnabled(MatchManager match, bool enabled)
        {
            TrackMatch(match);
            if (match != null) match.enabled = enabled;
        }

        private void SetBehaviourEnabled(Behaviour behaviour, bool enabled)
        {
            if (behaviour == null) return;
            if (!originalBehaviourStates.ContainsKey(behaviour))
                originalBehaviourStates.Add(behaviour, behaviour.enabled);
            behaviour.enabled = enabled;
        }

        private void SetControllerRadius(CharacterController controller, float radius)
        {
            if (controller == null) return;
            if (!originalControllerStates.ContainsKey(controller))
                originalControllerStates.Add(controller, new ControllerState(controller.enabled, controller.radius));
            controller.radius = radius;
        }

        private GameObject TrackTemporary(GameObject value)
        {
            if (value != null) temporaryObjects.Add(value);
            return value;
        }

        private void Mark(string marker) => observedMarkers.Add(marker);

        private void Check(bool condition, string reason)
        {
            if (!condition) Fail(reason);
        }

        private void Fail(string reason)
        {
            var normalized = string.IsNullOrWhiteSpace(reason)
                ? "UNSPECIFIED" : reason.Replace('\n', ' ').Replace('\r', ' ');
            if (!failureReasons.Add(normalized)) return;
            Debug.LogError($"BR_SMOKE_FAIL={normalized}");
        }

        private void OnRuntimeLog(string condition, string stackTrace, LogType type)
        {
            if (resultLogged || type is not (LogType.Error or LogType.Exception or LogType.Assert)
                || condition.StartsWith("BR_SMOKE_FAIL=", StringComparison.Ordinal)) return;
            var summary = condition.Split('\n')[0];
            Fail($"RUNTIME_LOG:{type}:{summary}");
        }

        private static bool HasPbrTextures(GameObject root)
        {
            if (root == null) return false;
            return root.GetComponentsInChildren<Renderer>(true).Any(renderer =>
                renderer.sharedMaterials.Any(material => material != null
                    && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null
                    && material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null));
        }

        private void FinishAndQuit()
        {
            if (resultLogged) return;
            foreach (var marker in RequiredMarkers)
                if (!observedMarkers.Contains(marker)) Fail($"MISSING_MARKER:{marker}");
            try
            {
                Cleanup();
            }
            catch (Exception exception)
            {
                Fail($"CLEANUP_EXCEPTION:{exception.GetType().Name}:{exception.Message}");
                Debug.LogException(exception);
            }
            resultLogged = true;
            var passed = failureReasons.Count == 0;
            UnregisterLogHandler();
            Debug.Log($"BR_SMOKE_RESULT={(passed ? "PASS" : "FAIL")};FAILURES={failureReasons.Count}");
            Application.Quit(passed ? 0 : 10);
        }

        private void Cleanup()
        {
            if (cleanupComplete) return;
            cleanupComplete = true;
            Time.timeScale = originalTimeScale;
            trackedHud?.CancelHudEditor();
            foreach (var entry in originalControllerStates)
            {
                if (entry.Key == null) continue;
                entry.Key.enabled = entry.Value.Enabled;
                entry.Key.radius = entry.Value.Radius;
            }
            foreach (var entry in originalBehaviourStates)
                if (entry.Key != null) entry.Key.enabled = entry.Value;
            if (trackedMatch != null)
            {
                if (matchStateTracked) trackedMatch.enabled = originalMatchEnabled;
                if (trackedMatch.State != MatchState.Lobby) trackedMatch.ReturnToLobby();
            }
            foreach (var temporary in temporaryObjects)
                if (temporary != null) Destroy(temporary);
            temporaryObjects.Clear();
        }

        private void OnDisable()
        {
            UnregisterLogHandler();
            if (!resultLogged) Cleanup();
        }

        private void OnDestroy()
        {
            UnregisterLogHandler();
            if (!resultLogged) Cleanup();
        }

        private void UnregisterLogHandler()
        {
            if (!logHandlerRegistered) return;
            Application.logMessageReceived -= OnRuntimeLog;
            logHandlerRegistered = false;
        }

        private readonly struct ControllerState
        {
            public ControllerState(bool enabled, float radius)
            {
                Enabled = enabled;
                Radius = radius;
            }

            public bool Enabled { get; }
            public float Radius { get; }
        }

        private struct ImportedDoor
        {
            public ImportedDoor(string id, Vector3 midpoint, Vector3 outside, Vector3 inside)
            {
                Id = id;
                Midpoint = midpoint;
                Outside = outside;
                Inside = inside;
                Width = 0f;
                ClearHeight = 0f;
                Ratio = 0f;
            }

            public string Id;
            public Vector3 Midpoint;
            public Vector3 Outside;
            public Vector3 Inside;
            public float Width;
            public float ClearHeight;
            public float Ratio;
        }

        private struct ImportedWindow
        {
            public ImportedWindow(string id, Vector3 aperture, Vector3 outside, Vector3 inside)
            {
                Id = id;
                Aperture = aperture;
                Outside = outside;
                Inside = inside;
                Width = 0f;
                Sill = 0f;
                Top = 0f;
                ClearHeight = 0f;
            }

            public string Id;
            public Vector3 Aperture;
            public Vector3 Outside;
            public Vector3 Inside;
            public float Width;
            public float Sill;
            public float Top;
            public float ClearHeight;
        }
    }
}
#endif
