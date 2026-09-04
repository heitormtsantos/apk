using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

namespace BattleRoyale
{
    public readonly struct MatchSettingsRects
    {
        public MatchSettingsRects(Rect lookRow, Rect adsRow, Rect fireDragRow, Rect frameRateRow,
            Rect editHudButton, Rect primaryButton, Rect secondaryButton)
        {
            LookRow = lookRow;
            AdsRow = adsRow;
            FireDragRow = fireDragRow;
            FrameRateRow = frameRateRow;
            EditHudButton = editHudButton;
            PrimaryButton = primaryButton;
            SecondaryButton = secondaryButton;
        }

        public Rect LookRow { get; }
        public Rect AdsRow { get; }
        public Rect FireDragRow { get; }
        public Rect FrameRateRow { get; }
        public Rect EditHudButton { get; }
        public Rect PrimaryButton { get; }
        public Rect SecondaryButton { get; }
    }

    public static class MatchSettingsLayout
    {
        private const float RowWidth = 144f;
        private const float RowHeight = 44f;

        public static MatchSettingsRects Lobby(Rect safeArea, bool mobile)
        {
            var topRight = safeArea.height < 500f || (mobile && safeArea.width > safeArea.height);
            var x = topRight ? safeArea.xMax - 340f : safeArea.x + 34f;
            var y = topRight ? safeArea.y + 24f : safeArea.y + 342f;
            var edit = new Rect(safeArea.x + 34f,
                safeArea.y + Mathf.Min(474f, safeArea.height - 56f), 148f, 34f);
            var play = new Rect(safeArea.xMax - 276f, safeArea.yMax - 104f, 242f, 62f);
            return new MatchSettingsRects(
                new Rect(x, y, RowWidth, RowHeight),
                new Rect(x + 158f, y, RowWidth, RowHeight),
                new Rect(x, y + 52f, RowWidth, RowHeight),
                new Rect(x + 158f, y + 52f, RowWidth, RowHeight),
                edit, play, default);
        }

        public static MatchSettingsRects Pause(Rect panel)
        {
            var gap = 12f;
            var rowWidth = Mathf.Min(RowWidth, (panel.width - 60f) * 0.5f);
            var left = panel.x + 24f;
            var right = left + rowWidth + gap;
            var buttonWidth = (panel.width - 60f) * 0.5f;
            return new MatchSettingsRects(
                new Rect(left, panel.y + 66f, rowWidth, RowHeight),
                new Rect(right, panel.y + 66f, rowWidth, RowHeight),
                new Rect(left, panel.y + 132f, rowWidth, RowHeight),
                default,
                new Rect(right, panel.y + 136f, rowWidth, 36f),
                new Rect(left, panel.yMax - 58f, buttonWidth, 38f),
                new Rect(right, panel.yMax - 58f, buttonWidth, 38f));
        }
    }

    public readonly struct LobbySelectionRects
    {
        public LobbySelectionRects(Rect[] modes, Rect[] playlists, Rect summary, Rect rankedSummary)
        {
            Modes = modes;
            Playlists = playlists;
            Summary = summary;
            RankedSummary = rankedSummary;
        }

        public Rect[] Modes { get; }
        public Rect[] Playlists { get; }
        public Rect Summary { get; }
        public Rect RankedSummary { get; }
    }

    public readonly struct BattleLobbyRects
    {
        public BattleLobbyRects(Rect identity, Rect[] currencies, Rect settingsButton, Rect hudButton,
            Rect[] services, Rect[] navigation, Rect teamPanel, Rect[] teamSlots, Rect matchDock,
            Rect mapSummary, Rect playlistSelector, Rect teamSizeSelector, Rect playButton,
            Rect characterFocus, Rect serviceDrawer, Rect promotion)
        {
            Identity = identity;
            Currencies = currencies;
            SettingsButton = settingsButton;
            HudButton = hudButton;
            Services = services;
            Navigation = navigation;
            TeamPanel = teamPanel;
            TeamSlots = teamSlots;
            MatchDock = matchDock;
            MapSummary = mapSummary;
            PlaylistSelector = playlistSelector;
            TeamSizeSelector = teamSizeSelector;
            PlayButton = playButton;
            CharacterFocus = characterFocus;
            ServiceDrawer = serviceDrawer;
            Promotion = promotion;
        }

        public Rect Identity { get; }
        public Rect[] Currencies { get; }
        public Rect SettingsButton { get; }
        public Rect HudButton { get; }
        public Rect[] Services { get; }
        public Rect[] Navigation { get; }
        public Rect TeamPanel { get; }
        public Rect[] TeamSlots { get; }
        public Rect MatchDock { get; }
        public Rect MapSummary { get; }
        public Rect PlaylistSelector { get; }
        public Rect TeamSizeSelector { get; }
        public Rect PlayButton { get; }
        public Rect CharacterFocus { get; }
        public Rect ServiceDrawer { get; }
        public Rect Promotion { get; }
    }

    public static class MatchLobbyLayout
    {
        public const float MinimumTouchHeight = 44f;

        public static LobbySelectionRects Selection(Rect safeArea)
        {
            var panelWidth = Mathf.Min(390f, safeArea.width);
            var contentX = safeArea.x + Mathf.Min(34f, panelWidth * 0.09f);
            var contentWidth = Mathf.Max(220f, panelWidth - (contentX - safeArea.x) * 2f);
            const float gap = 6f;
            var modeWidth = (contentWidth - gap * 3f) / 4f;
            var modes = new Rect[4];
            for (var i = 0; i < modes.Length; i++)
                modes[i] = new Rect(contentX + i * (modeWidth + gap), safeArea.y + 191f, modeWidth, 38f);
            var playlistWidth = (contentWidth - gap) * 0.5f;
            var playlists = new[]
            {
                new Rect(contentX, safeArea.y + 249f, playlistWidth, 34f),
                new Rect(contentX + playlistWidth + gap, safeArea.y + 249f, playlistWidth, 34f)
            };
            return new LobbySelectionRects(modes, playlists,
                new Rect(contentX, safeArea.y + 291f, contentWidth, 20f),
                new Rect(contentX, safeArea.y + 315f, contentWidth, 20f));
        }

        public static Rect TeamRoster(Rect safeArea, bool mobile, int teammateCount)
            => MatchAuxiliaryLayout.TeamRoster(safeArea, mobile, teammateCount);

        public static BattleLobbyRects Balanced(Rect safeArea, int teamSize)
        {
            teamSize = Mathf.Clamp(teamSize, 1, 4);
            var portrait = safeArea.height > safeArea.width;
            var shortLandscape = !portrait && safeArea.height < 430f;
            var margin = portrait || shortLandscape ? 12f : 18f;
            var identityHeight = portrait ? 58f : shortLandscape ? 48f : 62f;
            var identityWidth = portrait ? safeArea.width - margin * 2f : shortLandscape ? 196f : 292f;
            var identity = new Rect(safeArea.x + margin, safeArea.y + margin,
                Mathf.Min(identityWidth, safeArea.width - margin * 2f), identityHeight);
            var utilitySize = MinimumTouchHeight;
            var hudButton = new Rect(safeArea.xMax - margin - utilitySize, safeArea.y + margin,
                utilitySize, utilitySize);
            var settingsButton = new Rect(hudButton.x - utilitySize - 8f, hudButton.y,
                utilitySize, utilitySize);
            var currencies = new Rect[2];
            if (portrait)
            {
                currencies[0] = new Rect(safeArea.x + margin, identity.yMax + 6f, 94f, MinimumTouchHeight);
                currencies[1] = new Rect(currencies[0].xMax + 6f, currencies[0].y, 94f, MinimumTouchHeight);
                settingsButton.y = currencies[0].y;
                hudButton.y = currencies[0].y;
            }
            else
            {
                var currencyWidth = shortLandscape ? 82f : 108f;
                currencies[1] = new Rect(settingsButton.x - 8f - currencyWidth, settingsButton.y,
                    currencyWidth, MinimumTouchHeight);
                currencies[0] = new Rect(currencies[1].x - 6f - currencyWidth, settingsButton.y,
                    currencyWidth, MinimumTouchHeight);
            }

            var services = new Rect[5];
            var navigation = new Rect[3];
            Rect teamPanel;
            var teamSlots = new Rect[4];
            Rect matchDock;
            Rect characterFocus;
            Rect promotion;
            if (portrait)
            {
                var serviceY = currencies[0].yMax + 6f;
                var serviceGap = 4f;
                var serviceWidth = (safeArea.width - margin * 2f - serviceGap * 4f) / 5f;
                for (var i = 0; i < services.Length; i++)
                    services[i] = new Rect(safeArea.x + margin + i * (serviceWidth + serviceGap), serviceY,
                        serviceWidth, MinimumTouchHeight);
                var teamY = serviceY + MinimumTouchHeight + 6f;
                teamPanel = new Rect(safeArea.x + margin, teamY, safeArea.width - margin * 2f, 73f);
                var slotWidth = (teamPanel.width - 18f) / 4f;
                for (var i = 0; i < teamSlots.Length; i++)
                    teamSlots[i] = new Rect(teamPanel.x + 6f + i * slotWidth,
                        teamPanel.y + 23f, slotWidth - 4f, MinimumTouchHeight);
                var dockHeight = 224f;
                matchDock = new Rect(safeArea.x + margin, safeArea.yMax - dockHeight - margin,
                    safeArea.width - margin * 2f, dockHeight);
                var navY = matchDock.y - MinimumTouchHeight - 6f;
                var navWidth = serviceWidth;
                for (var i = 0; i < navigation.Length; i++)
                    navigation[i] = new Rect(safeArea.x + margin + i * (navWidth + 6f), navY,
                        navWidth, MinimumTouchHeight);
                characterFocus = new Rect(teamPanel.x, teamPanel.yMax + 8f, teamPanel.width,
                    Mathf.Max(0f, navigation[0].y - teamPanel.yMax - 16f));
                promotion = new Rect(safeArea.x + margin, characterFocus.y,
                    safeArea.width - margin * 2f, Mathf.Min(84f, characterFocus.height));
            }
            else
            {
                var compactWidth = safeArea.width < 1100f;
                var serviceWidth = MinimumTouchHeight;
                var promotionWidth = shortLandscape ? 164f : compactWidth ? 220f : 250f;
                var promotionHeight = shortLandscape ? 78f : compactWidth ? 112f : 140f;
                promotion = new Rect(safeArea.x + margin,
                    identity.yMax + (shortLandscape ? 7f : 14f), promotionWidth, promotionHeight);
                var serviceY = promotion.yMax + 8f;
                for (var i = 0; i < services.Length; i++)
                    services[i] = new Rect(safeArea.x + margin + i * (serviceWidth + 6f),
                        serviceY, serviceWidth, MinimumTouchHeight);
                var navWidth = compactWidth ? 56f : 138f;
                var navY = safeArea.yMax - margin - MinimumTouchHeight;
                for (var i = 0; i < navigation.Length; i++)
                    navigation[i] = new Rect(safeArea.x + margin + i * (navWidth + 6f), navY,
                        navWidth, MinimumTouchHeight);
                var teamWidth = shortLandscape ? 124f : compactWidth ? 148f : 180f;
                var slotHeight = MinimumTouchHeight;
                teamPanel = new Rect(safeArea.xMax - margin - teamWidth,
                    identity.yMax + (shortLandscape ? 8f : 20f), teamWidth,
                    27f + (shortLandscape ? 2f : 4f) * (slotHeight + 5f) + 5f);
                for (var i = 0; i < teamSlots.Length; i++)
                    teamSlots[i] = shortLandscape
                        ? new Rect(teamPanel.x + 7f + (i % 2) * ((teamPanel.width - 17f) * 0.5f + 3f),
                            teamPanel.y + 27f + (i / 2) * (slotHeight + 5f),
                            (teamPanel.width - 17f) * 0.5f, slotHeight)
                        : new Rect(teamPanel.x + 7f,
                            teamPanel.y + 27f + i * (slotHeight + 5f), teamPanel.width - 14f, slotHeight);
                var dockWidth = shortLandscape ? 398f : compactWidth ? 420f : 462f;
                var dockHeight = shortLandscape ? 110f : 118f;
                matchDock = new Rect(safeArea.xMax - margin - dockWidth,
                    safeArea.yMax - margin - dockHeight, dockWidth, dockHeight);
                var drawerX = promotion.xMax + 8f;
                var drawerWidth = Mathf.Min(compactWidth ? 238f : 290f,
                    Mathf.Max(0f, safeArea.center.x - 70f - drawerX));
                characterFocus = new Rect(drawerX + drawerWidth + 14f, identity.yMax + 6f,
                    Mathf.Max(0f, teamPanel.x - drawerX - drawerWidth - 28f),
                    Mathf.Max(0f, matchDock.y - identity.yMax - 14f));
            }

            Rect mapSummary;
            Rect playButton;
            Rect playlistSelector;
            Rect teamSizeSelector;
            if (portrait)
            {
                var contentX = matchDock.x + 10f;
                var contentWidth = matchDock.width - 20f;
                mapSummary = new Rect(contentX, matchDock.y + 10f, contentWidth, 38f);
                playlistSelector = new Rect(contentX, mapSummary.yMax + 8f, contentWidth, MinimumTouchHeight);
                teamSizeSelector = new Rect(contentX, playlistSelector.yMax + 8f, contentWidth, MinimumTouchHeight);
                playButton = new Rect(contentX, matchDock.yMax - 62f - 10f, contentWidth, 62f);
            }
            else
            {
                var innerY = matchDock.y + 10f;
                var mapWidth = shortLandscape ? 100f : 142f;
                var playWidth = shortLandscape ? 102f : 146f;
                mapSummary = new Rect(matchDock.x + 10f, innerY, mapWidth, MinimumTouchHeight);
                playButton = new Rect(matchDock.xMax - playWidth - 10f, innerY, playWidth,
                    matchDock.height - 20f);
                var optionsX = mapSummary.xMax + 8f;
                var optionsWidth = Mathf.Max(174f, playButton.x - optionsX - 8f);
                playlistSelector = new Rect(optionsX, innerY, optionsWidth, MinimumTouchHeight);
                teamSizeSelector = new Rect(optionsX, playlistSelector.yMax + 6f,
                    optionsWidth, MinimumTouchHeight);
                mapSummary.height = matchDock.height - 20f;
            }

            Rect serviceDrawer;
            if (portrait)
            {
                serviceDrawer = new Rect(safeArea.x + margin, characterFocus.y,
                    safeArea.width - margin * 2f, Mathf.Min(190f, characterFocus.height));
            }
            else
            {
                var drawerX = promotion.xMax + 8f;
                var drawerWidth = Mathf.Min(safeArea.width < 1100f ? 238f : 290f,
                    Mathf.Max(0f, safeArea.center.x - 70f - drawerX));
                serviceDrawer = new Rect(drawerX, services[0].y, drawerWidth,
                    Mathf.Max(MinimumTouchHeight, Mathf.Min(270f,
                        navigation[0].y - services[0].y - 8f)));
            }

            return new BattleLobbyRects(identity, currencies, settingsButton, hudButton, services, navigation,
                teamPanel, teamSlots, matchDock, mapSummary, playlistSelector, teamSizeSelector,
                playButton, characterFocus, serviceDrawer, promotion);
        }
    }

    public static class MinimapVisibilityPolicy
    {
        public const float EnemyRevealSeconds = 2.8f;
        public const float EnemyRevealDistance = 48f;

        public static bool ShouldShow(bool isLocalPlayer, bool isTeammate,
            float secondsSinceLastShot, float sqrDistanceToPlayer) => isLocalPlayer || isTeammate
            || (secondsSinceLastShot <= EnemyRevealSeconds
                && sqrDistanceToPlayer <= EnemyRevealDistance * EnemyRevealDistance);
    }

    public static class MatchHudText
    {
        public static string TeamRosterLine(string displayName, string state, float distanceMeters,
            float bleedoutSeconds)
        {
            var safeName = string.IsNullOrEmpty(displayName) ? "ALIADO" : displayName;
            var safeState = string.IsNullOrEmpty(state) ? "ELIMINADO" : state;
            var status = safeState == "DERRUBADO"
                ? $"{safeState} {Mathf.Max(0f, bleedoutSeconds):0}s"
                : safeState;
            return $"{safeName}  |  {Mathf.Max(0f, distanceMeters):0} m  |  {status}";
        }

        public static string StagingStatus(int currentParticipants, int expectedParticipants,
            int teamCount, float secondsRemaining) =>
            $"{Mathf.Max(0, currentParticipants)}/{Mathf.Max(1, expectedParticipants)}  |  "
            + $"{Mathf.Max(1, teamCount)} EQUIPES  |  {Mathf.Max(0f, secondsRemaining):0}s";
    }

    public static class MatchAuxiliaryLayout
    {
        public static Rect LobbyPanel(Rect safeArea)
        {
            var width = Mathf.Min(390f, safeArea.width <= 600f ? safeArea.width : safeArea.width * 0.48f);
            return Fit(new Rect(safeArea.xMin, safeArea.yMin, width, safeArea.height), safeArea);
        }

        public static Rect StagingStatus(Rect safeArea) => Fit(
            new Rect(safeArea.center.x - 165f, safeArea.yMin + 22f, 330f, 74f), safeArea);

        public static Rect TeamRoster(Rect safeArea, bool mobile, int teammateCount)
        {
            var width = mobile ? Mathf.Min(214f, safeArea.width - 24f) : 250f;
            var x = mobile ? safeArea.xMax - width - 12f : safeArea.xMin + 18f;
            var y = mobile ? safeArea.yMin + 246f : safeArea.yMin + 108f;
            return Fit(new Rect(x, y, width, Mathf.Max(0, teammateCount) * 32f + 8f), safeArea);
        }

        public static Rect DeathLootPanel(Rect safeArea, bool mobile, int entryCount)
        {
            var width = mobile
                ? Mathf.Min(420f, Mathf.Max(300f, safeArea.width * 0.48f))
                : Mathf.Min(390f, safeArea.width * 0.46f);
            width = Mathf.Min(width, safeArea.width - 24f);
            var height = Mathf.Min(104f + Mathf.Max(0, entryCount) * 42f, 394f);
            return Fit(new Rect(safeArea.xMax - width - (mobile ? 12f : 18f),
                safeArea.center.y - height * 0.5f, width, height), safeArea);
        }

        public static Rect ResultPanel(Rect safeArea, bool ranked)
        {
            var width = Mathf.Min(470f, safeArea.width - 24f);
            var height = Mathf.Min(ranked ? 310f : 250f, safeArea.height - 24f);
            return Fit(new Rect(safeArea.center.x - width * 0.5f,
                safeArea.center.y - height * 0.5f, width, height), safeArea);
        }

        public static Rect PausePanel(Rect safeArea)
        {
            var width = Mathf.Min(380f, safeArea.width - 24f);
            var height = Mathf.Min(310f, safeArea.height - 24f);
            return Fit(new Rect(safeArea.center.x - width * 0.5f,
                safeArea.center.y - height * 0.5f, width, height), safeArea);
        }

        public static Rect DeploymentTelemetry(Rect safeArea) => Fit(
            new Rect(safeArea.xMin + 18f, safeArea.yMax - 104f, 244f, 86f), safeArea);

        public static bool Contains(Rect safeArea, Rect rect) => rect.width >= 0f && rect.height >= 0f
            && rect.xMin >= safeArea.xMin && rect.yMin >= safeArea.yMin
            && rect.xMax <= safeArea.xMax && rect.yMax <= safeArea.yMax;

        private static Rect Fit(Rect rect, Rect safeArea)
        {
            var width = Mathf.Clamp(rect.width, 0f, Mathf.Max(0f, safeArea.width));
            var height = Mathf.Clamp(rect.height, 0f, Mathf.Max(0f, safeArea.height));
            var x = Mathf.Clamp(rect.x, safeArea.xMin, safeArea.xMax - width);
            var y = Mathf.Clamp(rect.y, safeArea.yMin, safeArea.yMax - height);
            return new Rect(x, y, width, height);
        }
    }

    public sealed class MatchHUD : MonoBehaviour
    {
        private MatchManager match;
        private GUIStyle small;
        private GUIStyle medium;
        private GUIStyle large;
        private GUIStyle centered;
        private GUIStyle hero;
        private GUIStyle noAmmo;
        private GUIStyle lowAmmo;
        private GUIStyle damageBody;
        private GUIStyle damageHeadshot;
        private GUIStyle eliminationRight;
        private GUIStyle eliminationCenter;
        private GUIStyle pauseTitle;
        private GUIStyle editorFrame;
        private GUIStyle editorFrameCompact;
        private GUIStyle lobbyTitle;
        private GUIStyle lobbyTitleCentered;
        private GUIStyle lobbyCaption;
        private GUIStyle lobbyCaptionCentered;
        private GUIStyle lobbyCentered;
        private GUIStyle lobbyButton;
        private GUIStyle lobbyButtonLeft;
        private GUIStyle lobbyPrimaryButton;
        private GUIStyle lobbySectionTitle;
        private Texture2D white;
        private Texture2D zoneRing;
        private Texture2D controlDisc;
        private Texture2D minimapTexture;
        private Vector2 deathLootScroll;
        private Vector2 backpackInventoryScroll;
        private readonly List<LootPickup> nearbyInventoryLoot = new(16);
        private Vector2 matchShopScroll;
        private HUDLayoutProfile desktopLayout;
        private HUDLayoutProfile mobileLayout;
        private HUDLayoutProfile desktopSnapshot;
        private HUDLayoutProfile mobileSnapshot;
        private Func<HUDElementId, Rect> desktopDefaults;
        private Func<HUDElementId, Rect> mobileDefaults;
        private Rect configuredSafeArea;
        private LobbySelectionRects lobbySelection;
        private BattleLobbyRects battleLobby;
        private bool layoutsInitialized;
        private bool mobileControlsDirty = true;
        private readonly Stack<HUDLayoutProfile> editHistory = new();
        private readonly List<BRParticipant> teamRosterBuffer = new(3);
        private bool editingHud;
        private bool editMobile;
        private bool snapToGrid = true;
        private bool editorToolbarTop;
        private HUDElementId selectedElement = HUDElementId.Vitals;
        private bool draggingElement;
        private bool resizingElement;
        private bool showLobbySettings;
        private bool showFullMap;
        private int lobbyDrawer = -1;
        private ClothingSlot wardrobeSlot = ClothingSlot.Shirt;
        private string lobbyFeedback;
        private float lobbyFeedbackUntil;
        private Vector2 dragOffset;
        private Rect dragStartRect;
        private Vector2 dragStartMouse;

        private enum SensitivitySetting
        {
            Look,
            Ads,
            FireDrag
        }

        private enum CombatItemIcon
        {
            Ammo,
            MedKit,
            Armor,
            Helmet,
            Attachment,
            GlooWall,
            Grenade,
            Currency,
            Backpack
        }

        private static readonly Color Panel = new(0.035f, 0.045f, 0.055f, 0.88f);
        private static readonly Color Cyan = new(0.1f, 0.82f, 0.95f, 1f);
        private static readonly Color Amber = new(1f, 0.68f, 0.14f, 1f);
        private static readonly int[] FrameRateOptions = { 30, 60, 90 };
        private static readonly BRMatchMode[] LobbyModes =
            { BRMatchMode.Solo, BRMatchMode.Duo, BRMatchMode.Squad };
        private static readonly string[] LobbyModeLabels = { "SOLO", "DUO", "SQUAD" };
        private static readonly string[] LobbyNavigationLabels = { "ARSENAL", "OPERADOR", "COLECAO" };
        private static readonly ClothingSlot[] WardrobeSlots =
        {
            ClothingSlot.Shirt, ClothingSlot.Pants, ClothingSlot.Shoes,
            ClothingSlot.Hair, ClothingSlot.Hat, ClothingSlot.Face
        };
        private static readonly string[] WardrobeSlotLabels =
        {
            "CIMA", "CALCA", "TENIS", "CABELO", "CHAPEU", "ROSTO"
        };
        private static readonly string[] LobbyServiceLabels =
            { "LOJA", "LUCK ROYALE", "PASSE", "MISSOES", "EVENTOS" };
        private static readonly MatchShopCategory[] MatchShopCategories =
        {
            MatchShopCategory.Weapons, MatchShopCategory.Ammo, MatchShopCategory.Healing,
            MatchShopCategory.Armor, MatchShopCategory.GlooWall, MatchShopCategory.Throwables,
            MatchShopCategory.Utility
        };
        private static readonly string[] MatchShopCategoryLabels =
            { "ARMAS", "MUNICAO", "CURA", "PROTECAO", "GEL", "GRANADAS", "UTIL" };
        private static readonly LobbyIconId[] LobbyServiceIcons =
            { LobbyIconId.Store, LobbyIconId.Luck, LobbyIconId.Pass, LobbyIconId.Missions, LobbyIconId.Events };
        private static readonly LobbyIconId[] LobbyNavigationIcons =
            { LobbyIconId.Arsenal, LobbyIconId.Operator, LobbyIconId.Collection };
        private static readonly string[] LobbyDrawerTitles =
        {
            "LOJA DE CAMPO", "LUCK ROYALE", "PASSE RAVEN", "MISSOES", "EVENTOS",
            "ARSENAL", "OPERADOR", "COLECAO"
        };
        private static readonly string[] LobbyDrawerCaptions =
        {
            "Pacotes e ofertas da temporada", "Recompensas rotativas", "Progresso da temporada",
            "Objetivos diarios e semanais", "Operacoes por tempo limitado", "Equipamentos encontrados na ilha",
            "Personalizacao do combatente", "Visuais, emotes e paraquedas"
        };

        public void Configure(MatchManager manager) => match = manager;
        public bool MinimapReady => minimapTexture != null;
        public Texture2D MinimapTexture => minimapTexture;
        public bool HudEditorOpen => editingHud;
        public bool LobbyDrawerOpen => lobbyDrawer >= 0;
        public IReadOnlyList<BRParticipant> TeamRosterBuffer => teamRosterBuffer;
        public void OpenHudEditor(bool mobileProfile)
        {
            BeginHudEdit();
            editMobile = mobileProfile;
            selectedElement = mobileProfile ? HUDElementId.Fire : HUDElementId.Vitals;
        }
        public void CancelHudEditor() => CancelHudEdit();
        public void OpenWardrobe() => lobbyDrawer = 7;
        public void CloseLobbyDrawer() => lobbyDrawer = -1;

        private IEnumerator Start()
        {
            EnsureLayouts();
            yield return null;
            yield return null;
            CaptureMinimapTexture();
        }

        private void OnGUI()
        {
            if (match == null) return;
            GUI.depth = 0;
            EnsureStyles();
            EnsureLayouts();
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.M
                && match.State is MatchState.Plane or MatchState.Active)
            {
                showFullMap = !showFullMap;
                Event.current.Use();
            }
            if (match.State == MatchState.Lobby)
            {
                DrawMainLobby();
                if (editingHud) DrawHudEditor();
                return;
            }
            if (match.Paused)
            {
                DrawPauseOverlay();
                if (editingHud) DrawHudEditor();
                return;
            }
            if (match.State == MatchState.Complete)
            {
                DrawMatchOverlay();
                return;
            }
            if (match.State == MatchState.Staging)
            {
                DrawConfigured(HUDElementId.MatchStatus);
                DrawTeamRoster(match.Player);
                DrawControlsHint();
                if (MobileRuntime) DrawMobileControls();
                DrawConfigured(HUDElementId.Pause);
                return;
            }
            var player = match.Player;
            DrawConfigured(HUDElementId.Vitals, player);
            DrawTeamRoster(player);
            DrawConfigured(HUDElementId.MatchStatus);
            DrawConfigured(HUDElementId.Compass);
            DrawConfigured(HUDElementId.Minimap);
            if (match.State == MatchState.Active)
            {
                DrawMatchCurrency();
                if (match.InventoryOpen)
                {
                    DrawBackpackInventoryPanel();
                    DrawSafeZoneAlert(player);
                }
                else if (match.ActiveVehicle != null)
                {
                    DrawVehicleHUD();
                    DrawSafeZoneAlert(player);
                }
                else
                {
                    DrawConfigured(HUDElementId.Weapon, player);
                    DrawConfigured(HUDElementId.LootPrompt);
                    DrawConfigured(HUDElementId.PickupFeedback);
                    if (match.MatchShop == null || !match.MatchShop.IsShopOpen)
                    {
                        DrawCrosshair(player);
                        DrawDamageFeedback();
                        DrawCombatAlerts(player);
                        DrawSafeZoneAlert(player);
                        DrawDeathLootPanel();
                    }
                    else DrawMatchShopPanel();
                }
            }
            else DrawDeploymentTelemetry(player);
            DrawControlsHint();
            if (MobileRuntime && match.ActiveDeathLoot == null && !match.InventoryOpen)
            {
                if (match.ActiveVehicle != null) DrawVehicleMobileControls();
                else DrawMobileControls();
            }
            if (match.State == MatchState.Active && match.ActiveVehicle == null && !match.InventoryOpen
                && match.ActiveDeathLoot == null) DrawBackpackQuickButton(player);
            if (showFullMap) DrawFullMap();
            DrawConfigured(HUDElementId.Pause);
        }

        private void DrawBackpackInventoryPanel()
        {
            var player = match.Player;
            if (player == null) return;
            var safe = GuiSafeArea;
            var width = Mathf.Min(MobileRuntime ? safe.width - 20f : 620f, safe.width - 20f);
            var height = Mathf.Min(safe.height - 32f, 610f);
            var rect = new Rect(safe.xMax - width - 10f, safe.center.y - height * 0.5f, width, height);
            Fill(rect, new Color(0.025f, 0.035f, 0.045f, 0.97f));
            DrawOutline(rect, new Color(0.20f, 0.82f, 0.94f, 0.72f), 1f);

            var backpack = player.Backpack?.Current;
            GUI.Label(new Rect(rect.x + 18f, rect.y + 12f, rect.width - 100f, 26f),
                backpack != null ? backpack.displayName.ToUpperInvariant() : "SEM MOCHILA", large);
            if (GUI.Button(new Rect(rect.xMax - 48f, rect.y + 10f, 34f, 30f), "X"))
            {
                match.CloseInventory();
                return;
            }
            var inventory = player.Inventory;
            var ratio = inventory.CurrentUsage / Mathf.Max(1f, inventory.MaxCapacity);
            DrawBar(new Rect(rect.x + 18f, rect.y + 48f, rect.width - 36f, 18f), ratio,
                ratio > 0.92f ? new Color(1f, 0.32f, 0.18f) : Cyan,
                $"CAPACIDADE  {inventory.CurrentUsage:0.#} / {inventory.MaxCapacity:0.#}");

            var left = new Rect(rect.x + 18f, rect.y + 82f, (rect.width - 54f) * 0.55f, rect.height - 100f);
            var right = new Rect(left.xMax + 18f, left.y, rect.xMax - left.xMax - 36f, left.height);
            Fill(left, new Color(1f, 1f, 1f, 0.045f));
            Fill(right, new Color(1f, 1f, 1f, 0.045f));
            GUI.Label(new Rect(left.x + 10f, left.y + 5f, left.width - 20f, 24f), "ITENS", medium);
            GUI.Label(new Rect(right.x + 10f, right.y + 5f, right.width - 20f, 24f), "PROXIMO", medium);

            var y = left.y + 34f;
            foreach (AmmoKind kind in Enum.GetValues(typeof(AmmoKind)))
            {
                var count = inventory.AmmoFor(kind);
                if (count <= 0) continue;
                DrawInventoryDropRow(new Rect(left.x + 8f, y, left.width - 16f, 36f),
                    $"MUNICAO {kind.ToString().ToUpperInvariant()}  x{count}", () => match.DropAmmo(kind));
                y += 40f;
            }
            if (inventory.MedKits > 0)
            {
                DrawInventoryDropRow(new Rect(left.x + 8f, y, left.width - 16f, 36f),
                    $"KIT MEDICO  x{inventory.MedKits}", match.DropMedKit);
                y += 40f;
            }
            if (inventory.GlooWalls > 0)
            {
                DrawInventoryDropRow(new Rect(left.x + 8f, y, left.width - 16f, 36f),
                    $"PAREDE DE GEL  x{inventory.GlooWalls}", match.DropGlooWall);
                y += 40f;
            }
            if (inventory.Grenades > 0)
            {
                DrawInventoryDropRow(new Rect(left.x + 8f, y, left.width - 16f, 36f),
                    $"GRANADA  x{inventory.Grenades}", match.DropGrenade);
                y += 40f;
            }
            if (backpack != null)
                DrawInventoryDropRow(new Rect(left.x + 8f, y, left.width - 16f, 36f),
                    backpack.displayName.ToUpperInvariant(), match.DropBackpack);

            match.FillNearbyInventoryLoot(nearbyInventoryLoot);
            var contentHeight = Mathf.Max(right.height - 42f, nearbyInventoryLoot.Count * 42f);
            var viewport = new Rect(right.x + 6f, right.y + 32f, right.width - 12f, right.height - 38f);
            backpackInventoryScroll = GUI.BeginScrollView(viewport, backpackInventoryScroll,
                new Rect(0f, 0f, viewport.width - 16f, contentHeight));
            for (var i = 0; i < nearbyInventoryLoot.Count; i++)
            {
                var pickup = nearbyInventoryLoot[i];
                if (pickup == null) continue;
                var row = new Rect(0f, i * 42f, viewport.width - 18f, 36f);
                Fill(row, new Color(1f, 1f, 1f, 0.07f));
                GUI.Label(new Rect(row.x + 8f, row.y, row.width - 64f, row.height), pickup.DisplayName, small);
                if (GUI.Button(new Rect(row.xMax - 54f, row.y + 5f, 50f, 26f), "PEGAR"))
                    match.CollectNearbyInventoryLoot(pickup);
            }
            GUI.EndScrollView();
        }

        private void DrawBackpackQuickButton(BRParticipant player)
        {
            if (player == null) return;
            var safe = GuiSafeArea;
            var size = MobileRuntime ? 58f : 46f;
            var rect = MobileRuntime
                ? new Rect(safe.xMax - 178f, safe.yMax - 174f, size, size)
                : new Rect(safe.xMax - 322f, safe.yMax - 72f, size, size);
            Fill(rect, new Color(0.025f, 0.055f, 0.07f, 0.82f));
            DrawOutline(rect, new Color(0.25f, 0.82f, 0.94f, 0.82f), 1f);
            DrawItemIcon(new Rect(rect.x + 9f, rect.y + 7f, rect.width - 18f, rect.height - 20f),
                CombatItemIcon.Backpack, Color.white);
            GUI.Label(new Rect(rect.x + 2f, rect.yMax - 15f, rect.width - 4f, 14f),
                $"{player.Inventory.CurrentUsage:0}/{player.Inventory.MaxCapacity:0}", centered);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) match.OpenInventory();
        }

        private void DrawInventoryDropRow(Rect rect, string label, Func<bool> drop)
        {
            Fill(rect, new Color(1f, 1f, 1f, 0.07f));
            GUI.Label(new Rect(rect.x + 8f, rect.y, rect.width - 92f, rect.height), label, small);
            if (GUI.Button(new Rect(rect.xMax - 80f, rect.y + 5f, 74f, 26f), "LARGAR")) drop?.Invoke();
        }

        private void DrawMainLobby()
        {
            var safe = GuiSafeArea;
            battleLobby = MatchLobbyLayout.Balanced(safe, match.TeamSize);
            DrawLobbyIdentity(battleLobby.Identity);
            DrawLobbyCurrencies(battleLobby.Currencies);
            DrawLobbyPromotion(battleLobby.Promotion);
            DrawLobbyServices(battleLobby.Services);
            DrawLobbyNavigation(battleLobby.Navigation);
            if (lobbyDrawer >= 0) DrawLobbyDrawer(battleLobby.ServiceDrawer);
            DrawLobbyTeamPanel(battleLobby);
            DrawLobbyMatchDock(battleLobby);

            if (safe.width > safe.height)
            {
                GUI.Label(new Rect(safe.center.x - 105f, safe.y + 14f, 210f, 26f),
                    "RAVEN DROP", lobbyTitleCentered);
                GUI.Label(new Rect(safe.center.x - 105f, safe.y + 38f, 210f, 18f),
                    "OPERATIONS COMMAND", lobbyCaptionCentered);
            }
            if (DrawLobbyUtilityButton(battleLobby.SettingsButton, false, "Configuracoes"))
                showLobbySettings = !showLobbySettings;
            if (!editingHud && DrawLobbyUtilityButton(battleLobby.HudButton, true, "Editar HUD"))
                BeginHudEdit();
            if (!string.IsNullOrEmpty(lobbyFeedback) && Time.unscaledTime < lobbyFeedbackUntil)
            {
                var toastWidth = Mathf.Min(420f, safe.width - 24f);
                var toast = new Rect(safe.center.x - toastWidth * 0.5f,
                    battleLobby.MatchDock.y - 42f, toastWidth, 32f);
                Fill(toast, new Color(0.025f, 0.035f, 0.04f, 0.94f));
                Fill(new Rect(toast.x, toast.y, 4f, toast.height), Cyan);
                GUI.Label(new Rect(toast.x + 12f, toast.y, toast.width - 20f, toast.height),
                    lobbyFeedback, lobbyCentered);
            }
            if (showLobbySettings) DrawLobbySettingsOverlay(safe);
        }

        private void DrawLobbyIdentity(Rect rect)
        {
            FillCutPanel(rect, new Color(0.02f, 0.035f, 0.045f, 0.82f), Cyan);
            var portraitSize = Mathf.Min(48f, rect.height - 10f);
            var portrait = new Rect(rect.x + 6f, rect.y + 5f, portraitSize, portraitSize);
            Fill(portrait, new Color(0.12f, 0.55f, 0.64f, 0.92f));
            DrawOutline(portrait, Cyan, 2f);
            DrawLobbyIcon(new Rect(portrait.x + 7f, portrait.y + 7f,
                portrait.width - 14f, portrait.height - 14f), LobbyIconId.Operator);
            var textX = portrait.xMax + 10f;
            GUI.Label(new Rect(textX, rect.y + 5f, rect.xMax - textX - 8f, 23f),
                "RAVEN UNIT", medium);
            GUI.Label(new Rect(textX, rect.y + 27f, rect.xMax - textX - 8f, 17f),
                $"NV. {match.AccountProfile?.Levels?.Level ?? 1}  |  {match.Tier.ToString().ToUpperInvariant()}", lobbyCaption);
            var rating = match.Profile?.Rating ?? 0;
            var tierStart = RankedProgression.ThresholdForTier(match.Tier);
            var nextTier = (RankedTier)Mathf.Min((int)RankedTier.Master, (int)match.Tier + 1);
            var tierEnd = match.Tier == RankedTier.Master
                ? tierStart + 400 : RankedProgression.ThresholdForTier(nextTier);
            var progress = Mathf.InverseLerp(tierStart, Mathf.Max(tierStart + 1, tierEnd), rating);
            DrawBar(new Rect(textX, rect.yMax - 11f, rect.xMax - textX - 8f, 5f), progress,
                match.Playlist == BRPlaylist.Ranked ? Amber : Cyan, string.Empty);
        }

        private void DrawLobbyCurrencies(Rect[] currencies)
        {
            var account = match.AccountProfile;
            if (account == null || account.State == ProfileLoadState.Loading)
            {
                DrawCurrency(currencies[0], LobbyIconId.Coin, "...", Amber);
                DrawCurrency(currencies[1], LobbyIconId.Diamond, "...", Cyan);
                return;
            }
            DrawCurrency(currencies[0], LobbyIconId.Coin,
                FormatAccountBalance(account.Wallet.GetBalance(CurrencyType.SoftCurrency)), Amber);
            DrawCurrency(currencies[1], LobbyIconId.Diamond,
                FormatAccountBalance(account.Wallet.GetBalance(CurrencyType.PremiumCurrency)) + "+", Cyan);
        }

        private static string FormatAccountBalance(long value) => value >= 1000000
            ? $"{value / 1000000f:0.#}M" : value >= 10000 ? $"{value / 1000f:0.#}K" : value.ToString();

        private bool DrawLobbyUtilityButton(Rect rect, bool crosshair, string tooltip)
        {
            Fill(rect, new Color(0.08f, 0.11f, 0.13f, 0.94f));
            DrawLobbyIcon(new Rect(rect.x + 9f, rect.y + 9f, rect.width - 18f, rect.height - 18f),
                crosshair ? LobbyIconId.Hud : LobbyIconId.Settings);
            return GUI.Button(rect, new GUIContent(string.Empty, tooltip), lobbyButton);
        }

        private static void DrawLobbyIcon(Rect rect, LobbyIconId iconId)
        {
            var sprite = LobbyIconCatalog.Load(iconId);
            if (sprite == null) return;
            GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true);
        }

        private void DrawCurrency(Rect rect, LobbyIconId iconId, string value, Color color)
        {
            FillCutPanel(rect, new Color(0.02f, 0.035f, 0.045f, 0.8f), color);
            var icon = new Rect(rect.x + 5f, rect.y + 7f, 30f, 30f);
            Fill(icon, new Color(color.r, color.g, color.b, 0.1f));
            DrawLobbyIcon(new Rect(icon.x + 4f, icon.y + 4f,
                icon.width - 8f, icon.height - 8f), iconId);
            GUI.Label(new Rect(icon.xMax + 7f, rect.y, rect.width - 44f, rect.height), value,
                lobbyCentered);
        }

        private void DrawLobbyServices(Rect[] services)
        {
            for (var i = 0; i < services.Length; i++)
            {
                var rect = services[i];
                var selected = lobbyDrawer == i;
                var hovered = rect.Contains(Event.current.mousePosition);
                FillCutPanel(rect, selected ? new Color(0.08f, 0.22f, 0.25f, 0.94f)
                    : hovered ? new Color(0.07f, 0.12f, 0.14f, 0.94f)
                    : new Color(0.015f, 0.03f, 0.04f, 0.78f), i is 0 or 2 ? Amber : Cyan);
                var compact = rect.width < 100f;
                var icon = compact
                    ? new Rect(rect.center.x - 13f, rect.center.y - 13f, 26f, 26f)
                    : new Rect(rect.x + 11f, rect.y + 9f, 26f, 26f);
                DrawLobbyIcon(icon, LobbyServiceIcons[i]);
                if (!compact)
                    GUI.Label(new Rect(icon.xMax + 7f, rect.y, rect.xMax - icon.xMax - 11f, rect.height),
                        LobbyServiceLabels[i], lobbyButtonLeft);
                if (GUI.Button(rect, new GUIContent(string.Empty, LobbyServiceLabels[i]), lobbyButton))
                    lobbyDrawer = selected ? -1 : i;
                if (i is 2 or 4) DrawLobbyBadge(rect, i == 2 ? "3" : "!");
            }
        }

        private void DrawLobbyPromotion(Rect rect)
        {
            if (rect.width < 80f || rect.height < MatchLobbyLayout.MinimumTouchHeight) return;
            FillCutPanel(rect, new Color(0.025f, 0.055f, 0.07f, 0.78f), Amber);
            var compact = rect.height < 100f;
            DrawLobbyIcon(new Rect(rect.x + 12f, rect.y + 12f, compact ? 26f : 36f,
                compact ? 26f : 36f), LobbyIconId.Events);
            GUI.Label(new Rect(rect.x + (compact ? 46f : 56f), rect.y + 8f,
                rect.width - (compact ? 56f : 68f), 24f), "OPERACAO NIGHTFALL", lobbySectionTitle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + (compact ? 34f : 50f), rect.width - 24f, 20f),
                "RECOMPENSA DE TEMPORADA", lobbyCaption);
            if (!compact)
            {
                Fill(new Rect(rect.x + 12f, rect.y + 78f, rect.width - 24f, 4f),
                    new Color(1f, 1f, 1f, 0.16f));
                Fill(new Rect(rect.x + 12f, rect.y + 78f, (rect.width - 24f) * 0.68f, 4f), Amber);
                GUI.Label(new Rect(rect.x + 12f, rect.yMax - 42f, rect.width - 96f, 28f),
                    "FALTAM 2 DIAS", lobbyCaption);
                GUI.Label(new Rect(rect.xMax - 88f, rect.yMax - 42f, 76f, 28f), "VER EVENTO", lobbyCaptionCentered);
            }
            if (GUI.Button(rect, new GUIContent(string.Empty, "Abrir eventos"), lobbyButton)) lobbyDrawer = 4;
        }

        private void DrawLobbyBadge(Rect anchor, string text)
        {
            var badge = new Rect(anchor.xMax - 13f, anchor.y - 4f, 18f, 18f);
            Fill(badge, new Color(0.95f, 0.22f, 0.12f, 1f));
            GUI.Label(badge, text, lobbyCaptionCentered);
        }

        private void DrawLobbyNavigation(Rect[] navigation)
        {
            for (var i = 0; i < navigation.Length; i++)
            {
                Fill(navigation[i], new Color(0.025f, 0.035f, 0.04f, 0.88f));
                Fill(new Rect(navigation[i].x, navigation[i].y, navigation[i].width, 2f),
                    i == 2 ? Amber : Cyan);
                var compact = navigation[i].width < 94f;
                var iconSize = 25f;
                var icon = compact
                    ? new Rect(navigation[i].center.x - iconSize * 0.5f,
                        navigation[i].center.y - iconSize * 0.5f, iconSize, iconSize)
                    : new Rect(navigation[i].x + 9f, navigation[i].center.y - iconSize * 0.5f,
                        iconSize, iconSize);
                DrawLobbyIcon(icon, LobbyNavigationIcons[i]);
                if (!compact)
                    GUI.Label(new Rect(icon.xMax + 5f, navigation[i].y,
                        navigation[i].xMax - icon.xMax - 8f, navigation[i].height),
                        LobbyNavigationLabels[i], lobbyButtonLeft);
                if (GUI.Button(navigation[i], new GUIContent(string.Empty, LobbyNavigationLabels[i]),
                    lobbyButton)) lobbyDrawer = lobbyDrawer == i + 5 ? -1 : i + 5;
            }
        }

        private void DrawLobbyDrawer(Rect rect)
        {
            if (rect.width < 120f || rect.height < MinimumDrawerHeight) return;
            Fill(rect, new Color(0.018f, 0.032f, 0.04f, 0.97f));
            DrawOutline(rect, new Color(Cyan.r, Cyan.g, Cyan.b, 0.58f), 1f);
            Fill(new Rect(rect.x, rect.y, rect.width, 3f), lobbyDrawer is 0 or 2 ? Amber : Cyan);
            var iconId = lobbyDrawer < LobbyServiceIcons.Length
                ? LobbyServiceIcons[lobbyDrawer] : LobbyNavigationIcons[lobbyDrawer - 5];
            DrawLobbyIcon(new Rect(rect.x + 14f, rect.y + 13f, 30f, 30f), iconId);
            GUI.Label(new Rect(rect.x + 53f, rect.y + 9f, rect.width - 98f, 24f),
                LobbyDrawerTitles[lobbyDrawer], lobbySectionTitle);
            GUI.Label(new Rect(rect.x + 53f, rect.y + 31f, rect.width - 98f, 19f),
                LobbyDrawerCaptions[lobbyDrawer], lobbyCaption);
            var close = new Rect(rect.xMax - 39f, rect.y + 8f, 31f, 31f);
            DrawLobbyIcon(new Rect(close.x + 6f, close.y + 6f, 19f, 19f), LobbyIconId.Close);
            if (GUI.Button(close, new GUIContent(string.Empty, "Fechar"), lobbyButton)) lobbyDrawer = -1;

            if (lobbyDrawer == 7)
            {
                DrawWardrobeDrawer(rect);
                return;
            }
            if (lobbyDrawer == 2)
            {
                DrawBattlePassDrawer(rect);
                return;
            }
            if (lobbyDrawer == 3)
            {
                DrawMissionsDrawer(rect);
                return;
            }
            if (lobbyDrawer == 4)
            {
                DrawRankedSeasonDrawer(rect);
                return;
            }

            var rowY = rect.y + 62f;
            var rowHeight = Mathf.Max(42f, (rect.height - 76f) / 3f);
            for (var i = 0; i < 3; i++)
            {
                if (i > 0) Fill(new Rect(rect.x + 14f, rowY, rect.width - 28f, 1f),
                    new Color(1f, 1f, 1f, 0.12f));
                var marker = new Rect(rect.x + 15f, rowY + 10f, 5f, Mathf.Min(28f, rowHeight - 16f));
                Fill(marker, i == 0 ? Amber : Cyan);
                GUI.Label(new Rect(marker.xMax + 10f, rowY + 4f, rect.width - 48f, 20f),
                    DrawerRowTitle(lobbyDrawer, i), lobbySectionTitle);
                GUI.Label(new Rect(marker.xMax + 10f, rowY + 23f, rect.width - 48f,
                    Mathf.Max(16f, rowHeight - 25f)), DrawerRowCaption(lobbyDrawer, i), lobbyCaption);
                rowY += rowHeight;
            }
        }

        private void DrawBattlePassDrawer(Rect rect)
        {
            var account = match.AccountProfile;
            if (account == null || account.State != ProfileLoadState.Loaded)
            {
                GUI.Label(new Rect(rect.x + 14f, rect.y + 70f, rect.width - 28f, 30f),
                    "CARREGANDO PERFIL...", lobbyCentered);
                return;
            }
            var progress = account.BattlePass.Progress;
            var season = account.BattlePassSeason;
            var tierData = season.Tier(progress.tier);
            var required = Mathf.Max(1f, tierData != null ? tierData.xpRequired : 1000L);
            var y = rect.y + 62f;
            DrawProgressionRow(new Rect(rect.x + 14f, y, rect.width - 28f, 54f),
                $"TIER {progress.tier}", $"{progress.xpIntoTier} / {required:0} XP",
                Mathf.Clamp01(progress.xpIntoTier / required), Amber);
            y += 60f;
            var freeClaim = $"bp_{season.SeasonId}_tier_{progress.tier:D3}_free";
            var freeClaimed = progress.claimedRewardIds.Contains(freeClaim);
            DrawActionRow(new Rect(rect.x + 14f, y, rect.width - 28f, 48f),
                "TRILHA GRATUITA", freeClaimed ? "COLETADO" : "COLETAR", () =>
                {
                    var result = account.BattlePass.Claim("ui:" + freeClaim + ":" + Guid(),
                        progress.tier, BattlePassTrack.Free);
                    SetLobbyFeedback(result.Success ? "Recompensa gratuita coletada." : result.Message);
                }, freeClaimed);
            y += 54f;
            var premiumClaim = $"bp_{season.SeasonId}_tier_{progress.tier:D3}_premium";
            var premiumClaimed = progress.claimedRewardIds.Contains(premiumClaim);
            if (!progress.premiumUnlocked)
                DrawActionRow(new Rect(rect.x + 14f, y, rect.width - 28f, 48f),
                    $"PASSE PREMIUM  {season.PremiumPrice} DIAMONDS", "COMPRAR", () =>
                    {
                        var result = account.BattlePass.BuyPremium("ui:buy_pass:" + season.SeasonId);
                        SetLobbyFeedback(result == AccountPurchaseResult.Success
                            ? "Passe Premium desbloqueado." : result.ToString());
                    }, false);
            else
                DrawActionRow(new Rect(rect.x + 14f, y, rect.width - 28f, 48f),
                    "TRILHA PREMIUM", premiumClaimed ? "COLETADO" : "COLETAR", () =>
                    {
                        var result = account.BattlePass.Claim("ui:" + premiumClaim + ":" + Guid(),
                            progress.tier, BattlePassTrack.Premium);
                        SetLobbyFeedback(result.Success ? "Recompensa premium coletada." : result.Message);
                    }, premiumClaimed);
        }

        private void DrawMissionsDrawer(Rect rect)
        {
            var account = match.AccountProfile;
            if (account == null || account.State != ProfileLoadState.Loaded)
            {
                GUI.Label(new Rect(rect.x + 14f, rect.y + 70f, rect.width - 28f, 30f),
                    "CARREGANDO PERFIL...", lobbyCentered);
                return;
            }
            var y = rect.y + 62f;
            var shown = 0;
            foreach (var definition in account.MissionCatalog.Missions)
            {
                if (definition == null || shown >= 3) continue;
                MissionProgressData progress = null;
                foreach (var value in account.Data.missions)
                    if (value != null && value.missionId == definition.missionId) { progress = value; break; }
                var amount = progress?.progress ?? 0L;
                var complete = progress?.completed == true;
                var label = definition.missionId.Replace("mission_", string.Empty).Replace('_', ' ').ToUpperInvariant();
                DrawProgressionRow(new Rect(rect.x + 14f, y, rect.width - 28f, 54f),
                    label, complete ? "CONCLUIDA" : $"{amount} / {definition.target}",
                    Mathf.Clamp01((float)amount / Mathf.Max(1f, definition.target)), complete ? Amber : Cyan);
                y += 60f;
                shown++;
            }
        }

        private void DrawRankedSeasonDrawer(Rect rect)
        {
            var account = match.AccountProfile;
            if (account == null || account.State != ProfileLoadState.Loaded) return;
            var ranked = account.Ranked.Get(match.Mode);
            var tier = RankedProgression.TierForRating(ranked.rating);
            var next = (RankedTier)Mathf.Min((int)RankedTier.Master, (int)tier + 1);
            var start = RankedProgression.ThresholdForTier(tier);
            var end = tier == RankedTier.Master ? start + 400 : RankedProgression.ThresholdForTier(next);
            var y = rect.y + 62f;
            DrawProgressionRow(new Rect(rect.x + 14f, y, rect.width - 28f, 58f),
                $"{match.Mode.ToString().ToUpperInvariant()}  {tier.ToString().ToUpperInvariant()}",
                $"{ranked.rating} RP  |  MELHOR {ranked.highestRating} RP",
                Mathf.InverseLerp(start, Mathf.Max(start + 1, end), ranked.rating), Amber);
            y += 66f;
            GUI.Label(new Rect(rect.x + 14f, y, rect.width - 28f, 22f),
                account.SeasonConfig.SeasonId.ToUpperInvariant(), lobbySectionTitle);
            GUI.Label(new Rect(rect.x + 14f, y + 23f, rect.width - 28f, 40f),
                $"PARTIDAS {ranked.matches}  |  VITORIAS {ranked.victories}\nMELHOR COLOCACAO #{Mathf.Max(0, ranked.bestPlacement)}",
                lobbyCaption);
        }

        private void DrawProgressionRow(Rect row, string title, string detail, float progress,
            Color accent)
        {
            Fill(row, new Color(1f, 1f, 1f, 0.055f));
            Fill(new Rect(row.x, row.y, 4f, row.height), accent);
            GUI.Label(new Rect(row.x + 12f, row.y + 4f, row.width - 24f, 20f), title, lobbySectionTitle);
            GUI.Label(new Rect(row.x + 12f, row.y + 23f, row.width - 24f, 18f), detail, lobbyCaption);
            Fill(new Rect(row.x + 12f, row.yMax - 8f, row.width - 24f, 3f), new Color(1f, 1f, 1f, 0.14f));
            Fill(new Rect(row.x + 12f, row.yMax - 8f, (row.width - 24f) * Mathf.Clamp01(progress), 3f), accent);
        }

        private void DrawActionRow(Rect row, string title, string action, Action callback, bool disabled)
        {
            Fill(row, new Color(1f, 1f, 1f, 0.055f));
            GUI.Label(new Rect(row.x + 12f, row.y, row.width - 108f, row.height), title, lobbySectionTitle);
            var button = new Rect(row.xMax - 94f, row.y + 7f, 84f, row.height - 14f);
            GUI.enabled = !disabled;
            if (GUI.Button(button, action, lobbyButton)) callback?.Invoke();
            GUI.enabled = true;
        }

        private static string Guid() => System.Guid.NewGuid().ToString("N");

        private void DrawWardrobeDrawer(Rect rect)
        {
            var manager = match != null ? match.LobbyWardrobe : null;
            var database = manager != null ? manager.Database : null;
            var gap = 4f;
            var columns = 3;
            var buttonWidth = (rect.width - 28f - gap * 2f) / columns;
            for (var i = 0; i < WardrobeSlots.Length; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var categoryRect = new Rect(rect.x + 14f + column * (buttonWidth + gap),
                    rect.y + 61f + row * 30f, buttonWidth, 26f);
                Fill(categoryRect, wardrobeSlot == WardrobeSlots[i]
                    ? new Color(0.06f, 0.48f, 0.54f, 0.88f)
                    : new Color(1f, 1f, 1f, 0.07f));
                if (GUI.Button(categoryRect, WardrobeSlotLabels[i], lobbyCaptionCentered))
                    wardrobeSlot = WardrobeSlots[i];
            }

            if (manager == null || database == null)
            {
                GUI.Label(new Rect(rect.x + 14f, rect.y + 130f, rect.width - 28f, 36f),
                    "GUARDA-ROUPA INDISPONIVEL", lobbyCentered);
                return;
            }

            var items = database.GetBySlot(wardrobeSlot);
            var y = rect.y + 128f;
            var availableHeight = Mathf.Max(32f, rect.yMax - y - 12f);
            var itemHeight = Mathf.Clamp((availableHeight - Mathf.Max(0, items.Count - 1) * 4f)
                / Mathf.Max(1, items.Count), 30f, 48f);
            for (var i = 0; i < items.Count && y + itemHeight <= rect.yMax - 6f; i++)
            {
                var item = items[i];
                var itemRect = new Rect(rect.x + 14f, y, rect.width - 28f, itemHeight);
                var equipped = manager.IsEquipped(item);
                Fill(itemRect, equipped ? new Color(0.95f, 0.55f, 0.08f, 0.24f)
                    : new Color(1f, 1f, 1f, 0.055f));
                Fill(new Rect(itemRect.x, itemRect.y, 4f, itemRect.height), equipped ? Amber : Cyan);
                if (item.Icon != null)
                    GUI.DrawTexture(new Rect(itemRect.x + 8f, itemRect.y + 3f, itemRect.height - 6f, itemRect.height - 6f),
                        item.Icon.texture, ScaleMode.ScaleToFit, true);
                var owned = manager.IsOwned(item);
                var state = equipped ? "EQUIPADO" : owned ? "EQUIPAR" : "BLOQUEADO";
                GUI.Label(new Rect(itemRect.x + itemRect.height + 6f, itemRect.y + 2f,
                        itemRect.width - itemRect.height - 79f, itemRect.height - 4f),
                    item.DisplayName.ToUpperInvariant(), lobbyCaption);
                GUI.Label(new Rect(itemRect.xMax - 76f, itemRect.y + 2f, 68f, itemRect.height - 4f),
                    state, lobbyCaptionCentered);
                if (!equipped && owned && GUI.Button(itemRect, GUIContent.none, lobbyButton))
                {
                    if (manager.Equip(item)) SetLobbyFeedback($"{item.DisplayName} equipado.");
                }
                y += itemHeight + 4f;
            }

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0
                && !rect.Contains(currentEvent.mousePosition) && match.LobbyCharacterRoot != null)
            {
                match.LobbyCharacterRoot.Rotate(0f,
                    CharacterPreviewRotator.RotationDelta(currentEvent.delta.x, 0.28f, false), 0f, Space.Self);
                currentEvent.Use();
            }
        }

        private const float MinimumDrawerHeight = 120f;

        private static string DrawerRowTitle(int drawer, int row)
        {
            if (drawer == 3) return row == 0 ? "SOBREVIVA 10 MIN" : row == 1 ? "COLETE 5 ITENS" : "ELIMINE 2 OPONENTES";
            if (drawer == 5) return row == 0 ? "RIFLES" : row == 1 ? "SMGS" : "PRECISAO";
            if (drawer == 6) return row == 0 ? "STEVE" : row == 1 ? "TRAJE TATICO" : "ANIMACOES";
            if (drawer == 7) return row == 0 ? "VISUAIS" : row == 1 ? "EMOTES" : "PARAQUEDAS";
            return row == 0 ? "DESTAQUE" : row == 1 ? "RECOMPENSA" : "EM BREVE";
        }

        private static string DrawerRowCaption(int drawer, int row)
        {
            if (drawer == 3) return row == 0 ? "0/10 minutos" : row == 1 ? "0/5 itens" : "0/2 eliminacoes";
            if (drawer == 5) return row == 0 ? "Vesper-9 disponivel na ilha" : row == 1 ? "Kairo SMG disponivel na ilha" : "Longveil disponivel na ilha";
            if (drawer == 6) return row == 0 ? "Operador equipado" : row == 1 ? "Conjunto atual" : "Pacote locomocao ativo";
            if (drawer == 7) return row == 0 ? "5 itens registrados" : row == 1 ? "3 gestos disponiveis" : "Modelo padrao equipado";
            return row == 2 ? "Conteudo online sera conectado" : "Previa local da temporada";
        }

        private void DrawLobbyTeamPanel(BattleLobbyRects layout)
        {
            FillCutPanel(layout.TeamPanel, new Color(0.015f, 0.03f, 0.04f, 0.76f), Cyan);
            GUI.Label(new Rect(layout.TeamPanel.x + 9f, layout.TeamPanel.y + 5f,
                layout.TeamPanel.width - 18f, 20f), $"EQUIPE  {match.TeamSize}/4", lobbyCaption);
            var compactSlots = layout.TeamSlots[0].width < 100f;
            for (var i = 0; i < layout.TeamSlots.Length; i++)
            {
                var slot = layout.TeamSlots[i];
                var activeSlot = i < match.TeamSize;
                Fill(slot, i == 0 ? new Color(0.1f, 0.82f, 0.95f, 0.16f)
                    : activeSlot ? new Color(1f, 1f, 1f, 0.07f)
                    : new Color(0f, 0f, 0f, 0.2f));
                if (i == 0)
                {
                    var avatarSize = Mathf.Min(34f, slot.width * 0.35f);
                    var avatar = new Rect(slot.x + 5f, slot.y + 5f, avatarSize, slot.height - 10f);
                    Fill(avatar, new Color(0.12f, 0.55f, 0.64f, 0.9f));
                    DrawLobbyIcon(new Rect(avatar.x + 5f, avatar.y + 5f,
                        avatar.width - 10f, avatar.height - 10f), LobbyIconId.Operator);
                    GUI.Label(new Rect(avatar.xMax + 5f, slot.y, slot.xMax - avatar.xMax - 8f,
                            slot.height), compactSlots ? "OK" : "RAVEN UNIT\nPRONTO", lobbyCentered);
                    continue;
                }
                var inviteIcon = new Rect(slot.x + 8f, slot.center.y - 12f, 24f, 24f);
                DrawLobbyIcon(inviteIcon, LobbyIconId.Invite);
                var label = activeSlot ? "       CONVIDAR" : "       BLOQUEADO";
                if (GUI.Button(slot, compactSlots ? string.Empty : label, lobbyButton) && activeSlot)
                    SetLobbyFeedback("Convites online ainda nao estao conectados.");
            }
        }

        private void DrawLobbyMatchDock(BattleLobbyRects layout)
        {
            FillCutPanel(layout.MatchDock, new Color(0.012f, 0.028f, 0.038f, 0.88f), Cyan);
            Fill(layout.MapSummary, new Color(0.08f, 0.16f, 0.19f, 0.92f));
            Fill(new Rect(layout.MapSummary.x, layout.MapSummary.y,
                layout.MapSummary.width, 3f), Amber);
            DrawLobbyIcon(new Rect(layout.MapSummary.x + 8f, layout.MapSummary.y + 9f, 20f, 20f),
                LobbyIconId.Map);
            GUI.Label(new Rect(layout.MapSummary.x + 33f, layout.MapSummary.y + 4f,
                layout.MapSummary.width - 39f, 28f), "BERMUDA", lobbySectionTitle);
            GUI.Label(new Rect(layout.MapSummary.x + 10f, layout.MapSummary.y + 34f,
                layout.MapSummary.width - 20f, Mathf.Max(16f, layout.MapSummary.height - 27f)),
                $"ILHA  |  {match.TeamSize * match.TeamCount} JOGADORES", lobbyCaption);

            var playlistLabel = match.Playlist == BRPlaylist.Ranked ? "RANQUEADA   <  >" : "CASUAL   <  >";
            if (DrawSegment(layout.PlaylistSelector, playlistLabel, true))
                match.SetPlaylist(match.Playlist == BRPlaylist.Ranked ? BRPlaylist.Casual : BRPlaylist.Ranked);
            var modeIndex = System.Array.IndexOf(LobbyModes, match.Mode);
            if (DrawSegment(layout.TeamSizeSelector, $"{LobbyModeLabels[Mathf.Max(0, modeIndex)]}   <  >", true))
                match.SetMode(LobbyModes[(Mathf.Max(0, modeIndex) + 1) % LobbyModes.Length]);
            if (!editingHud && LobbyButton(layout.PlayButton,
                match.Playlist == BRPlaylist.Ranked ? "JOGAR\nRANQUEADA" : "JOGAR", Amber,
                lobbyPrimaryButton)) match.StartMatch();
        }

        private void DrawLobbySettingsOverlay(Rect safe)
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.46f));
            var width = Mathf.Min(370f, safe.width - 24f);
            var height = Mathf.Min(292f, safe.height - 24f);
            var panel = new Rect(safe.center.x - width * 0.5f, safe.center.y - height * 0.5f,
                width, height);
            Fill(panel, new Color(0.025f, 0.035f, 0.04f, 0.99f));
            Fill(new Rect(panel.x, panel.y, panel.width, 3f), Cyan);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, panel.width - 36f, 28f),
                "CONFIGURACOES", medium);
            var rowWidth = (panel.width - 48f) * 0.5f;
            var settings = new MatchSettingsRects(
                new Rect(panel.x + 18f, panel.y + 48f, rowWidth, 44f),
                new Rect(panel.center.x + 6f, panel.y + 48f, rowWidth, 44f),
                new Rect(panel.x + 18f, panel.y + 108f, rowWidth, 44f),
                new Rect(panel.center.x + 6f, panel.y + 108f, rowWidth, 44f),
                default, default, default);
            DrawLobbySettings(settings);
            if (GUI.Button(new Rect(panel.x + 18f, panel.yMax - 52f, panel.width - 36f, 38f),
                "FECHAR")) showLobbySettings = false;
        }

        private void SetLobbyFeedback(string message)
        {
            lobbyFeedback = message;
            lobbyFeedbackUntil = Time.unscaledTime + 2.8f;
        }

        private void DrawStagingStatus()
        {
            var safe = GuiSafeArea;
            var rect = MatchAuxiliaryLayout.StagingStatus(safe);
            Fill(rect, Panel);
            GUI.Label(new Rect(rect.x, rect.y + 8f, rect.width, 24f), "AREA DE ESPERA", centered);
            GUI.Label(new Rect(rect.x, rect.y + 35f, rect.width, 26f),
                MatchHudText.StagingStatus(match.Participants.Count, match.TeamSize * match.TeamCount,
                    match.TeamCount, match.PhaseTimeRemaining), centered);
        }

        private void DrawVitals(BRParticipant player)
        {
            var rect = HudRect(HUDElementId.Vitals);
            Fill(rect, Panel);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, 150f, 20f), "RAVEN UNIT", medium);
            GUI.Label(new Rect(rect.xMax - 82f, rect.y + 9f, 70f, 18f), $"K {player?.Eliminations ?? 0}", small);
            var health = player != null ? player.Health.Health : 0f;
            var healthRatio = player != null ? health / player.Health.MaxHealth : 0f;
            DrawBar(new Rect(rect.x + 12f, rect.y + 35f, 226f, 14f), healthRatio, new Color(0.17f, 0.82f, 0.38f), $"HP {health:0}");
            if (player != null && player.IsDowned)
                DrawBar(new Rect(rect.x + 12f, rect.y + 57f, 226f, 10f), player.Health.BleedoutRatio,
                    Amber, $"SANGRAMENTO {player.Health.BleedoutRemaining:0}s");
            else
            {
                var armor = player != null ? Mathf.Max(player.Health.VestLevel, player.Health.HelmetLevel) / 4f : 0f;
                DrawBar(new Rect(rect.x + 12f, rect.y + 57f, 150f, 10f), armor, Cyan, $"ARMOR {(player != null ? player.Health.VestLevel : 0)}");
                GUI.Label(new Rect(rect.x + 171f, rect.y + 53f, 68f, 20f), $"MED {player?.Inventory.MedKits ?? 0}", small);
            }
            if (player != null && player.Inventory.UsingMedKit)
            {
                var healRect = new Rect(rect.x, rect.yMax + 6f, rect.width, 24f);
                Fill(healRect, Panel);
                DrawBar(new Rect(healRect.x + 8f, healRect.y + 7f, healRect.width - 16f, 10f),
                    player.Inventory.HealProgress, new Color(0.18f, 0.86f, 0.42f), "USANDO KIT MEDICO");
            }
        }

        private void DrawTeamRoster(BRParticipant player)
        {
            if (player == null || match.TeamSize <= 1) return;
            var teammates = PopulateTeamRosterBuffer(teamRosterBuffer, player, match.Participants);
            if (teammates.Count == 0) return;
            var safe = GuiSafeArea;
            var rect = MatchLobbyLayout.TeamRoster(safe, MobileRuntime, teammates.Count);
            Fill(rect, new Color(0.025f, 0.04f, 0.05f, 0.86f));
            for (var i = 0; i < teammates.Count; i++)
            {
                var teammate = teammates[i];
                var row = new Rect(rect.x + 6f, rect.y + 4f + i * 32f, rect.width - 12f, 28f);
                var state = TeamStateLabel(teammate);
                var healthRatio = teammate.Health != null
                    ? teammate.Health.Health / Mathf.Max(1f, teammate.Health.MaxHealth) : 0f;
                Fill(row, new Color(1f, 1f, 1f, 0.06f));
                Fill(new Rect(row.x, row.y, 3f, row.height), state == "ATIVO" ? Cyan
                    : state == "DERRUBADO" ? Amber : new Color(0.55f, 0.58f, 0.62f));
                var distance = Vector3.Distance(player.transform.position, teammate.transform.position);
                var bleedout = teammate.Health != null ? teammate.Health.BleedoutRemaining : 0f;
                GUI.Label(new Rect(row.x + 8f, row.y + 1f, row.width - 16f, 17f),
                    MatchHudText.TeamRosterLine(teammate.DisplayName, state, distance, bleedout), small);
                DrawBar(new Rect(row.x + 8f, row.y + 21f, row.width - 16f, 4f), healthRatio,
                    state == "ATIVO" ? Cyan : Amber, string.Empty);
            }
        }

        public static List<BRParticipant> PopulateTeamRosterBuffer(List<BRParticipant> buffer,
            BRParticipant player, IReadOnlyList<BRParticipant> participants)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            buffer.Clear();
            if (player == null || participants == null) return buffer;
            foreach (var participant in participants)
                if (participant != null && participant != player && participant.TeamId == player.TeamId)
                    buffer.Add(participant);
            return buffer;
        }

        public static string TeamStateLabel(BRParticipant participant)
        {
            if (participant == null || participant.IsEliminated
                || participant.Phase == ParticipantPhase.Eliminated) return "ELIMINADO";
            return participant.IsDowned || participant.Phase == ParticipantPhase.Downed
                ? "DERRUBADO" : "ATIVO";
        }

        private void DrawMatchStatus()
        {
            var rect = HudRect(HUDElementId.MatchStatus);
            Fill(rect, Panel);
            if (match.State == MatchState.Plane)
            {
                GUI.Label(new Rect(rect.x, rect.y + 5f, rect.width, 20f), $"{match.PassengersRemaining} NO AVIAO", centered);
                GUI.Label(new Rect(rect.x, rect.y + 27f, rect.width, 18f),
                    $"ROTA  |  {match.AliveTeamCount} EQUIPES", centered);
            }
            else
            {
                GUI.Label(new Rect(rect.x, rect.y + 5f, rect.width, 20f),
                    $"{match.AliveCount} VIVOS  |  {match.AliveTeamCount} EQUIPES", centered);
                var zone = match.SafeZone;
                var phase = zone.CurrentState switch
                {
                    SafeZoneState.Shrinking => "ZONA DIMINUINDO",
                    SafeZoneState.Finished => "ZONA FINAL",
                    _ => "ZONA DIMINUIRA EM"
                };
                var seconds = Mathf.CeilToInt(zone.StateTimeRemaining);
                GUI.Label(new Rect(rect.x, rect.y + 27f, rect.width, 18f),
                    $"{phase}  {seconds / 60:00}:{seconds % 60:00}", centered);
            }
        }

        private void DrawSafeZoneAlert(BRParticipant player)
        {
            var zone = match?.SafeZone;
            if (player == null || zone == null || zone.CurrentState == SafeZoneState.Inactive
                || zone.IsInsideSafeZone(player.transform.position)) return;
            var safe = GuiSafeArea;
            var width = Mathf.Min(330f, safe.width - 24f);
            var rect = new Rect(safe.center.x - width * 0.5f, safe.y + safe.height * 0.18f, width, 48f);
            Fill(rect, new Color(0.36f, 0.025f, 0.02f, 0.9f));
            Fill(new Rect(rect.x, rect.y, 5f, rect.height), new Color(1f, 0.24f, 0.08f, 1f));
            GUI.Label(new Rect(rect.x + 12f, rect.y + 2f, rect.width - 20f, 23f),
                "FORA DA ZONA SEGURA", centered);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 24f, rect.width - 20f, 20f),
                $"SAFE {Mathf.CeilToInt(zone.GetDistanceToSafeZone(player.transform.position))} m", centered);
        }

        private void DrawCompass()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var heading = Mathf.Repeat(camera.transform.eulerAngles.y, 360f);
            var cardinal = heading switch
            {
                >= 337.5f or < 22.5f => "N",
                < 67.5f => "NE",
                < 112.5f => "E",
                < 157.5f => "SE",
                < 202.5f => "S",
                < 247.5f => "SW",
                < 292.5f => "W",
                _ => "NW"
            };
            var rect = HudRect(HUDElementId.Compass);
            Fill(rect, new Color(0.02f, 0.03f, 0.04f, 0.72f));
            Fill(new Rect(rect.center.x - 1f, rect.y, 2f, 5f), Amber);
            GUI.Label(rect, $"{cardinal}   {heading:000}°", centered);
        }

        private void DrawWeapon(BRParticipant player)
        {
            var rect = HudRect(HUDElementId.Weapon);
            Fill(rect, new Color(0.025f, 0.032f, 0.040f, 0.91f));
            DrawOutline(rect, new Color(1f, 1f, 1f, 0.16f), 1f);
            var weapon = player != null ? player.Inventory.ActiveWeapon : null;
            var header = new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 22f);
            if (weapon != null)
            {
                var reserve = player.Inventory.AmmoFor(weapon.ammoKind);
                GUI.Label(header, weapon.displayName.ToUpperInvariant(), medium);
                GUI.Label(new Rect(header.xMax - 104f, header.y, 104f, header.height),
                    $"{player.Weapon.Magazine:00} / {reserve:000}", centered);
                if (player.Weapon.Reloading)
                    DrawBar(new Rect(header.x, header.yMax - 3f, header.width, 3f),
                        player.Weapon.ReloadProgress, Amber, string.Empty);
            }
            else
            {
                GUI.Label(header, "DESARMADO", medium);
                GUI.Label(new Rect(header.xMax - 116f, header.y, 116f, header.height), "COLETE NO CHAO", small);
            }
            DrawWeaponSlots(rect, player);
            DrawCarriedLootSlots(rect, player);
        }

        private void DrawWeaponSlots(Rect rect, BRParticipant player)
        {
            var weapons = player != null ? player.Inventory.Weapons : null;
            var gap = 6f;
            var slotWidth = (rect.width - 16f - gap) * 0.5f;
            for (var i = 0; i < 2; i++)
            {
                var slot = new Rect(rect.x + 8f + i * (slotWidth + gap), rect.y + 30f, slotWidth, 50f);
                var slotWeapon = weapons != null && i < weapons.Count ? weapons[i] : null;
                var active = slotWeapon != null && slotWeapon == player.Inventory.ActiveWeapon;
                Fill(slot, active ? new Color(0.44f, 0.10f, 0.08f, 0.92f) : new Color(1f, 1f, 1f, 0.07f));
                DrawOutline(slot, active ? Amber : new Color(1f, 1f, 1f, 0.14f), active ? 2f : 1f);
                if (slotWeapon != null)
                {
                    DrawWeaponIcon(new Rect(slot.x + 7f, slot.y + 4f, slot.width - 14f, 28f),
                        slotWeapon.weaponClass, active ? Color.white : new Color(0.80f, 0.84f, 0.86f));
                    var magazine = player.Weapon.MagazineFor(slotWeapon);
                    var reserve = player.Inventory.AmmoFor(slotWeapon.ammoKind);
                    GUI.Label(new Rect(slot.x + 6f, slot.yMax - 18f, slot.width - 12f, 16f),
                        $"{i + 1}   {magazine:00} / {reserve:000}", small);
                }
                else
                {
                    DrawFistIcon(new Rect(slot.center.x - 15f, slot.y + 7f, 30f, 25f),
                        new Color(1f, 1f, 1f, 0.34f));
                    GUI.Label(new Rect(slot.x + 6f, slot.yMax - 18f, slot.width - 12f, 16f),
                        $"{i + 1}   VAZIO", small);
                }
                if (GUI.Button(slot, GUIContent.none, GUIStyle.none) && slotWeapon != null)
                    player.Inventory.SelectWeapon(i);
            }
        }

        private void DrawCarriedLootSlots(Rect rect, BRParticipant player)
        {
            if (player == null) return;
            var row = new Rect(rect.x + 8f, rect.y + 84f, rect.width - 16f, Mathf.Max(32f, rect.height - 92f));
            var gap = 4f;
            var width = (row.width - gap * 3f) * 0.25f;
            var active = player.Inventory.ActiveWeapon;
            DrawInventoryItem(new Rect(row.x, row.y, width, row.height), CombatItemIcon.Ammo,
                active != null ? player.Inventory.AmmoFor(active.ammoKind).ToString() : "0", "MUN", Cyan);
            DrawInventoryItem(new Rect(row.x + (width + gap), row.y, width, row.height), CombatItemIcon.MedKit,
                player.Inventory.MedKits.ToString(), "KIT", new Color(0.24f, 0.95f, 0.48f));
            DrawInventoryItem(new Rect(row.x + (width + gap) * 2f, row.y, width, row.height), CombatItemIcon.Armor,
                Mathf.Max(player.Health.VestLevel, player.Health.HelmetLevel).ToString(), "PROT", Cyan);
            DrawInventoryItem(new Rect(row.x + (width + gap) * 3f, row.y, width, row.height), CombatItemIcon.Attachment,
                player.Inventory.StabilizerLevel.ToString(), "MOD", new Color(0.76f, 0.48f, 1f));
        }

        private void DrawInventoryItem(Rect rect, CombatItemIcon icon, string amount, string label, Color accent)
        {
            Fill(rect, new Color(0.08f, 0.10f, 0.12f, 0.94f));
            Fill(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), accent);
            DrawItemIcon(new Rect(rect.x + 4f, rect.y + 3f, rect.height - 8f, rect.height - 8f), icon, Color.white);
            GUI.Label(new Rect(rect.x + rect.height - 1f, rect.y + 1f,
                rect.width - rect.height - 3f, rect.height * 0.55f), amount, medium);
            GUI.Label(new Rect(rect.x + rect.height - 1f, rect.y + rect.height * 0.48f,
                rect.width - rect.height - 3f, rect.height * 0.40f), label, small);
        }

        private void DrawLootPrompt()
        {
            if (match.State != MatchState.Active) return;
            var revive = match.ReviveTarget != null ? match.ReviveTarget : match.NearbyReviveTarget;
            if (revive != null)
            {
                var reviveRect = HudRect(HUDElementId.LootPrompt);
                Fill(reviveRect, new Color(0.04f, 0.06f, 0.07f, 0.96f));
                Fill(new Rect(reviveRect.x, reviveRect.y, 5f, reviveRect.height), Cyan);
                GUI.Label(new Rect(reviveRect.x + 16f, reviveRect.y + 5f, reviveRect.width - 30f, 20f),
                    $"REVIVER {revive.DisplayName.ToUpperInvariant()}", medium);
                var prompt = MobileRuntime ? "SEGURE USE" : "SEGURE E";
                GUI.Label(new Rect(reviveRect.x + 16f, reviveRect.y + 27f, 104f, 17f), prompt, small);
                if (match.ReviveTarget != null)
                    DrawBar(new Rect(reviveRect.x + 124f, reviveRect.y + 32f, reviveRect.width - 142f, 7f),
                        match.ReviveProgress, Cyan, string.Empty);
                return;
            }
            var nearbyVehicle = match.NearbyVehicle;
            if (nearbyVehicle != null)
            {
                var vehicleRect = HudRect(HUDElementId.LootPrompt);
                Fill(vehicleRect, new Color(0.035f, 0.06f, 0.07f, 0.96f));
                Fill(new Rect(vehicleRect.x, vehicleRect.y, 5f, vehicleRect.height), Cyan);
                GUI.Label(new Rect(vehicleRect.x + 16f, vehicleRect.y + 5f, vehicleRect.width - 110f, 22f),
                    "VEICULO", medium);
                GUI.Label(new Rect(vehicleRect.x + 16f, vehicleRect.y + 28f, vehicleRect.width - 110f, 18f),
                    MobileRuntime ? "USE  ENTRAR" : "F  ENTRAR", small);
                if (GUI.Button(new Rect(vehicleRect.xMax - 82f, vehicleRect.y + 10f, 68f,
                        vehicleRect.height - 20f), "ENTRAR")) match.TryInteractPlayer();
                return;
            }
            var nearbyShop = match.NearbyMatchShop;
            if (nearbyShop != null && (match.MatchShop == null || !match.MatchShop.IsShopOpen))
            {
                var shopRect = HudRect(HUDElementId.LootPrompt);
                Fill(shopRect, new Color(0.035f, 0.06f, 0.07f, 0.96f));
                Fill(new Rect(shopRect.x, shopRect.y, 5f, shopRect.height), Amber);
                DrawLobbyIcon(new Rect(shopRect.x + 13f, shopRect.y + 10f, 34f, 34f), LobbyIconId.Store);
                GUI.Label(new Rect(shopRect.x + 56f, shopRect.y + 5f, shopRect.width - 145f, 22f),
                    nearbyShop.displayName, medium);
                GUI.Label(new Rect(shopRect.x + 56f, shopRect.y + 28f, shopRect.width - 145f, 18f),
                    MobileRuntime ? "USE  ABRIR LOJA" : "E  ABRIR LOJA", small);
                if (GUI.Button(new Rect(shopRect.xMax - 82f, shopRect.y + 10f, 68f,
                        shopRect.height - 20f), "ABRIR")) match.TryInteractPlayer();
                return;
            }
            var deathLoot = match.NearbyDeathLoot;
            if (deathLoot != null && match.ActiveDeathLoot == null)
            {
                var deathRect = HudRect(HUDElementId.LootPrompt);
                Fill(deathRect, new Color(0.04f, 0.06f, 0.07f, 0.96f));
                Fill(new Rect(deathRect.x, deathRect.y, 5f, deathRect.height), Amber);
                GUI.Label(new Rect(deathRect.x + 16f, deathRect.y + 7f, 290f, 20f), $"CAIXA DE {deathLoot.OwnerName.ToUpperInvariant()}", medium);
                var openLabel = MobileRuntime ? "USE  ABRIR E ESCOLHER ITENS" : "E  ABRIR E ESCOLHER ITENS";
                GUI.Label(new Rect(deathRect.x + 16f, deathRect.y + 31f, 290f, 18f), openLabel, small);
                return;
            }
            var loot = match.NearbyPlayerLoot;
            if (loot == null) return;
            var rect = HudRect(HUDElementId.LootPrompt);
            Fill(rect, new Color(0.04f, 0.06f, 0.07f, 0.94f));
            Fill(new Rect(rect.x, rect.y, 5f, rect.height), LootColor(loot.Kind));
            DrawLootIcon(new Rect(rect.x + 12f, rect.y + 8f, 38f, 38f), loot.Kind,
                loot.Weapon != null ? loot.Weapon.weaponClass : WeaponClass.Rifle, Color.white);
            GUI.Label(new Rect(rect.x + 58f, rect.y + 5f, rect.width - 150f, 22f),
                loot.DisplayName.ToUpperInvariant(), medium);
            var collectLabel = MobileRuntime ? "USE  COLETAR" : "E  COLETAR";
            GUI.Label(new Rect(rect.x + 58f, rect.y + 28f, rect.width - 150f, 17f), collectLabel, small);
            var collectButton = new Rect(rect.xMax - 82f, rect.y + 10f, 68f, rect.height - 20f);
            if (GUI.Button(collectButton, "PEGAR")) match.TryInteractPlayer();
        }

        private void DrawMatchCurrency()
        {
            var wallet = match.PlayerWallet;
            if (wallet == null) return;
            var safe = GuiSafeArea;
            var rect = new Rect(safe.xMax - 194f, safe.yMin + 202f, 176f, 38f);
            DrawCurrency(rect, LobbyIconId.Coin, wallet.CurrentCurrency.ToString(), Amber);
        }

        private void DrawVehicleHUD()
        {
            var vehicle = match.ActiveVehicle;
            if (vehicle == null) return;
            var controller = vehicle.GetComponent<VehicleController>();
            var health = vehicle.GetComponent<VehicleHealth>();
            var fuel = vehicle.GetComponent<VehicleFuel>();
            var seat = vehicle.GetSeat(match.Player);
            var safe = GuiSafeArea;
            var rect = new Rect(safe.xMax - 228f, safe.yMax - 132f, 210f, 104f);
            Fill(rect, new Color(0.025f, 0.04f, 0.05f, 0.94f));
            Fill(new Rect(rect.x, rect.y, 5f, rect.height), Cyan);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 22f),
                seat?.IsDriver == true ? "MOTORISTA" : "PASSAGEIRO", medium);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 31f, rect.width - 28f, 29f),
                $"{Mathf.RoundToInt(controller != null ? controller.CurrentSpeedKmh : 0f):000} KM/H", lobbySectionTitle);
            DrawBar(new Rect(rect.x + 14f, rect.y + 66f, rect.width - 28f, 7f),
                health != null ? health.NormalizedHealth : 0f, new Color(0.18f, 0.85f, 0.35f), string.Empty);
            DrawBar(new Rect(rect.x + 14f, rect.y + 82f, rect.width - 28f, 6f),
                fuel != null ? fuel.NormalizedFuel : 1f, Amber, string.Empty);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 89f, rect.width - 28f, 13f),
                MobileRuntime ? "USE SAIR" : "F SAIR  H BUZINA  C CAMERA  R VIRAR", small);
        }

        private void DrawVehicleMobileControls()
        {
            DrawMobileControls();
            VehicleControlLabel(MobileControlLayout.ToGui(MobileControlLayout.Fire), "ACEL");
            VehicleControlLabel(MobileControlLayout.ToGui(MobileControlLayout.Aim), "RE");
            VehicleControlLabel(MobileControlLayout.ToGui(MobileControlLayout.Jump), "FREIO");
            VehicleControlLabel(MobileControlLayout.ToGui(MobileControlLayout.Interact), "SAIR");
            VehicleControlLabel(MobileControlLayout.ToGui(MobileControlLayout.Reload), "BUZ");
            VehicleControlLabel(MobileControlLayout.ToGui(MobileControlLayout.Swap), "CAM");
        }

        private void VehicleControlLabel(Rect rect, string label)
        {
            GUI.Label(new Rect(rect.x, rect.yMax - 17f, rect.width, 16f), label, lobbyCaptionCentered);
        }

        private void DrawMatchShopPanel()
        {
            var controller = match.MatchShop;
            var terminal = controller?.ActiveTerminal;
            if (controller == null || terminal == null) return;
            var safe = GuiSafeArea;
            var width = Mathf.Min(760f, safe.width - 24f);
            var height = Mathf.Min(500f, safe.height - 28f);
            var panelRect = new Rect(safe.center.x - width * 0.5f, safe.center.y - height * 0.5f, width, height);
            Fill(panelRect, new Color(0.018f, 0.028f, 0.034f, 0.985f));
            DrawOutline(panelRect, new Color(0.12f, 0.82f, 0.94f, 0.72f), 2f);
            Fill(new Rect(panelRect.x, panelRect.y, panelRect.width, 48f), new Color(0.045f, 0.075f, 0.088f, 1f));
            DrawLobbyIcon(new Rect(panelRect.x + 14f, panelRect.y + 9f, 30f, 30f), LobbyIconId.Store);
            GUI.Label(new Rect(panelRect.x + 52f, panelRect.y + 5f, panelRect.width - 250f, 38f),
                terminal.displayName, large);
            DrawLobbyIcon(new Rect(panelRect.xMax - 178f, panelRect.y + 12f, 24f, 24f), LobbyIconId.Coin);
            GUI.Label(new Rect(panelRect.xMax - 148f, panelRect.y + 5f, 92f, 38f),
                (match.PlayerWallet?.CurrentCurrency ?? 0).ToString(), centered);
            if (GUI.Button(new Rect(panelRect.xMax - 43f, panelRect.y + 9f, 30f, 30f), "X"))
            {
                controller.CloseShop();
                return;
            }

            var categoriesY = panelRect.y + 56f;
            var categoryWidth = (panelRect.width - 24f) / MatchShopCategories.Length;
            for (var i = 0; i < MatchShopCategories.Length; i++)
            {
                var category = MatchShopCategories[i];
                var rect = new Rect(panelRect.x + 12f + categoryWidth * i, categoriesY,
                    categoryWidth - 3f, 34f);
                Fill(rect, controller.ActiveCategory == category
                    ? new Color(0.08f, 0.62f, 0.72f, 0.95f)
                    : new Color(1f, 1f, 1f, 0.07f));
                if (GUI.Button(rect, MatchShopCategoryLabels[i], small)) controller.ShowCategory(category);
            }

            var listRect = new Rect(panelRect.x + 12f, categoriesY + 42f, panelRect.width - 24f,
                panelRect.height - 132f);
            var count = controller.CategoryItems.Count;
            var content = new Rect(0f, 0f, listRect.width - 18f, Mathf.Max(listRect.height, count * 68f));
            matchShopScroll = GUI.BeginScrollView(listRect, matchShopScroll, content, false,
                content.height > listRect.height);
            for (var i = 0; i < count; i++)
            {
                var item = controller.CategoryItems[i];
                if (item == null) continue;
                DrawMatchShopProduct(new Rect(2f, i * 68f, content.width - 6f, 60f), item, controller, terminal);
            }
            GUI.EndScrollView();

            if (!string.IsNullOrEmpty(controller.LastFeedback)
                && Time.unscaledTime < controller.FeedbackVisibleUntil)
            {
                var success = controller.LastResult == PurchaseResult.Success;
                Fill(new Rect(panelRect.x + 12f, panelRect.yMax - 34f, panelRect.width - 24f, 25f),
                    success ? new Color(0.04f, 0.42f, 0.24f, 0.95f) : new Color(0.48f, 0.08f, 0.04f, 0.95f));
                GUI.Label(new Rect(panelRect.x + 18f, panelRect.yMax - 34f, panelRect.width - 36f, 25f),
                    controller.LastFeedback, centered);
            }
        }

        private void DrawMatchShopProduct(Rect rect, MatchShopItemData item, MatchShopController controller,
            MatchShopTerminal terminal)
        {
            var stock = terminal.GetRemainingStock(item, match.Player);
            var soldOut = !terminal.HasStock(item, match.Player);
            var affordable = match.PlayerWallet != null && match.PlayerWallet.CanAfford(controller.GetFinalPrice(item));
            Fill(rect, new Color(1f, 1f, 1f, 0.065f));
            Fill(new Rect(rect.x, rect.y, 4f, rect.height), soldOut ? Color.gray : affordable ? Cyan : Amber);
            var iconRect = new Rect(rect.x + 12f, rect.y + 8f, 46f, 44f);
            DrawMatchShopProductIcon(iconRect, item);
            GUI.Label(new Rect(rect.x + 70f, rect.y + 5f, rect.width - 270f, 24f),
                item.displayName.ToUpperInvariant(), medium);
            var detail = item.quantityGranted > 1 ? $"x{item.quantityGranted}" : item.category.ToString().ToUpperInvariant();
            if (stock != int.MaxValue) detail += $"  |  ESTOQUE {stock}";
            GUI.Label(new Rect(rect.x + 70f, rect.y + 31f, rect.width - 270f, 20f), detail, small);
            DrawLobbyIcon(new Rect(rect.xMax - 190f, rect.y + 17f, 22f, 22f), LobbyIconId.Coin);
            GUI.Label(new Rect(rect.xMax - 164f, rect.y + 10f, 72f, 36f), item.price.ToString(), centered);
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = soldOut ? Color.gray : affordable ? new Color(0.08f, 0.72f, 0.48f) : Amber;
            var buttonLabel = soldOut ? "ESGOTADO"
                : controller.IsAwaitingConfirmation(item) ? "CONFIRMAR" : "COMPRAR";
            if (GUI.Button(new Rect(rect.xMax - 86f, rect.y + 11f, 74f, 38f), buttonLabel))
                controller.RequestPurchase(item);
            GUI.backgroundColor = previous;
        }

        private void DrawMatchShopProductIcon(Rect rect, MatchShopItemData item)
        {
            if (item.productType == MatchShopProductType.Weapon)
            {
                DrawWeaponIcon(rect, item.weapon != null ? item.weapon.weaponClass : WeaponClass.Rifle, Color.white);
                return;
            }
            if (item.productType is MatchShopProductType.MedKit)
                DrawItemIcon(rect, CombatItemIcon.MedKit, Color.white);
            else if (item.productType is MatchShopProductType.Vest or MatchShopProductType.Helmet)
                DrawItemIcon(rect, item.productType == MatchShopProductType.Helmet
                    ? CombatItemIcon.Helmet : CombatItemIcon.Armor, Color.white);
            else if (item.productType == MatchShopProductType.Ammo)
                DrawItemIcon(rect, CombatItemIcon.Ammo, Color.white);
            else if (item.productType == MatchShopProductType.Stabilizer)
                DrawItemIcon(rect, CombatItemIcon.Attachment, Color.white);
            else DrawHudIcon(rect, HudIconId.Interact);
        }

        private void DrawPickupFeedback()
        {
            if (!match.PickupFeedbackVisible) return;
            var rect = HudRect(HUDElementId.PickupFeedback);
            Fill(rect, new Color(0.02f, 0.04f, 0.05f, 0.82f));
            Fill(new Rect(rect.x, rect.y, 4f, rect.height), Cyan);
            GUI.Label(rect, match.PickupFeedback.ToUpperInvariant(), centered);
        }

        private void DrawDeathLootPanel()
        {
            var container = match.ActiveDeathLoot;
            if (container == null || !container.IsOpen) return;
            var count = container.Entries.Count;
            var safe = GuiSafeArea;
            var rect = MatchAuxiliaryLayout.DeathLootPanel(safe, MobileRuntime, count);
            Fill(rect, new Color(0.025f, 0.035f, 0.045f, 0.97f));
            Fill(new Rect(rect.x, rect.y, 5f, rect.height), Amber);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 10f, rect.width - 76f, 24f), $"LOOT DE {container.OwnerName.ToUpperInvariant()}", medium);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 34f, rect.width - 36f, 18f), "ESCOLHA O QUE DESEJA PEGAR", small);
            if (GUI.Button(new Rect(rect.xMax - 44f, rect.y + 10f, 30f, 28f), "X"))
            {
                match.CloseActiveDeathLoot();
                return;
            }
            if (GUI.Button(new Rect(rect.x + 18f, rect.y + 56f, rect.width - 36f, 30f), "PEGAR TUDO"))
            {
                match.TakeAllDeathLoot();
                return;
            }
            var scrollRect = new Rect(rect.x + 12f, rect.y + 94f, rect.width - 24f, rect.height - 104f);
            var viewRect = new Rect(0f, 0f, scrollRect.width - 18f, Mathf.Max(scrollRect.height, count * 42f));
            deathLootScroll = GUI.BeginScrollView(scrollRect, deathLootScroll, viewRect, false, count * 42f > scrollRect.height);
            for (var i = 0; i < count; i++)
            {
                var entry = container.Entries[i];
                var entryId = entry.EntryId;
                var row = new Rect(4f, i * 42f, viewRect.width - 8f, 34f);
                var accent = entry.Kind == DeathLootKind.Weapon ? Amber : new Color(0.24f, 0.72f, 0.82f);
                Fill(row, new Color(1f, 1f, 1f, 0.07f));
                Fill(new Rect(row.x, row.y, 3f, row.height), accent);
                DrawDeathLootIcon(new Rect(row.x + 8f, row.y + 4f, 27f, 27f), entry, Color.white);
                GUI.Label(new Rect(row.x + 42f, row.y + 2f, row.width - 118f, row.height - 4f),
                    entry.DisplayName.ToUpperInvariant(), small);
                var takeRect = new Rect(row.xMax - 68f, row.y + 4f, 62f, row.height - 8f);
                var take = GUI.Button(takeRect, "PEGAR");
                if (!take) continue;
                match.TakeDeathLootEntryById(entryId);
                break;
            }
            GUI.EndScrollView();
        }

        private void DrawMinimap()
        {
            var rect = HudRect(HUDElementId.Minimap);
            Fill(rect, new Color(0.025f, 0.035f, 0.04f, 0.94f));
            var inner = new Rect(rect.x + 8f, rect.y + 8f, Mathf.Max(8f, rect.width - 16f), Mathf.Max(8f, rect.height - 16f));
            if (minimapTexture != null) GUI.DrawTexture(inner, minimapTexture, ScaleMode.StretchToFill, false);
            else Fill(inner, new Color(0.12f, 0.19f, 0.14f, 1f));
            if (match.State == MatchState.Plane)
            {
                var start = MapPoint(inner, match.Drop.PlaneStart);
                var end = MapPoint(inner, match.Drop.PlaneEnd);
                DrawLine(start, end, new Color(1f, 1f, 1f, 0.65f), 2f);
                var plane = MapPoint(inner, match.Drop.PlanePosition);
                Fill(new Rect(plane.x - 5f, plane.y - 5f, 10f, 10f), Amber);
            }
            else
            {
                if (match.SafeZone.HasNextZone)
                    DrawZoneOnMap(inner, match.SafeZone.NextCenter, match.SafeZone.NextRadius,
                        new Color(1f, 0.72f, 0.15f, 0.72f));
                DrawZoneOnMap(inner, match.SafeZone.CurrentCenter, match.SafeZone.CurrentRadius, new Color(0.1f, 0.86f, 1f, 0.9f));
            }
            foreach (var participant in match.Participants)
            {
                if (participant == null || participant.Health.IsDead || participant.Phase == ParticipantPhase.WaitingPlane) continue;
                var player = match.Player;
                var teammate = player != null && participant.TeamId == player.TeamId;
                var secondsSinceShot = Time.time - participant.Weapon.LastShotTime;
                var sqrDistance = player != null
                    ? (participant.transform.position - player.transform.position).sqrMagnitude
                    : float.PositiveInfinity;
                if (!MinimapVisibilityPolicy.ShouldShow(participant.IsPlayer, teammate,
                        secondsSinceShot, sqrDistance)) continue;
                var point = MapPoint(inner, participant.transform.position);
                var color = participant.IsPlayer ? Color.white : teammate ? Cyan : new Color(0.95f, 0.22f, 0.18f, 0.9f);
                var dot = participant.IsPlayer ? 8f : teammate ? 6f : 4f;
                Fill(new Rect(point.x - dot * 0.5f, point.y - dot * 0.5f, dot, dot), color);
            }
            DrawAirdropMarkers(inner, false);
            GUI.Label(new Rect(rect.x + 7f, rect.yMax - 23f, 54f, 18f), "MAPA", small);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) showFullMap = true;
        }

        private void DrawFullMap()
        {
            var safe = GuiSafeArea;
            var side = Mathf.Min(safe.height * 0.86f, safe.width * 0.72f);
            var frame = new Rect(safe.center.x - side * 0.5f, safe.center.y - side * 0.5f, side, side);
            Fill(new Rect(safe.x, safe.y, safe.width, safe.height), new Color(0f, 0f, 0f, 0.62f));
            Fill(frame, new Color(0.025f, 0.035f, 0.04f, 0.98f));
            var map = new Rect(frame.x + 12f, frame.y + 38f, frame.width - 24f, frame.height - 50f);
            if (minimapTexture != null) GUI.DrawTexture(map, minimapTexture, ScaleMode.StretchToFill, false);
            else Fill(map, new Color(0.12f, 0.19f, 0.14f, 1f));

            if (match.SafeZone != null && match.SafeZone.CurrentState != SafeZoneState.Inactive)
            {
                if (match.SafeZone.HasNextZone)
                    DrawZoneOnMap(map, match.SafeZone.NextCenter, match.SafeZone.NextRadius,
                        new Color(1f, 0.72f, 0.15f, 0.86f));
                DrawZoneOnMap(map, match.SafeZone.CurrentCenter, match.SafeZone.CurrentRadius,
                    new Color(0.1f, 0.86f, 1f, 0.96f));
            }
            if (match.State == MatchState.Plane && match.Drop != null)
            {
                DrawLine(MapPoint(map, match.Drop.PlaneStart), MapPoint(map, match.Drop.PlaneEnd), Color.white, 2f);
                var plane = MapPoint(map, match.Drop.PlanePosition);
                Fill(new Rect(plane.x - 6f, plane.y - 6f, 12f, 12f), Amber);
            }
            foreach (var participant in match.Participants)
            {
                if (participant == null || participant.Health.IsDead
                    || participant.Phase == ParticipantPhase.WaitingPlane) continue;
                var player = match.Player;
                var teammate = player != null && participant.TeamId == player.TeamId;
                var secondsSinceShot = Time.time - participant.Weapon.LastShotTime;
                var sqrDistance = player != null
                    ? (participant.transform.position - player.transform.position).sqrMagnitude
                    : float.PositiveInfinity;
                if (!MinimapVisibilityPolicy.ShouldShow(participant.IsPlayer, teammate,
                        secondsSinceShot, sqrDistance)) continue;
                var point = MapPoint(map, participant.transform.position);
                var color = participant.IsPlayer ? Color.white : teammate ? Cyan : new Color(1f, 0.2f, 0.16f);
                Fill(new Rect(point.x - 4f, point.y - 4f, 8f, 8f), color);
            }
            DrawAirdropMarkers(map, true);
            GUI.Label(new Rect(frame.x + 14f, frame.y + 6f, frame.width - 70f, 26f),
                "MAPA TATICO", medium);
            if (GUI.Button(new Rect(frame.xMax - 44f, frame.y + 5f, 32f, 28f), "X")) showFullMap = false;
        }

        private void DrawAirdropMarkers(Rect map, bool labeled)
        {
            foreach (var crate in AirdropCrate.Active)
            {
                if (crate == null || !crate.ShowMapMarker) continue;
                var point = MapPoint(map, crate.transform.position);
                var size = labeled ? 12f : 8f;
                Fill(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), Amber);
                DrawOutline(new Rect(point.x - size * 0.7f, point.y - size * 0.7f,
                    size * 1.4f, size * 1.4f), Color.white, 1f);
                if (labeled)
                    GUI.Label(new Rect(point.x + 8f, point.y - 10f, 90f, 20f), "AIRDROP", small);
            }
        }

        private void DrawDeploymentTelemetry(BRParticipant player)
        {
            if (player == null) return;
            var safe = GuiSafeArea;
            var rect = MatchAuxiliaryLayout.DeploymentTelemetry(safe);
            Fill(rect, Panel);
            var phase = player.Phase switch
            {
                ParticipantPhase.WaitingPlane => "NO AVIAO",
                ParticipantPhase.Freefall => "QUEDA LIVRE",
                ParticipantPhase.Parachute => "PARAQUEDAS",
                _ => "POUSO"
            };
            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, 210f, 22f), phase, medium);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 34f, 210f, 20f), $"ALTITUDE {Mathf.Max(0f, player.transform.position.y):0} m", small);
            var mobile = MobileRuntime;
            var prompt = player.Phase == ParticipantPhase.WaitingPlane ? (mobile ? "DROP  SALTAR" : "SPACE  SALTAR")
                : player.Phase == ParticipantPhase.Freefall ? (mobile ? "DROP  ABRIR PARAQUEDAS" : "SPACE  ABRIR PARAQUEDAS")
                : mobile ? "MOVE  DIRECIONAR" : "WASD  DIRECIONAR";
            GUI.Label(new Rect(rect.x + 12f, rect.y + 58f, 220f, 18f), prompt, small);
        }

        private void DrawZoneOnMap(Rect mapRect, Vector3 center, float radius, Color tint)
        {
            var point = MapPoint(mapRect, center);
            var diameter = radius * 2f / match.MapSize.x * mapRect.width;
            var previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(new Rect(point.x - diameter * 0.5f, point.y - diameter * 0.5f, diameter, diameter), zoneRing);
            GUI.color = previous;
        }

        private Vector2 MapPoint(Rect mapRect, Vector3 world)
        {
            var x = Mathf.InverseLerp(-match.MapSize.x * 0.5f, match.MapSize.x * 0.5f, world.x);
            var y = Mathf.InverseLerp(-match.MapSize.y * 0.5f, match.MapSize.y * 0.5f, world.z);
            return new Vector2(mapRect.x + x * mapRect.width, mapRect.yMax - y * mapRect.height);
        }

        private void DrawCrosshair(BRParticipant player)
        {
            var weaponSystem = player != null ? player.Weapon : null;
            var weapon = player != null ? player.Inventory.ActiveWeapon : null;
            var recent = weaponSystem != null ? Mathf.Clamp01(1f - (Time.time - weaponSystem.LastShotTime) / 0.16f) : 0f;
            var aiming = player != null && (player.Motor.Aiming
                || Camera.main != null && Camera.main.GetComponent<ThirdPersonCamera>()?.Aiming == true);
            var x = Screen.width * 0.5f;
            var y = Screen.height * 0.5f;
            var cameraController = Camera.main != null ? Camera.main.GetComponent<ThirdPersonCamera>() : null;
            var cameraLock = cameraController?.AimAssistLocked == true;
            var crosshairColor = cameraLock || weaponSystem != null && weaponSystem.AimAssistLocked ? Cyan : Color.white;
            var fullFirstPersonAds = cameraController?.PresentationMode == AimPresentationMode.FirstPerson;
            if (aiming && fullFirstPersonAds)
            {
                var scopeType = cameraController != null ? cameraController.ActiveScopeType : ScopeType.IronSight;
                var scopeDrawingOwnReticle = ScopeController.HasOptic(scopeType)
                    && Camera.main.GetComponent<ScopeController>()?.ScopeActive == true;
                if (!scopeDrawingOwnReticle)
                {
                    var sightColor = scopeType is ScopeType.RedDot or ScopeType.Holographic
                        ? new Color(1f, 0.12f, 0.05f, 0.96f) : crosshairColor;
                    var radius = scopeType == ScopeType.Holographic ? 7f : 4f;
                    Fill(new Rect(x - 1.5f, y - 1.5f, 3f, 3f), sightColor);
                    Fill(new Rect(x - radius - 3f, y - 0.5f, 3f, 1f), new Color(sightColor.r, sightColor.g, sightColor.b, 0.65f));
                    Fill(new Rect(x + radius, y - 0.5f, 3f, 1f), new Color(sightColor.r, sightColor.g, sightColor.b, 0.65f));
                    Fill(new Rect(x - 0.5f, y - radius - 3f, 1f, 3f), new Color(sightColor.r, sightColor.g, sightColor.b, 0.65f));
                    Fill(new Rect(x - 0.5f, y + radius, 1f, 3f), new Color(sightColor.r, sightColor.g, sightColor.b, 0.65f));
                }
            }
            else if (weapon != null)
            {
                var fieldOfView = Camera.main != null ? Camera.main.fieldOfView : 62f;
                var gap = GameplayTuning.CrosshairGap(weaponSystem.CurrentSpread(aiming), fieldOfView, Screen.height)
                    + recent * 7f;
                DrawClassCrosshair(weapon.weaponClass, new Vector2(x, y), gap, crosshairColor);
            }
            if (weaponSystem != null && weaponSystem.LastShotHit && Time.time - weaponSystem.LastShotTime < 0.18f)
            {
                var hitColor = weaponSystem.LastShotHeadshot ? DamageFeedbackSystem.HeadshotColor : DamageFeedbackSystem.BodyColor;
                DrawLine(new Vector2(x - 9f, y - 9f), new Vector2(x - 3f, y - 3f), hitColor, 2f);
                DrawLine(new Vector2(x + 9f, y - 9f), new Vector2(x + 3f, y - 3f), hitColor, 2f);
                DrawLine(new Vector2(x - 9f, y + 9f), new Vector2(x - 3f, y + 3f), hitColor, 2f);
                DrawLine(new Vector2(x + 9f, y + 9f), new Vector2(x + 3f, y + 3f), hitColor, 2f);
            }
        }

        private void DrawClassCrosshair(WeaponClass weaponClass, Vector2 center, float gap, Color color)
        {
            var length = weaponClass switch
            {
                WeaponClass.Shotgun => 7f,
                WeaponClass.Sniper => 4f,
                WeaponClass.Pistol => 6f,
                _ => 8f
            };
            var thickness = weaponClass == WeaponClass.Shotgun ? 3f : 2f;
            Fill(new Rect(center.x - gap - length, center.y - thickness * 0.5f, length, thickness), color);
            Fill(new Rect(center.x + gap, center.y - thickness * 0.5f, length, thickness), color);
            if (weaponClass != WeaponClass.Sniper)
            {
                Fill(new Rect(center.x - thickness * 0.5f, center.y - gap - length, thickness, length), color);
                Fill(new Rect(center.x - thickness * 0.5f, center.y + gap, thickness, length), color);
            }
            if (weaponClass == WeaponClass.Pistol) Fill(new Rect(center.x - 1f, center.y - 1f, 2f, 2f), color);
        }

        private void DrawDamageFeedback()
        {
            var camera = Camera.main;
            var feedback = DamageFeedbackSystem.Instance;
            if (camera == null || feedback == null) return;
            foreach (var entry in feedback.Events)
            {
                var screen = camera.WorldToScreenPoint(entry.WorldPosition + Vector3.up * entry.Age * 0.65f);
                if (screen.z <= 0f) continue;
                var alpha = 1f - Mathf.Clamp01(entry.Age / entry.Duration);
                var color = entry.Headshot ? DamageFeedbackSystem.HeadshotColor : DamageFeedbackSystem.BodyColor;
                color.a = alpha;
                var style = entry.Headshot ? damageHeadshot : damageBody;
                style.normal.textColor = color;
                var stackOffset = new Vector2(entry.StackIndex * 13f, -entry.StackIndex * 8f);
                GUI.Label(new Rect(screen.x - 50f + stackOffset.x,
                    Screen.height - screen.y - 24f + stackOffset.y, 100f, 48f),
                    entry.Damage.ToString(), style);
            }
        }

        private void DrawCombatAlerts(BRParticipant player)
        {
            var feedback = DamageFeedbackSystem.Instance;
            if (feedback == null || player == null) return;
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            foreach (var incoming in feedback.IncomingEvents)
            {
                var direction = Vector3.ProjectOnPlane(incoming.SourcePosition - player.transform.position, Vector3.up);
                if (direction.sqrMagnitude < 0.01f) direction = player.transform.forward;
                var camera = Camera.main;
                var local = camera != null ? camera.transform.InverseTransformDirection(direction.normalized) : direction.normalized;
                var angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                var alpha = 1f - Mathf.Clamp01(incoming.Age / incoming.Duration);
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(angle, center);
                Fill(new Rect(center.x - 24f, center.y - 82f, 48f, 5f), new Color(1f, 0.12f, 0.06f, alpha));
                Fill(new Rect(center.x - 3f, center.y - 88f, 6f, 13f), new Color(1f, 0.12f, 0.06f, alpha));
                GUI.matrix = matrix;
            }

            var y = 210f;
            foreach (var elimination in feedback.EliminationEvents)
            {
                var alpha = 1f - Mathf.Clamp01(elimination.Age / elimination.Duration);
                var color = elimination.LocalPlayerKill ? Amber : Color.white;
                color.a = alpha;
                eliminationRight.normal.textColor = color;
                var weapon = string.IsNullOrWhiteSpace(elimination.WeaponId)
                    ? string.Empty : $" [{elimination.WeaponId.ToUpperInvariant()}]";
                var headshot = elimination.Headshot ? "  HEADSHOT" : string.Empty;
                var assist = string.IsNullOrWhiteSpace(elimination.Assistant)
                    ? string.Empty : $"  + {elimination.Assistant}";
                GUI.Label(new Rect(Screen.width - 510f, y, 490f, 22f),
                    $"{elimination.Killer}{assist}{weapon}  >  {elimination.Victim}{headshot}", eliminationRight);
                y += 22f;
            }

            for (var i = feedback.EliminationEvents.Count - 1; i >= 0; i--)
            {
                var elimination = feedback.EliminationEvents[i];
                if (!elimination.LocalPlayerKill || elimination.Age > 1.4f) continue;
                var alpha = 1f - Mathf.Clamp01(elimination.Age / 1.4f);
                var color = Amber;
                color.a = alpha;
                eliminationCenter.normal.textColor = color;
                GUI.Label(new Rect(center.x - 150f, center.y + 48f, 300f, 26f),
                    $"ELIMINOU {elimination.Victim.ToUpperInvariant()}", eliminationCenter);
                break;
            }
        }

        private void DrawControlsHint()
        {
            if (MobileRuntime) return;
            var text = match.State switch
            {
                MatchState.Active => "WASD mover   MOUSE olhar   RMB mirar   LMB atirar   E coletar   R recarregar   4 curar",
                MatchState.Staging => "WASD mover   MOUSE olhar   SPACE pular",
                MatchState.Plane when match.Player != null && match.Player.Phase == ParticipantPhase.Freefall => "WASD direcionar   SPACE abrir paraquedas",
                MatchState.Plane => "SPACE saltar do aviao   MOUSE olhar",
                _ => string.Empty
            };
            var safe = GuiSafeArea;
            var width = Mathf.Min(680f, safe.width - 40f);
            GUI.Label(new Rect(safe.center.x - width * 0.5f, safe.yMax - 28f, width, 20f), text, centered);
        }

        private void DrawMatchOverlay()
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.54f));
            var rect = MatchAuxiliaryLayout.ResultPanel(GuiSafeArea, match.LastRankedResult != null);
            Fill(rect, new Color(0.025f, 0.035f, 0.045f, 0.97f));
            var title = match.ResultText;
            GUI.Label(new Rect(rect.x + 20f, rect.y + 14f, rect.width - 40f, rect.height - 82f), title, centered);
            var buttonWidth = (rect.width - 48f) * 0.5f;
            if (GUI.Button(new Rect(rect.x + 20f, rect.yMax - 60f, buttonWidth, 38f), "JOGAR NOVAMENTE")) match.StartMatch();
            if (GUI.Button(new Rect(rect.x + 28f + buttonWidth, rect.yMax - 60f, buttonWidth, 38f), "VOLTAR AO LOBBY")) match.ReturnToLobby();
        }

        private void DrawPauseButton()
        {
            var rect = HudRect(HUDElementId.Pause);
            DrawTouchControl(rect, HudIconId.Pause, new Color(0.02f, 0.04f, 0.05f, 0.72f));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) match.SetPaused(true);
        }

        private void DrawPauseOverlay()
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.62f));
            var safe = GuiSafeArea;
            var rect = MatchAuxiliaryLayout.PausePanel(safe);
            var settings = MatchSettingsLayout.Pause(rect);
            Fill(rect, new Color(0.025f, 0.035f, 0.045f, 0.98f));
            GUI.Label(new Rect(rect.x + 20f, rect.y + 18f, rect.width - 40f, 36f), "PAUSADO", pauseTitle);
            DrawSensitivitySetting(settings.LookRow, "SENSIBILIDADE", GamePreferences.LookSensitivity, "0.0x",
                SensitivitySetting.Look, 0.1f);
            DrawSensitivitySetting(settings.AdsRow, "SENS. ADS", GamePreferences.AdsSensitivity, "0.00",
                SensitivitySetting.Ads, 0.05f);
            DrawSensitivitySetting(settings.FireDragRow, "FIRE DRAG", GamePreferences.FireDragSensitivity, "0.00",
                SensitivitySetting.FireDrag, 0.05f);
            if (!editingHud && GUI.Button(settings.EditHudButton, "EDITAR HUD")) BeginHudEdit();
            if (!editingHud && GUI.Button(settings.PrimaryButton, "CONTINUAR")) match.SetPaused(false);
            if (!editingHud && GUI.Button(settings.SecondaryButton, "LOBBY")) match.ReturnToLobby();
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color, float width)
        {
            var matrix = GUI.matrix;
            var angle = Vector3.Angle(end - start, Vector2.right);
            if (start.y > end.y) angle = -angle;
            GUIUtility.RotateAroundPivot(angle, start);
            Fill(new Rect(start.x, start.y - width * 0.5f, Vector2.Distance(start, end), width), color);
            GUI.matrix = matrix;
        }

        private void DrawMobileControls()
        {
            var move = new Rect(MobileControlLayout.MoveCenter.x - MobileControlLayout.MoveRadius,
                Screen.height - MobileControlLayout.MoveCenter.y - MobileControlLayout.MoveRadius,
                MobileControlLayout.MoveRadius * 2f, MobileControlLayout.MoveRadius * 2f);
            DrawMobileControl(HUDElementId.Move, move, HudIconId.Move, new Color(0.03f, 0.07f, 0.09f, 0.38f));
            var thumbCenter = MobileControlLayout.MoveCenter + MobileControlLayout.MoveVisual * (MobileControlLayout.MoveRadius * 0.54f);
            if (mobileLayout.Get(HUDElementId.Move)?.visible != false)
                DrawDisc(new Rect(thumbCenter.x - 17f, Screen.height - thumbCenter.y - 17f, 34f, 34f),
                    new Color(0.92f, 0.97f, 1f, 0.62f));
            DrawMobileControl(HUDElementId.Fire, MobileControlLayout.ToGui(MobileControlLayout.Fire), HudIconId.Fire, MobileControlLayout.FireHeld
                ? new Color(1f, 0.58f, 0.12f, 0.72f) : new Color(1f, 0.24f, 0.12f, 0.48f));
            DrawMobileControl(HUDElementId.Aim, MobileControlLayout.ToGui(MobileControlLayout.Aim), HudIconId.Aim, MobileControlLayout.AimHeld
                ? new Color(0.12f, 0.86f, 1f, 0.68f) : new Color(0.12f, 0.65f, 0.78f, 0.38f));
            DrawMobileControl(HUDElementId.Jump, MobileControlLayout.ToGui(MobileControlLayout.Jump), HudIconId.Jump, new Color(0.24f, 0.31f, 0.36f, 0.42f));
            DrawMobileControl(HUDElementId.Interact, MobileControlLayout.ToGui(MobileControlLayout.Interact), HudIconId.Interact, new Color(0.92f, 0.55f, 0.08f, 0.42f));
            DrawMobileControl(HUDElementId.Reload, MobileControlLayout.ToGui(MobileControlLayout.Reload), HudIconId.Reload, new Color(0.18f, 0.25f, 0.29f, 0.44f));
            DrawMobileControl(HUDElementId.Heal, MobileControlLayout.ToGui(MobileControlLayout.Heal), HudIconId.Heal, new Color(0.12f, 0.66f, 0.29f, 0.46f));
            DrawMobileControl(HUDElementId.Swap, MobileControlLayout.ToGui(MobileControlLayout.Swap), HudIconId.Swap, new Color(0.34f, 0.38f, 0.48f, 0.44f));
        }

        private void DrawLobbySettings(MatchSettingsRects layout)
        {
            DrawSensitivitySetting(layout.LookRow, "SENSIBILIDADE", GamePreferences.LookSensitivity, "0.0x",
                SensitivitySetting.Look, 0.1f);
            DrawSensitivitySetting(layout.AdsRow, "SENS. ADS", GamePreferences.AdsSensitivity, "0.00",
                SensitivitySetting.Ads, 0.05f);
            DrawSensitivitySetting(layout.FireDragRow, "FIRE DRAG", GamePreferences.FireDragSensitivity, "0.00",
                SensitivitySetting.FireDrag, 0.05f);
            GUI.Label(new Rect(layout.FrameRateRow.x, layout.FrameRateRow.y, layout.FrameRateRow.width, 16f), "LIMITE DE FPS", small);
            for (var i = 0; i < FrameRateOptions.Length; i++)
            {
                var previous = GUI.backgroundColor;
                GUI.backgroundColor = GamePreferences.FrameRate == FrameRateOptions[i] ? Amber : Color.gray;
                if (GUI.Button(new Rect(layout.FrameRateRow.x + i * 50f, layout.FrameRateRow.y + 18f, 44f, 26f),
                    FrameRateOptions[i].ToString())) GamePreferences.SetFrameRate(FrameRateOptions[i]);
                GUI.backgroundColor = previous;
            }
        }

        private bool DrawSegment(Rect rect, string label, bool selected)
        {
            return LobbyButton(rect, label,
                selected ? new Color(0.92f, 0.54f, 0.08f, 0.96f)
                    : new Color(0.08f, 0.11f, 0.13f, 0.94f));
        }

        private bool LobbyButton(Rect rect, string label, Color color, GUIStyle style = null)
        {
            Fill(rect, color);
            return GUI.Button(rect, label, style ?? lobbyButton);
        }

        private bool LobbyButton(Rect rect, GUIContent content, Color color)
        {
            Fill(rect, color);
            return GUI.Button(rect, content, lobbyButton);
        }

        private void DrawSensitivitySetting(Rect row, string label, float value, string format,
            SensitivitySetting setting, float step)
        {
            GUI.Label(new Rect(row.x, row.y, row.width, 16f), label, small);
            var buttonWidth = Mathf.Min(36f, row.width * 0.25f);
            var valueWidth = Mathf.Max(36f, row.width - buttonWidth * 2f - 12f);
            if (GUI.Button(new Rect(row.x, row.y + 18f, buttonWidth, 26f), "-")) AdjustSensitivity(setting, -step);
            GUI.Label(new Rect(row.x + buttonWidth + 6f, row.y + 18f, valueWidth, 26f), value.ToString(format), centered);
            if (GUI.Button(new Rect(row.xMax - buttonWidth, row.y + 18f, buttonWidth, 26f), "+")) AdjustSensitivity(setting, step);
        }

        private static void AdjustSensitivity(SensitivitySetting setting, float delta)
        {
            if (setting == SensitivitySetting.Look)
                GamePreferences.SetLookSensitivity(GamePreferences.LookSensitivity + delta);
            else if (setting == SensitivitySetting.Ads)
                GamePreferences.SetAdsSensitivity(GamePreferences.AdsSensitivity + delta);
            else
                GamePreferences.SetFireDragSensitivity(GamePreferences.FireDragSensitivity + delta);
        }

        private void EnsureLayouts()
        {
            var safe = GuiSafeArea;
            desktopDefaults ??= id => DefaultHudRectFor(id, false);
            mobileDefaults ??= id => DefaultHudRectFor(id, true);
            if (!layoutsInitialized)
            {
                desktopLayout ??= HUDLayoutProfile.Load(false, safe, desktopDefaults);
                mobileLayout ??= HUDLayoutProfile.Load(true, safe, mobileDefaults);
                lobbySelection = MatchLobbyLayout.Selection(safe);
                configuredSafeArea = safe;
                layoutsInitialized = true;
                mobileControlsDirty = true;
            }
            else if (!SameRect(configuredSafeArea, safe))
            {
                desktopLayout.Repair(safe, desktopDefaults);
                mobileLayout.Repair(safe, mobileDefaults);
                lobbySelection = MatchLobbyLayout.Selection(safe);
                configuredSafeArea = safe;
                mobileControlsDirty = true;
            }

            if (!mobileControlsDirty) return;
            MobileControlLayout.Configure(
                mobileLayout.Resolve(HUDElementId.Move, safe, DefaultHudRectFor(HUDElementId.Move, true)),
                mobileLayout.Resolve(HUDElementId.Fire, safe, DefaultHudRectFor(HUDElementId.Fire, true)),
                mobileLayout.Resolve(HUDElementId.Aim, safe, DefaultHudRectFor(HUDElementId.Aim, true)),
                mobileLayout.Resolve(HUDElementId.Jump, safe, DefaultHudRectFor(HUDElementId.Jump, true)),
                mobileLayout.Resolve(HUDElementId.Interact, safe, DefaultHudRectFor(HUDElementId.Interact, true)),
                mobileLayout.Resolve(HUDElementId.Reload, safe, DefaultHudRectFor(HUDElementId.Reload, true)),
                mobileLayout.Resolve(HUDElementId.Heal, safe, DefaultHudRectFor(HUDElementId.Heal, true)),
                mobileLayout.Resolve(HUDElementId.Swap, safe, DefaultHudRectFor(HUDElementId.Swap, true)),
                IsMobileControlVisible(HUDElementId.Move), IsMobileControlVisible(HUDElementId.Fire),
                IsMobileControlVisible(HUDElementId.Aim), IsMobileControlVisible(HUDElementId.Jump),
                IsMobileControlVisible(HUDElementId.Interact), IsMobileControlVisible(HUDElementId.Reload),
                IsMobileControlVisible(HUDElementId.Heal), IsMobileControlVisible(HUDElementId.Swap));
            mobileControlsDirty = false;
        }

        private static readonly bool ForceMobileHudPreview =
            Array.Exists(Environment.GetCommandLineArgs(), argument =>
                string.Equals(argument, "-brMobileHudPreview", StringComparison.OrdinalIgnoreCase));

        private bool MobileRuntime => (Application.isMobilePlatform && !Application.isEditor)
            || ForceMobileHudPreview;
        private Rect GuiSafeArea => new(Screen.safeArea.x, Screen.height - Screen.safeArea.yMax, Screen.safeArea.width, Screen.safeArea.height);
        private HUDLayoutProfile ActiveLayout => MobileRuntime ? mobileLayout : desktopLayout;
        private HUDLayoutProfile EditingLayout => editMobile ? mobileLayout : desktopLayout;

        private Rect HudRect(HUDElementId id)
        {
            var profile = ActiveLayout;
            return profile != null ? profile.Resolve(id, GuiSafeArea, DefaultHudRect(id)) : DefaultHudRect(id);
        }

        private void DrawConfigured(HUDElementId id, BRParticipant player = null)
        {
            var entry = ActiveLayout?.Get(id);
            if (entry != null && !entry.visible) return;
            var previous = GUI.color;
            if (entry != null) GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * entry.opacity);
            switch (id)
            {
                case HUDElementId.Vitals: DrawVitals(player); break;
                case HUDElementId.MatchStatus:
                    if (match.State == MatchState.Staging) DrawStagingStatus();
                    else DrawMatchStatus();
                    break;
                case HUDElementId.Compass: DrawCompass(); break;
                case HUDElementId.Minimap: DrawMinimap(); break;
                case HUDElementId.Weapon: DrawWeapon(player); break;
                case HUDElementId.LootPrompt: DrawLootPrompt(); break;
                case HUDElementId.PickupFeedback: DrawPickupFeedback(); break;
                case HUDElementId.Pause: DrawPauseButton(); break;
            }
            GUI.color = previous;
        }

        private bool IsMobileControlVisible(HUDElementId id) => mobileLayout?.Get(id)?.visible != false;

        private static bool SameRect(Rect left, Rect right) => Mathf.Approximately(left.x, right.x)
            && Mathf.Approximately(left.y, right.y) && Mathf.Approximately(left.width, right.width)
            && Mathf.Approximately(left.height, right.height);

        private void DrawMobileControl(HUDElementId id, Rect rect, HudIconId icon, Color color)
        {
            var entry = mobileLayout?.Get(id);
            if (entry != null && !entry.visible) return;
            var previous = GUI.color;
            if (entry != null) GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * entry.opacity);
            DrawTouchControl(rect, icon, color);
            GUI.color = previous;
        }

        private Rect DefaultHudRect(HUDElementId id) => DefaultHudRectFor(id, editingHud ? editMobile : MobileRuntime);

        private Rect DefaultHudRectFor(HUDElementId id, bool mobile)
        {
            var safe = GuiSafeArea;
            Rect BottomControl(float centerX, float centerFromBottom, float size) =>
                new(centerX - size * 0.5f, safe.yMax - centerFromBottom - size * 0.5f, size, size);
            return id switch
            {
                HUDElementId.Vitals => new Rect(safe.xMin + 18f, safe.yMin + 18f, 250f, 82f),
                HUDElementId.MatchStatus => new Rect(safe.center.x - 112f, safe.yMin + 18f, 224f, 52f),
                HUDElementId.Compass => new Rect(safe.center.x - 92f, safe.yMin + 74f, 184f, 22f),
                HUDElementId.Minimap => new Rect(safe.xMax - 194f, safe.yMin + 18f, 176f, 176f),
                HUDElementId.Weapon => mobile
                    ? new Rect(safe.xMin + 18f, safe.yMin + 112f, 268f, 126f)
                    : new Rect(safe.xMax - 286f, safe.yMax - 150f, 268f, 126f),
                HUDElementId.LootPrompt => new Rect(safe.center.x - 165f, safe.yMax - (mobile ? 232f : 118f), 330f, 58f),
                HUDElementId.PickupFeedback => new Rect(safe.center.x - 155f, safe.center.y + 58f, 310f, 30f),
                HUDElementId.Pause => new Rect(safe.xMin + 278f, safe.yMin + 18f, 38f, 38f),
                HUDElementId.Move => BottomControl(safe.xMin + 92f, 100f, 124f),
                HUDElementId.Fire => BottomControl(safe.xMax - 70f, 104f, 78f),
                HUDElementId.Aim => BottomControl(safe.xMax - 158f, 158f, 62f),
                HUDElementId.Jump => BottomControl(safe.xMax - 72f, 205f, 62f),
                HUDElementId.Interact => BottomControl(safe.xMax - 246f, 102f, 58f),
                HUDElementId.Reload => BottomControl(safe.xMax - 158f, 82f, 52f),
                HUDElementId.Heal => BottomControl(safe.xMin + 186f, 55f, 52f),
                HUDElementId.Swap => BottomControl(safe.xMin + 244f, 55f, 52f),
                _ => new Rect(safe.xMin, safe.yMin, 80f, 40f)
            };
        }

        private void BeginHudEdit()
        {
            EnsureLayouts();
            desktopSnapshot = desktopLayout.Clone();
            mobileSnapshot = mobileLayout.Clone();
            editHistory.Clear();
            editMobile = MobileRuntime;
            editingHud = true;
            draggingElement = false;
            resizingElement = false;
        }

        private void DrawHudEditor()
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.48f));
            var safe = GuiSafeArea;
            var toolbarWidth = Mathf.Min(980f, safe.width - 20f);
            var toolbar = new Rect(safe.center.x - toolbarWidth * 0.5f,
                editorToolbarTop ? safe.yMin + 10f : safe.yMax - 116f, toolbarWidth, 106f);
            ProcessHudEditorInput(toolbar);

            foreach (var entry in EditingLayout.elements)
            {
                if (entry == null || (!editMobile && HUDLayoutProfile.IsMobileControl(entry.id))) continue;
                var rect = EditingLayout.Resolve(entry.id, safe, DefaultHudRect(entry.id));
                var selectedFrame = entry.id == selectedElement;
                Fill(rect, selectedFrame ? new Color(0.1f, 0.82f, 0.95f, 0.2f) : new Color(1f, 1f, 1f, 0.08f));
                DrawOutline(rect, selectedFrame ? Cyan : new Color(1f, 1f, 1f, 0.55f), selectedFrame ? 2f : 1f);
                var frameStyle = rect.width < 72f ? editorFrameCompact : editorFrame;
                GUI.Label(new Rect(rect.x + 5f, rect.y + 3f, Mathf.Max(24f, rect.width - 10f), 20f),
                    rect.width < 72f ? HudShortName(entry.id) : HudName(entry.id), frameStyle);
                if (selectedFrame)
                {
                    var handle = ResizeHandle(rect);
                    Fill(handle, Amber);
                }
            }

            Fill(toolbar, new Color(0.025f, 0.035f, 0.045f, 0.99f));
            Fill(new Rect(toolbar.x, toolbar.y, toolbar.width, 3f), Cyan);
            GUI.Label(new Rect(toolbar.x + 12f, toolbar.y + 8f, 112f, 22f), "EDITOR HUD", medium);
            if (GUI.Button(new Rect(toolbar.x + 126f, toolbar.y + 7f, 78f, 26f), editMobile ? "CELULAR" : "PC"))
            {
                editMobile = !editMobile;
                editHistory.Clear();
                selectedElement = editMobile ? HUDElementId.Fire : HUDElementId.Vitals;
            }
            if (GUI.Button(new Rect(toolbar.x + 212f, toolbar.y + 7f, 88f, 26f), snapToGrid ? "GRADE ON" : "GRADE OFF")) snapToGrid = !snapToGrid;
            if (GUI.Button(new Rect(toolbar.x + 308f, toolbar.y + 7f, 88f, 26f), "DESFAZER") && editHistory.Count > 0)
            {
                if (editMobile)
                {
                    mobileLayout = editHistory.Pop();
                    mobileControlsDirty = true;
                }
                else desktopLayout = editHistory.Pop();
            }
            if (GUI.Button(new Rect(toolbar.x + 404f, toolbar.y + 7f, 104f, 26f), "RESET ITEM"))
            {
                RememberEdit();
                EditingLayout.ResetElement(selectedElement, safe, DefaultHudRect(selectedElement));
                if (editMobile && HUDLayoutProfile.IsMobileControl(selectedElement)) mobileControlsDirty = true;
            }
            if (GUI.Button(new Rect(toolbar.x + 516f, toolbar.y + 7f, 104f, 26f), "RESET TUDO"))
            {
                RememberEdit();
                EditingLayout.ResetAll(safe, editMobile ? mobileDefaults : desktopDefaults);
                if (editMobile) mobileControlsDirty = true;
            }
            if (GUI.Button(new Rect(toolbar.x + 628f, toolbar.y + 7f, 120f, 26f), editorToolbarTop ? "BARRA EMBAIXO" : "BARRA NO TOPO"))
                editorToolbarTop = !editorToolbarTop;

            var selected = EditingLayout.Get(selectedElement);
            if (selected != null)
            {
                GUI.Label(new Rect(toolbar.x + 12f, toolbar.y + 42f, 82f, 20f), "OPACIDADE", small);
                var opacitySlider = new Rect(toolbar.x + 94f, toolbar.y + 44f, 126f, 18f);
                if (Event.current.type == EventType.MouseDown && opacitySlider.Contains(Event.current.mousePosition)) RememberEdit();
                selected.opacity = GUI.HorizontalSlider(opacitySlider, selected.opacity, 0.2f, 1f);
                GUI.Label(new Rect(toolbar.x + 226f, toolbar.y + 41f, 48f, 20f), $"{selected.opacity * 100f:0}%", small);

                var currentRect = EditingLayout.Resolve(selectedElement, safe, DefaultHudRect(selectedElement));
                var defaultRect = DefaultHudRect(selectedElement);
                var currentScale = HUDLayoutProfile.UniformScale(currentRect, defaultRect);
                GUI.Label(new Rect(toolbar.x + 286f, toolbar.y + 42f, 72f, 20f), "TAMANHO", small);
                var sizeSlider = new Rect(toolbar.x + 358f, toolbar.y + 44f, 126f, 18f);
                if (Event.current.type == EventType.MouseDown && sizeSlider.Contains(Event.current.mousePosition)) RememberEdit();
                var requestedScale = GUI.HorizontalSlider(sizeSlider, currentScale, 0.5f, 2f);
                GUI.Label(new Rect(toolbar.x + 490f, toolbar.y + 41f, 54f, 20f), $"{requestedScale * 100f:0}%", small);
                if (Mathf.Abs(requestedScale - currentScale) > 0.002f)
                {
                    var resized = HUDLayoutProfile.ScaleAroundCenter(currentRect, defaultRect, requestedScale);
                    EditingLayout.SetRect(selectedElement, resized, safe);
                    if (editMobile && HUDLayoutProfile.IsMobileControl(selectedElement)) mobileControlsDirty = true;
                }
                if (GUI.Button(new Rect(toolbar.x + 552f, toolbar.y + 38f, 92f, 27f), selected.visible ? "VISIVEL" : "OCULTO"))
                {
                    RememberEdit();
                    selected.visible = !selected.visible;
                    if (editMobile && HUDLayoutProfile.IsMobileControl(selectedElement)) mobileControlsDirty = true;
                }
            }
            GUI.Label(new Rect(toolbar.x + 12f, toolbar.y + 72f, toolbar.width - 300f, 22f), "ARRASTE PARA MOVER  |  CANTO LARANJA PARA REDIMENSIONAR", small);
            if (GUI.Button(new Rect(toolbar.xMax - 270f, toolbar.y + 69f, 122f, 28f), "CANCELAR")) CancelHudEdit();
            if (GUI.Button(new Rect(toolbar.xMax - 140f, toolbar.y + 69f, 128f, 28f), "SALVAR")) SaveHudEdit();
        }

        private void ProcessHudEditorInput(Rect toolbar)
        {
            var current = Event.current;
            var mouse = current.mousePosition;
            if (current.type == EventType.MouseDown && current.button == 0 && !toolbar.Contains(mouse))
            {
                for (var i = EditingLayout.elements.Count - 1; i >= 0; i--)
                {
                    var entry = EditingLayout.elements[i];
                    if (entry == null || (!editMobile && HUDLayoutProfile.IsMobileControl(entry.id))) continue;
                    var rect = EditingLayout.Resolve(entry.id, GuiSafeArea, DefaultHudRect(entry.id));
                    if (!rect.Contains(mouse)) continue;
                    selectedElement = entry.id;
                    RememberEdit();
                    dragStartRect = rect;
                    dragStartMouse = mouse;
                    resizingElement = ResizeHandle(rect).Contains(mouse);
                    draggingElement = !resizingElement;
                    dragOffset = mouse - rect.position;
                    current.Use();
                    break;
                }
            }
            else if (current.type == EventType.MouseDrag && (draggingElement || resizingElement))
            {
                var rect = dragStartRect;
                if (resizingElement)
                {
                    rect.width = Mathf.Max(34f, dragStartRect.width + mouse.x - dragStartMouse.x);
                    rect.height = Mathf.Max(24f, dragStartRect.height + mouse.y - dragStartMouse.y);
                }
                else rect.position = mouse - dragOffset;
                if (snapToGrid)
                {
                    rect.x = Mathf.Round(rect.x / 8f) * 8f;
                    rect.y = Mathf.Round(rect.y / 8f) * 8f;
                    rect.width = Mathf.Round(rect.width / 8f) * 8f;
                    rect.height = Mathf.Round(rect.height / 8f) * 8f;
                }
                EditingLayout.SetRect(selectedElement, rect, GuiSafeArea);
                if (editMobile && HUDLayoutProfile.IsMobileControl(selectedElement)) mobileControlsDirty = true;
                current.Use();
            }
            else if (current.type == EventType.MouseUp && (draggingElement || resizingElement))
            {
                draggingElement = false;
                resizingElement = false;
                current.Use();
            }
        }

        private void RememberEdit()
        {
            editHistory.Push(EditingLayout.Clone());
            while (editHistory.Count > 24)
            {
                var items = editHistory.ToArray();
                editHistory.Clear();
                for (var i = Mathf.Min(22, items.Length - 1); i >= 0; i--) editHistory.Push(items[i]);
            }
        }

        private void SaveHudEdit()
        {
            desktopLayout.Save();
            mobileLayout.Save();
            editingHud = false;
            editHistory.Clear();
            mobileControlsDirty = true;
        }

        private void CancelHudEdit()
        {
            if (desktopSnapshot != null) desktopLayout = desktopSnapshot;
            if (mobileSnapshot != null) mobileLayout = mobileSnapshot;
            editingHud = false;
            editHistory.Clear();
            mobileControlsDirty = true;
        }

        private static Rect ResizeHandle(Rect rect) => new(rect.xMax - 14f, rect.yMax - 14f, 14f, 14f);

        private void DrawOutline(Rect rect, Color color, float width)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, width), color);
            Fill(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            Fill(new Rect(rect.x, rect.y, width, rect.height), color);
            Fill(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        private void FillCutPanel(Rect rect, Color color, Color accent)
        {
            const float cut = 4f;
            Fill(new Rect(rect.x + cut, rect.y, Mathf.Max(0f, rect.width - cut * 2f), rect.height), color);
            Fill(new Rect(rect.x, rect.y + cut, rect.width, Mathf.Max(0f, rect.height - cut * 2f)), color);
            Fill(new Rect(rect.x + cut, rect.y, Mathf.Min(34f, Mathf.Max(0f, rect.width - cut * 2f)), 2f),
                new Color(accent.r, accent.g, accent.b, 0.9f));
        }

        private static string HudName(HUDElementId id) => id switch
        {
            HUDElementId.Vitals => "VIDA / ARMADURA",
            HUDElementId.MatchStatus => "STATUS DA PARTIDA",
            HUDElementId.Weapon => "ARMA / MUNICAO",
            HUDElementId.LootPrompt => "INTERACAO / LOOT",
            HUDElementId.PickupFeedback => "ITEM COLETADO",
            HUDElementId.Move => "JOYSTICK",
            HUDElementId.Interact => "USAR",
            HUDElementId.Reload => "RECARREGAR",
            HUDElementId.Heal => "KIT MEDICO",
            HUDElementId.Swap => "TROCAR ARMA",
            _ => id.ToString().ToUpperInvariant()
        };

        private static string HudShortName(HUDElementId id) => id switch
        {
            HUDElementId.Pause => "II",
            HUDElementId.Fire => "FIRE",
            HUDElementId.Aim => "AIM",
            HUDElementId.Jump => "JUMP",
            HUDElementId.Interact => "USE",
            HUDElementId.Reload => "R",
            HUDElementId.Heal => "+",
            HUDElementId.Swap => "1/2",
            _ => id.ToString().ToUpperInvariant()
        };

        private void DrawTouchControl(Rect rect, HudIconId icon, Color color)
        {
            DrawDisc(rect, color);
            var iconSize = Mathf.Min(rect.width, rect.height) * 0.48f;
            DrawHudIcon(new Rect(rect.center.x - iconSize * 0.5f, rect.center.y - iconSize * 0.5f,
                iconSize, iconSize), icon);
        }

        private void DrawDisc(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = new Color(color.r * previous.r, color.g * previous.g, color.b * previous.b,
                color.a * previous.a);
            GUI.DrawTexture(rect, controlDisc, ScaleMode.StretchToFill, true);
            GUI.color = new Color(0.72f, 0.93f, 0.98f, 0.72f * previous.a);
            GUI.DrawTexture(rect, zoneRing, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private static void DrawHudIcon(Rect rect, HudIconId iconId)
        {
            var sprite = HudIconCatalog.Load(iconId);
            if (sprite == null) return;
            GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true);
        }

        private void DrawWeaponIcon(Rect rect, WeaponClass weaponClass, Color color)
        {
            var cy = rect.center.y;
            var left = rect.x + rect.width * 0.08f;
            var right = rect.xMax - rect.width * 0.06f;
            if (weaponClass == WeaponClass.Pistol)
            {
                Fill(new Rect(left + rect.width * 0.20f, cy - 5f, rect.width * 0.47f, 8f), color);
                Fill(new Rect(left + rect.width * 0.55f, cy + 1f, rect.width * 0.10f, rect.height * 0.42f), color);
                Fill(new Rect(left + rect.width * 0.18f, cy - 2f, rect.width * 0.12f, 5f), color);
                return;
            }

            var bodyStart = weaponClass == WeaponClass.Smg ? left + rect.width * 0.18f : left + rect.width * 0.25f;
            var bodyWidth = weaponClass == WeaponClass.Smg ? rect.width * 0.42f : rect.width * 0.38f;
            Fill(new Rect(bodyStart, cy - 5f, bodyWidth, 9f), color);
            Fill(new Rect(bodyStart + bodyWidth, cy - 2f, Mathf.Max(4f, right - bodyStart - bodyWidth), 3f), color);
            DrawLine(new Vector2(bodyStart, cy - 2f), new Vector2(left, cy + 7f), color, 5f);
            DrawLine(new Vector2(bodyStart + bodyWidth * 0.52f, cy + 3f),
                new Vector2(bodyStart + bodyWidth * 0.60f, cy + rect.height * 0.42f), color, 5f);
            if (weaponClass is WeaponClass.Rifle or WeaponClass.Smg)
                DrawLine(new Vector2(bodyStart + bodyWidth * 0.42f, cy + 3f),
                    new Vector2(bodyStart + bodyWidth * 0.48f, cy + rect.height * 0.38f), color, 6f);
            if (weaponClass == WeaponClass.Sniper)
            {
                Fill(new Rect(bodyStart + bodyWidth * 0.16f, cy - 10f, bodyWidth * 0.52f, 4f), color);
                Fill(new Rect(bodyStart + bodyWidth * 0.28f, cy - 13f, 3f, 7f), color);
                Fill(new Rect(bodyStart + bodyWidth * 0.58f, cy - 13f, 3f, 7f), color);
            }
            else if (weaponClass == WeaponClass.Shotgun)
                Fill(new Rect(bodyStart + bodyWidth, cy + 3f, Mathf.Max(4f, right - bodyStart - bodyWidth), 2f), color);
        }

        private void DrawFistIcon(Rect rect, Color color)
        {
            Fill(new Rect(rect.x + rect.width * 0.28f, rect.y + rect.height * 0.36f,
                rect.width * 0.44f, rect.height * 0.46f), color);
            for (var i = 0; i < 4; i++)
                Fill(new Rect(rect.x + rect.width * (0.20f + i * 0.16f), rect.y + rect.height * 0.16f,
                    rect.width * 0.13f, rect.height * 0.28f), color);
        }

        private void DrawItemIcon(Rect rect, CombatItemIcon icon, Color color)
        {
            if (icon == CombatItemIcon.MedKit)
            {
                DrawHudIcon(rect, HudIconId.Heal);
                return;
            }
            if (icon == CombatItemIcon.Ammo)
            {
                var bulletWidth = Mathf.Max(2f, rect.width * 0.16f);
                for (var i = 0; i < 3; i++)
                {
                    var x = rect.x + rect.width * (0.18f + i * 0.25f);
                    Fill(new Rect(x, rect.y + rect.height * 0.28f, bulletWidth, rect.height * 0.58f), color);
                    Fill(new Rect(x + 1f, rect.y + rect.height * 0.16f,
                        Mathf.Max(1f, bulletWidth - 2f), rect.height * 0.16f), color);
                }
                return;
            }
            if (icon is CombatItemIcon.Armor or CombatItemIcon.Helmet)
            {
                var width = rect.width * 0.68f;
                var body = new Rect(rect.center.x - width * 0.5f, rect.y + rect.height * 0.20f,
                    width, rect.height * 0.64f);
                DrawOutline(body, color, Mathf.Max(2f, rect.width * 0.08f));
                if (icon == CombatItemIcon.Helmet)
                    Fill(new Rect(body.x - rect.width * 0.08f, body.center.y,
                        body.width + rect.width * 0.16f, Mathf.Max(2f, rect.height * 0.10f)), color);
                else
                {
                    Fill(new Rect(body.x - rect.width * 0.10f, body.y,
                        rect.width * 0.18f, body.height * 0.55f), color);
                    Fill(new Rect(body.xMax - rect.width * 0.08f, body.y,
                        rect.width * 0.18f, body.height * 0.55f), color);
                }
                return;
            }
            if (icon == CombatItemIcon.GlooWall)
            {
                var wall = new Rect(rect.x + rect.width * 0.14f, rect.y + rect.height * 0.27f,
                    rect.width * 0.72f, rect.height * 0.52f);
                DrawOutline(wall, color, Mathf.Max(2f, rect.width * 0.08f));
                Fill(new Rect(wall.center.x - 1f, wall.y, 2f, wall.height), color);
                return;
            }
            if (icon == CombatItemIcon.Grenade)
            {
                var body = new Rect(rect.x + rect.width * 0.25f, rect.y + rect.height * 0.30f,
                    rect.width * 0.5f, rect.height * 0.55f);
                var previous = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(body, controlDisc);
                GUI.color = previous;
                Fill(new Rect(rect.center.x - 2f, rect.y + rect.height * 0.13f,
                    4f, rect.height * 0.20f), color);
                return;
            }
            if (icon == CombatItemIcon.Currency)
            {
                DrawLobbyIcon(rect, LobbyIconId.Coin);
                return;
            }
            if (icon == CombatItemIcon.Backpack)
            {
                var body = new Rect(rect.x + rect.width * 0.18f, rect.y + rect.height * 0.22f,
                    rect.width * 0.64f, rect.height * 0.68f);
                Fill(body, color);
                DrawOutline(body, new Color(0f, 0f, 0f, 0.45f), 1f);
                Fill(new Rect(body.x + body.width * 0.18f, rect.y + rect.height * 0.08f,
                    body.width * 0.64f, rect.height * 0.20f), color);
                return;
            }

            Fill(new Rect(rect.x + rect.width * 0.17f, rect.y + rect.height * 0.30f,
                rect.width * 0.66f, rect.height * 0.42f), color);
            Fill(new Rect(rect.center.x - 2f, rect.y + rect.height * 0.12f, 4f, rect.height * 0.76f), color);
        }

        private void DrawBar(Rect rect, float value, Color color, string text)
        {
            Fill(rect, new Color(1f, 1f, 1f, 0.13f));
            Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), color);
            GUI.Label(new Rect(rect.x + 4f, rect.y - 2f, rect.width - 8f, rect.height + 4f), text, small);
        }

        private void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = previous;
        }

        private static Color LootColor(LootKind kind) => kind switch
        {
            LootKind.Weapon => Amber,
            LootKind.Ammo => Cyan,
            LootKind.Heal => new Color(0.2f, 0.95f, 0.42f),
            LootKind.Currency => Amber,
            LootKind.Backpack => new Color(0.48f, 0.72f, 1f),
            _ => new Color(0.72f, 0.65f, 0.95f)
        };

        private void DrawLootIcon(Rect rect, LootKind kind, WeaponClass weaponClass, Color color)
        {
            if (kind == LootKind.Currency)
            {
                DrawLobbyIcon(rect, LobbyIconId.Coin);
                return;
            }
            if (kind == LootKind.Weapon)
            {
                DrawWeaponIcon(rect, weaponClass, color);
                return;
            }
            var icon = kind switch
            {
                LootKind.Ammo => CombatItemIcon.Ammo,
                LootKind.Heal => CombatItemIcon.MedKit,
                LootKind.Vest => CombatItemIcon.Armor,
                LootKind.Helmet => CombatItemIcon.Helmet,
                LootKind.Backpack => CombatItemIcon.Backpack,
                LootKind.GlooWall => CombatItemIcon.GlooWall,
                LootKind.Grenade => CombatItemIcon.Grenade,
                _ => CombatItemIcon.Attachment
            };
            DrawItemIcon(rect, icon, color);
        }

        private void DrawDeathLootIcon(Rect rect, DeathLootEntry entry, Color color)
        {
            if (entry.Kind == DeathLootKind.Weapon && entry.Weapon != null)
            {
                DrawWeaponIcon(rect, entry.Weapon.weaponClass, color);
                return;
            }
            var icon = entry.Kind switch
            {
                DeathLootKind.Ammo => CombatItemIcon.Ammo,
                DeathLootKind.MedKit => CombatItemIcon.MedKit,
                DeathLootKind.Vest => CombatItemIcon.Armor,
                DeathLootKind.Helmet => CombatItemIcon.Helmet,
                DeathLootKind.GlooWall => CombatItemIcon.GlooWall,
                DeathLootKind.Grenade => CombatItemIcon.Grenade,
                DeathLootKind.Currency => CombatItemIcon.Currency,
                DeathLootKind.Backpack => CombatItemIcon.Backpack,
                _ => CombatItemIcon.Attachment
            };
            DrawItemIcon(rect, icon, color);
        }

        private void EnsureStyles()
        {
            if (white != null) return;
            white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();
            zoneRing = CreateRingTexture(96);
            controlDisc = CreateDiscTexture(96);
            small = MakeStyle(12, FontStyle.Normal, TextAnchor.MiddleLeft);
            medium = MakeStyle(15, FontStyle.Bold, TextAnchor.MiddleLeft);
            large = MakeStyle(25, FontStyle.Bold, TextAnchor.MiddleLeft);
            centered = MakeStyle(13, FontStyle.Bold, TextAnchor.MiddleCenter);
            hero = new GUIStyle(large) { fontSize = 36 };
            lobbyTitle = new GUIStyle(large) { fontSize = 22, clipping = TextClipping.Clip };
            lobbyTitleCentered = new GUIStyle(lobbyTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18
            };
            lobbyCaption = new GUIStyle(small) { fontSize = 11, clipping = TextClipping.Clip };
            lobbyCaptionCentered = new GUIStyle(lobbyCaption)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                normal = { textColor = new Color(0.63f, 0.87f, 0.92f) }
            };
            lobbyCentered = new GUIStyle(small)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                clipping = TextClipping.Clip,
                wordWrap = false
            };
            lobbyButton = new GUIStyle(centered)
            {
                fontSize = 12,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white },
                hover = { textColor = Cyan },
                active = { textColor = Amber }
            };
            lobbyButtonLeft = new GUIStyle(lobbyButton)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11
            };
            lobbyPrimaryButton = new GUIStyle(lobbyButton)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(0.08f, 0.06f, 0.02f) },
                hover = { textColor = Color.white }
            };
            lobbySectionTitle = new GUIStyle(medium)
            {
                fontSize = 12,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            noAmmo = new GUIStyle(small);
            noAmmo.normal.textColor = DamageFeedbackSystem.HeadshotColor;
            lowAmmo = new GUIStyle(small);
            lowAmmo.normal.textColor = Amber;
            damageBody = new GUIStyle(large) { alignment = TextAnchor.MiddleCenter, fontSize = 27 };
            damageHeadshot = new GUIStyle(large) { alignment = TextAnchor.MiddleCenter, fontSize = 31 };
            eliminationRight = new GUIStyle(small) { alignment = TextAnchor.MiddleRight };
            eliminationCenter = new GUIStyle(medium) { alignment = TextAnchor.MiddleCenter };
            pauseTitle = new GUIStyle(large) { alignment = TextAnchor.MiddleCenter };
            editorFrame = new GUIStyle(small)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            editorFrameCompact = new GUIStyle(editorFrame) { fontSize = 9 };
        }

        private static GUIStyle MakeStyle(int size, FontStyle style, TextAnchor alignment) => new(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = style,
            alignment = alignment,
            normal = { textColor = Color.white },
            wordWrap = true
        };

        private static Texture2D CreateRingTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = (size - 1) * 0.5f;
            var radius = center - 2f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                texture.SetPixel(x, y, Mathf.Abs(distance - radius) < 1.5f ? Color.white : Color.clear);
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateDiscTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = (size - 1) * 0.5f;
            var radius = center - 2f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                var alpha = 1f - Mathf.Clamp01((distance - radius + 2f) * 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            return texture;
        }

        private void CaptureMinimapTexture()
        {
            if (match == null || minimapTexture != null) return;
            const int resolution = 256;
            if (!ScopeController.RenderTexturesAvailable)
            {
                minimapTexture = CreateHeadlessMinimapTexture(resolution);
                return;
            }
            var cameraObject = new GameObject("Minimap Capture Camera");
            var captureCamera = cameraObject.AddComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.orthographic = true;
            var area = PlayableArea.Bounds;
            var mapSpan = Mathf.Max(area.size.x, area.size.z);
            captureCamera.orthographicSize = mapSpan * 0.515f;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = new Color(0.06f, 0.11f, 0.08f);
            captureCamera.allowHDR = false;
            captureCamera.allowMSAA = false;
            captureCamera.nearClipPlane = 0.3f;
            captureCamera.farClipPlane = Mathf.Max(500f, area.size.y + mapSpan * 0.3f);
            var captureHeight = area.max.y + Mathf.Max(120f, mapSpan * 0.12f);
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(area.center.x, captureHeight, area.center.z), Quaternion.Euler(90f, 0f, 0f));
            var renderTexture = RenderTexture.GetTemporary(resolution, resolution, 16, RenderTextureFormat.ARGB32);
            captureCamera.targetTexture = renderTexture;
            captureCamera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            minimapTexture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false)
            {
                name = "Bermuda Minimap",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            minimapTexture.ReadPixels(new Rect(0f, 0f, resolution, resolution), 0, 0);
            minimapTexture.Apply(false, false);
            RenderTexture.active = previous;
            captureCamera.targetTexture = null;
            RenderTexture.ReleaseTemporary(renderTexture);
            Destroy(cameraObject);
        }

        private static Texture2D CreateHeadlessMinimapTexture(int resolution)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false)
            {
                name = "Bermuda Minimap Headless",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[resolution * resolution];
            var background = new Color32(15, 28, 20, 255);
            for (var index = 0; index < pixels.Length; index++) pixels[index] = background;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private void OnDestroy()
        {
            if (minimapTexture != null) Destroy(minimapTexture);
            if (white != null) Destroy(white);
            if (zoneRing != null) Destroy(zoneRing);
        }
    }
}
