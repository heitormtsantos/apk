using NUnit.Framework;
using UnityEngine;

namespace BattleRoyale.Tests
{
    public sealed class BattleRoyaleRulesTests
    {
        [TestCase(0, RankedTier.Bronze)]
        [TestCase(399, RankedTier.Bronze)]
        [TestCase(400, RankedTier.Silver)]
        [TestCase(800, RankedTier.Gold)]
        [TestCase(1300, RankedTier.Platinum)]
        [TestCase(1900, RankedTier.Diamond)]
        [TestCase(2600, RankedTier.Master)]
        public void RankedTiersUseExactRatingThresholds(int rating, RankedTier expected)
        {
            Assert.That(RankedProgression.TierForRating(rating), Is.EqualTo(expected));
            Assert.That(RankedProgression.ThresholdForTier(expected), Is.LessThanOrEqualTo(rating));
        }

        [Test]
        public void RankedScoreCalculatesAllComponentsCapsEntryAndDelta()
        {
            var score = RankedProgression.CalculateScore(1, 20, 10, 3000, 6, RankedTier.Master);

            Assert.That(score.PlacementPoints, Is.EqualTo(30));
            Assert.That(score.KillPoints, Is.EqualTo(42));
            Assert.That(score.DamagePoints, Is.EqualTo(10));
            Assert.That(score.RevivePoints, Is.EqualTo(18));
            Assert.That(score.VictoryBonus, Is.EqualTo(12));
            Assert.That(score.EntryCost, Is.EqualTo(15));
            Assert.That(score.UnclampedDelta, Is.EqualTo(97));
            Assert.That(score.Delta, Is.EqualTo(70));
            Assert.That(new RankedScoreBreakdown(-50, 0, 0, 0, 0, 0).Delta, Is.EqualTo(-30));
        }

        [TestCase(RankedTier.Bronze, 0)]
        [TestCase(RankedTier.Silver, 3)]
        [TestCase(RankedTier.Gold, 6)]
        [TestCase(RankedTier.Platinum, 9)]
        [TestCase(RankedTier.Diamond, 12)]
        [TestCase(RankedTier.Master, 15)]
        public void RankedEntryCostMatchesEveryTier(RankedTier tier, int expected)
        {
            Assert.That(RankedProgression.EntryCostForTier(tier), Is.EqualTo(expected));
        }

        [TestCase(2, 20, 18)]
        [TestCase(5, 20, 18)]
        [TestCase(6, 20, 8)]
        [TestCase(10, 20, 8)]
        [TestCase(11, 20, -10)]
        public void RankedPlacementBandsUseTeamCount(int placement, int teams, int expected)
        {
            Assert.That(RankedProgression.CalculateScore(placement, teams, 0, 0, 0,
                RankedTier.Bronze).PlacementPoints, Is.EqualTo(expected));
        }

        [TestCase(0, 0, 1, 1, 1)]
        [TestCase(99, 4, 4, 4, 0)]
        [TestCase(-8, 8, 1, 8, 1)]
        public void RankedApplyNormalizesInputOnceForScoreStatsAndResult(int placement, int teamCount,
            int expectedPlacement, int expectedTeamCount, int expectedVictories)
        {
            var key = RankedProgression.KeyForMode(BRMatchMode.Duo);
            var hadValue = PlayerPrefs.HasKey(key);
            var original = PlayerPrefs.GetString(key, string.Empty);
            try
            {
                PlayerPrefs.DeleteKey(key);
                var result = RankedProgression.ApplyResult(BRMatchMode.Duo, BRPlaylist.Ranked,
                    placement, teamCount, -7, -900, -3);
                var profile = RankedProgression.Load(BRMatchMode.Duo);
                var expectedScore = RankedProgression.CalculateScore(expectedPlacement,
                    expectedTeamCount, 0, 0, 0, RankedTier.Bronze);

                Assert.That(result.Placement, Is.EqualTo(expectedPlacement));
                Assert.That(result.TeamCount, Is.EqualTo(expectedTeamCount));
                Assert.That(result.Kills, Is.Zero);
                Assert.That(result.Damage, Is.Zero);
                Assert.That(result.Revives, Is.Zero);
                Assert.That(result.Breakdown.Delta, Is.EqualTo(expectedScore.Delta));
                Assert.That(profile.victories, Is.EqualTo(expectedVictories));
                Assert.That(profile.bestPlacement, Is.EqualTo(expectedPlacement));
                Assert.That(profile.kills, Is.Zero);
                Assert.That(profile.damage, Is.Zero);
                Assert.That(profile.revives, Is.Zero);
            }
            finally
            {
                if (hadValue) PlayerPrefs.SetString(key, original);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void MinimapPolicyPreservesExistingEnemyRevealRule()
        {
            Assert.That(MinimapVisibilityPolicy.ShouldShow(false, true, 999f,
                float.PositiveInfinity), Is.True, "Allies must always be visible");
            Assert.That(MinimapVisibilityPolicy.ShouldShow(false, false, 2.8f,
                48f * 48f), Is.True, "Recent nearby enemies keep the existing reveal");
            Assert.That(MinimapVisibilityPolicy.ShouldShow(false, false, 2.81f,
                48f * 48f), Is.False, "Enemies that did not fire recently stay hidden");
            Assert.That(MinimapVisibilityPolicy.ShouldShow(false, false, 1f,
                48.01f * 48.01f), Is.False, "Distant enemies stay hidden even after firing");
        }

        [Test]
        public void HudTextHelpersExposeRosterBleedoutAndRealStagingCounts()
        {
            var active = MatchHudText.TeamRosterLine("Echo", "ATIVO", 17.4f, 0f);
            var downed = MatchHudText.TeamRosterLine("Nova", "DERRUBADO", 8.6f, 13.2f);
            var staging = MatchHudText.StagingStatus(17, 18, 6, 4.6f);

            Assert.That(active, Does.Contain("Echo"));
            Assert.That(active, Does.Contain("17 m"));
            Assert.That(active, Does.Contain("ATIVO"));
            Assert.That(downed, Does.Contain("9 m"));
            Assert.That(downed, Does.Contain("DERRUBADO 13s"));
            Assert.That(staging, Does.Contain("17/18"));
            Assert.That(staging, Does.Contain("6 EQUIPES"));
            Assert.That(staging, Does.Contain("5s"));
        }

        [Test]
        public void RankedResultTextContainsEveryBreakdownComponent()
        {
            var key = RankedProgression.KeyForMode(BRMatchMode.Trio);
            var hadValue = PlayerPrefs.HasKey(key);
            var original = PlayerPrefs.GetString(key, string.Empty);
            try
            {
                PlayerPrefs.DeleteKey(key);
                var result = RankedProgression.ApplyResult(BRMatchMode.Trio, BRPlaylist.Ranked,
                    1, 6, 2, 450, 1);
                var text = MatchManager.BuildResultText(result.Placement, result.Kills,
                    result.Damage, result.Revives, result, 90f);

                Assert.That(text, Does.Contain("Colocação +30"));
                Assert.That(text, Does.Contain("Kills +14"));
                Assert.That(text, Does.Contain("Dano +3"));
                Assert.That(text, Does.Contain("Revives +6"));
                Assert.That(text, Does.Contain("Vitória +12"));
                Assert.That(text, Does.Contain("Entrada -0"));
                Assert.That(text, Does.Contain("DELTA +65"));
                Assert.That(text, Does.Contain("BRONZE"));
                Assert.That(text, Does.Contain("65 RP"));
            }
            finally
            {
                if (hadValue) PlayerPrefs.SetString(key, original);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void CasualResultDoesNotCreateOrMutateRankedPersistence()
        {
            var key = RankedProgression.KeyForMode(BRMatchMode.Duo);
            var hadValue = PlayerPrefs.HasKey(key);
            var original = PlayerPrefs.GetString(key, string.Empty);
            try
            {
                PlayerPrefs.DeleteKey(key);
                Assert.That(RankedProgression.ApplyResult(BRMatchMode.Duo, BRPlaylist.Casual,
                    1, 10, 5, 900, 2), Is.Null);
                Assert.That(PlayerPrefs.HasKey(key), Is.False);
            }
            finally
            {
                if (hadValue) PlayerPrefs.SetString(key, original);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void RankedProfilesSavePerModeAndClampCorruptValues()
        {
            var soloKey = RankedProgression.KeyForMode(BRMatchMode.Solo);
            var trioKey = RankedProgression.KeyForMode(BRMatchMode.Trio);
            var hadSolo = PlayerPrefs.HasKey(soloKey);
            var oldSolo = PlayerPrefs.GetString(soloKey, string.Empty);
            var hadTrio = PlayerPrefs.HasKey(trioKey);
            var oldTrio = PlayerPrefs.GetString(trioKey, string.Empty);
            try
            {
                PlayerPrefs.SetString(soloKey,
                    "{\"version\":-4,\"rating\":-50,\"matches\":-2,\"victories\":8,\"kills\":-1,\"damage\":-1,\"revives\":-1,\"bestPlacement\":-3}");
                PlayerPrefs.SetString(trioKey, "not-json");
                var solo = RankedProgression.Load(BRMatchMode.Solo);
                var trio = RankedProgression.Load(BRMatchMode.Trio);

                Assert.That(solo.version, Is.EqualTo(RankedProgression.CurrentVersion));
                Assert.That(solo.rating, Is.Zero);
                Assert.That(solo.matches, Is.Zero);
                Assert.That(solo.victories, Is.Zero);
                Assert.That(solo.kills, Is.Zero);
                Assert.That(solo.damage, Is.Zero);
                Assert.That(solo.revives, Is.Zero);
                Assert.That(solo.bestPlacement, Is.Zero);
                Assert.That(trio.rating, Is.Zero);

                solo.rating = 812;
                RankedProgression.Save(BRMatchMode.Solo, solo);
                Assert.That(RankedProgression.Load(BRMatchMode.Solo).rating, Is.EqualTo(812));
                Assert.That(RankedProgression.Load(BRMatchMode.Trio).rating, Is.Zero);
            }
            finally
            {
                if (hadSolo) PlayerPrefs.SetString(soloKey, oldSolo);
                else PlayerPrefs.DeleteKey(soloKey);
                if (hadTrio) PlayerPrefs.SetString(trioKey, oldTrio);
                else PlayerPrefs.DeleteKey(trioKey);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void RankedApplyPersistsAllStatsAndBestPlacement()
        {
            var key = RankedProgression.KeyForMode(BRMatchMode.Squad);
            var hadValue = PlayerPrefs.HasKey(key);
            var original = PlayerPrefs.GetString(key, string.Empty);
            try
            {
                PlayerPrefs.DeleteKey(key);
                RankedProgression.ApplyResult(BRMatchMode.Squad, BRPlaylist.Ranked, 4, 5, 2, 375, 1);
                RankedProgression.ApplyResult(BRMatchMode.Squad, BRPlaylist.Ranked, 1, 5, 3, 600, 2);
                var profile = RankedProgression.Load(BRMatchMode.Squad);

                Assert.That(profile.matches, Is.EqualTo(2));
                Assert.That(profile.victories, Is.EqualTo(1));
                Assert.That(profile.kills, Is.EqualTo(5));
                Assert.That(profile.damage, Is.EqualTo(975));
                Assert.That(profile.revives, Is.EqualTo(3));
                Assert.That(profile.bestPlacement, Is.EqualTo(1));
            }
            finally
            {
                if (hadValue) PlayerPrefs.SetString(key, original);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void RankedMatchManagerAppliesResultOnlyOnce()
        {
            var key = RankedProgression.KeyForMode(BRMatchMode.Solo);
            var hadValue = PlayerPrefs.HasKey(key);
            var original = PlayerPrefs.GetString(key, string.Empty);
            var root = new GameObject("Ranked Once Manager");
            var player = CreateParticipant("Ranked Once Player", true, 1);
            try
            {
                PlayerPrefs.DeleteKey(key);
                var manager = root.AddComponent<MatchManager>();
                var config = new BRGameConfig { selectedMode = BRMatchMode.Solo, playlist = BRPlaylist.Ranked };
                typeof(MatchManager).GetField("config",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(manager, config);
                typeof(MatchManager).GetField("initialTeamCount",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(manager, 20);
                typeof(MatchManager).GetProperty("Player")?.SetValue(manager, player);
                typeof(MatchManager).GetProperty("State")?.SetValue(manager, MatchState.Active);
                ManagerParticipants(manager).Add(player);

                InvokePrivate(manager, "Complete");
                InvokePrivate(manager, "Complete");

                Assert.That(manager.LastRankedResult, Is.Not.Null);
                Assert.That(RankedProgression.Load(BRMatchMode.Solo).matches, Is.EqualTo(1));
                Assert.That(manager.ResultText, Does.Contain("DELTA"));
            }
            finally
            {
                ManagerParticipants(root.GetComponent<MatchManager>()).Clear();
                DestroyParticipants(player);
                Object.DestroyImmediate(root);
                if (hadValue) PlayerPrefs.SetString(key, original);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [TestCase(BRMatchMode.Solo, 20, 1, 20)]
        [TestCase(BRMatchMode.Duo, 20, 2, 10)]
        [TestCase(BRMatchMode.Trio, 18, 3, 6)]
        [TestCase(BRMatchMode.Squad, 20, 4, 5)]
        public void MatchModeSettersUseExactLobbyDefaults(BRMatchMode mode, int participants,
            int teamSize, int teams)
        {
            var root = new GameObject("Mode Setter Manager");
            try
            {
                var manager = root.AddComponent<MatchManager>();
                Assert.That(manager.SetMode(mode), Is.True);
                Assert.That(manager.SetPlaylist(BRPlaylist.Ranked), Is.True);
                Assert.That(manager.Mode, Is.EqualTo(mode));
                Assert.That(manager.Playlist, Is.EqualTo(BRPlaylist.Ranked));
                Assert.That(manager.TeamSize, Is.EqualTo(teamSize));
                Assert.That(manager.TeamCount, Is.EqualTo(teams));
                var config = (BRGameConfig)typeof(MatchManager).GetField("config",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(manager);
                Assert.That(config.participantCount, Is.EqualTo(participants));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LobbySelectionLayoutAvoidsSettingsOnDesktopAndMobile()
        {
            foreach (var safe in new[] { new Rect(0f, 0f, 1280f, 720f), new Rect(0f, 0f, 360f, 640f) })
            {
                var selection = MatchLobbyLayout.Selection(safe);
                var settings = MatchSettingsLayout.Lobby(safe, safe.width < 500f);
                foreach (var mode in selection.Modes)
                {
                    Assert.That(safe.Contains(mode.min) && safe.Contains(mode.max), Is.True);
                    Assert.That(mode.Overlaps(settings.LookRow), Is.False);
                    Assert.That(mode.Overlaps(settings.FireDragRow), Is.False);
                }
                foreach (var playlist in selection.Playlists)
                    Assert.That(playlist.Overlaps(settings.LookRow), Is.False);
                Assert.That(selection.RankedSummary.Overlaps(settings.LookRow), Is.False);
                var roster = MatchLobbyLayout.TeamRoster(safe, safe.width < 500f, 3);
                Assert.That(roster.xMin, Is.GreaterThanOrEqualTo(safe.xMin));
                Assert.That(roster.xMax, Is.LessThanOrEqualTo(safe.xMax));
                Assert.That(roster.yMin, Is.GreaterThanOrEqualTo(safe.yMin));
                Assert.That(roster.yMax, Is.LessThanOrEqualTo(safe.yMax));
            }
        }

        [TestCase(0f, 0f, 1280f, 720f, 4)]
        [TestCase(40f, 12f, 760f, 360f, 4)]
        [TestCase(0f, 18f, 360f, 622f, 3)]
        [TestCase(0f, 0f, 1920f, 1080f, 4)]
        [TestCase(0f, 0f, 960f, 540f, 3)]
        [TestCase(0f, 0f, 2340f, 1080f, 4)]
        [TestCase(24f, 12f, 912f, 516f, 2)]
        [TestCase(96f, 28f, 2148f, 1024f, 4)]
        public void BalancedLobbyLayoutContainsControlsAndProtectsCharacterFocus(float x, float y,
            float width, float height, int teamSize)
        {
            var safe = new Rect(x, y, width, height);
            var layout = MatchLobbyLayout.Balanced(safe, teamSize);
            var controls = new System.Collections.Generic.List<Rect>
            {
                layout.SettingsButton, layout.HudButton, layout.PlayButton,
                layout.PlaylistSelector, layout.TeamSizeSelector
            };
            controls.AddRange(layout.Currencies);
            controls.AddRange(layout.Services);
            controls.AddRange(layout.Navigation);
            controls.AddRange(layout.TeamSlots);

            Assert.That(MatchAuxiliaryLayout.Contains(safe, layout.Identity), Is.True);
            Assert.That(MatchAuxiliaryLayout.Contains(safe, layout.TeamPanel), Is.True);
            Assert.That(MatchAuxiliaryLayout.Contains(safe, layout.MatchDock), Is.True);
            Assert.That(MatchAuxiliaryLayout.Contains(safe, layout.CharacterFocus), Is.True);
            Assert.That(MatchAuxiliaryLayout.Contains(safe, layout.ServiceDrawer), Is.True);
            Assert.That(MatchAuxiliaryLayout.Contains(safe, layout.Promotion), Is.True);
            Assert.That(layout.Services.Length, Is.EqualTo(5));
            Assert.That(layout.Navigation.Length, Is.EqualTo(3));
            foreach (var control in controls)
            {
                Assert.That(MatchAuxiliaryLayout.Contains(safe, control), Is.True,
                    $"Control {control} escaped {safe}");
                Assert.That(control.height, Is.GreaterThanOrEqualTo(MatchLobbyLayout.MinimumTouchHeight));
            }

            foreach (var navigation in layout.Navigation)
            {
                Assert.That(navigation.Overlaps(layout.TeamPanel), Is.False);
                Assert.That(navigation.Overlaps(layout.MatchDock), Is.False);
                Assert.That(navigation.Overlaps(layout.CharacterFocus), Is.False);
            }
            foreach (var service in layout.Services)
            {
                Assert.That(service.Overlaps(layout.TeamPanel), Is.False);
                Assert.That(service.Overlaps(layout.MatchDock), Is.False);
                Assert.That(service.Overlaps(layout.CharacterFocus), Is.False);
            }
            Assert.That(layout.TeamPanel.Overlaps(layout.MatchDock), Is.False);
            Assert.That(layout.TeamPanel.Overlaps(layout.CharacterFocus), Is.False);
            Assert.That(layout.MatchDock.Overlaps(layout.CharacterFocus), Is.False);
            if (width >= height)
                Assert.That(layout.ServiceDrawer.Overlaps(layout.CharacterFocus), Is.False);
            Assert.That(layout.PlayButton.Overlaps(layout.MapSummary), Is.False);
            Assert.That(layout.PlaylistSelector.Overlaps(layout.TeamSizeSelector), Is.False);
        }

        [TestCase(0f, 0f, 1280f, 720f)]
        [TestCase(40f, 12f, 760f, 360f)]
        [TestCase(0f, 18f, 360f, 622f)]
        public void HangarLobbyUtilityClustersDoNotOverlap(float x, float y, float width, float height)
        {
            var safe = new Rect(x, y, width, height);
            var layout = MatchLobbyLayout.Balanced(safe, 4);
            Assert.That(layout.Currencies[0].Overlaps(layout.Currencies[1]), Is.False);
            foreach (var currency in layout.Currencies)
            {
                Assert.That(currency.Overlaps(layout.SettingsButton), Is.False);
                Assert.That(currency.Overlaps(layout.HudButton), Is.False);
            }
            Assert.That(layout.SettingsButton.Overlaps(layout.HudButton), Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void BalancedLobbyTeamPanelProvidesFourExplicitSlots(int teamSize)
        {
            var layout = MatchLobbyLayout.Balanced(new Rect(0f, 0f, 1280f, 720f), teamSize);
            for (var i = 0; i < 4; i++)
            {
                Assert.That(layout.TeamSlots[i].Overlaps(layout.TeamPanel), Is.True);
                Assert.That(layout.TeamSlots[i].height,
                    Is.GreaterThanOrEqualTo(MatchLobbyLayout.MinimumTouchHeight));
            }
        }

        [Test]
        public void LobbyIconCatalogHasUniqueStableResourceNames()
        {
            var names = new System.Collections.Generic.HashSet<string>();
            for (var i = 0; i < LobbyIconCatalog.Count; i++)
            {
                var name = LobbyIconCatalog.ResourceName((LobbyIconId)i);
                Assert.That(name, Is.Not.Empty);
                Assert.That(names.Add(name), Is.True, $"Duplicate lobby icon resource: {name}");
            }
        }

        [Test]
        public void CombatHudIconCatalogHasEveryTransparentPngImportedAsSprite()
        {
            Assert.That(HudIconCatalog.Count, Is.EqualTo(9));
            var names = new System.Collections.Generic.HashSet<string>();
            for (var i = 0; i < HudIconCatalog.Count; i++)
            {
                var id = (HudIconId)i;
                Assert.That(names.Add(HudIconCatalog.ResourceName(id)), Is.True);
                Assert.That(HudIconCatalog.Load(id), Is.Not.Null,
                    $"Missing combat HUD sprite {HudIconCatalog.ResourcePath(id)}");
            }
        }

        [Test]
        public void EveryLobbyIconLoadsAsOneTwentyEightPixelSprite()
        {
            for (var i = 0; i < LobbyIconCatalog.Count; i++)
            {
                var id = (LobbyIconId)i;
                var sprite = LobbyIconCatalog.Load(id);
                Assert.That(sprite, Is.Not.Null, $"Missing sprite {LobbyIconCatalog.ResourcePath(id)}");
                Assert.That(sprite.texture.width, Is.EqualTo(128));
                Assert.That(sprite.texture.height, Is.EqualTo(128));
            }
        }

        [TestCase(32f, 24f, 1280f, 720f, false)]
        [TestCase(0f, 18f, 360f, 622f, true)]
        [TestCase(40f, 12f, 760f, 360f, true)]
        public void AllAuxiliaryHudPanelsStayInsideSafeArea(float x, float y, float width,
            float height, bool mobile)
        {
            var safe = new Rect(x, y, width, height);
            var panels = new[]
            {
                MatchAuxiliaryLayout.LobbyPanel(safe),
                MatchAuxiliaryLayout.StagingStatus(safe),
                MatchAuxiliaryLayout.TeamRoster(safe, mobile, 3),
                MatchAuxiliaryLayout.DeathLootPanel(safe, mobile, 8),
                MatchAuxiliaryLayout.ResultPanel(safe, false),
                MatchAuxiliaryLayout.ResultPanel(safe, true),
                MatchAuxiliaryLayout.PausePanel(safe),
                MatchAuxiliaryLayout.DeploymentTelemetry(safe)
            };
            foreach (var panel in panels)
                Assert.That(MatchAuxiliaryLayout.Contains(safe, panel), Is.True,
                    $"Panel {panel} escaped safe area {safe}");

            var selection = MatchLobbyLayout.Selection(safe);
            foreach (var rect in selection.Modes)
                Assert.That(MatchAuxiliaryLayout.Contains(safe, rect), Is.True);
            foreach (var rect in selection.Playlists)
                Assert.That(MatchAuxiliaryLayout.Contains(safe, rect), Is.True);
            Assert.That(MatchAuxiliaryLayout.Contains(safe, selection.Summary), Is.True);
            Assert.That(MatchAuxiliaryLayout.Contains(safe, selection.RankedSummary), Is.True);

            var lobbySettings = MatchSettingsLayout.Lobby(safe, mobile);
            AssertSettingsRectsInside(safe, lobbySettings);
            var pausePanel = MatchAuxiliaryLayout.PausePanel(safe);
            AssertSettingsRectsInside(pausePanel, MatchSettingsLayout.Pause(pausePanel));
        }

        [Test]
        public void MobileDeathLootUsesCompactSidePanel()
        {
            var safe = new Rect(0f, 0f, 1280f, 720f);
            var panel = MatchAuxiliaryLayout.DeathLootPanel(safe, true, 8);

            Assert.That(panel.width, Is.LessThanOrEqualTo(420f));
            Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(safe.center.x));
            Assert.That(MatchAuxiliaryLayout.Contains(safe, panel), Is.True);
        }

        [Test]
        public void TeamRosterUsesRequiredStateLabels()
        {
            var active = CreateParticipant("Roster Active", false, 1);
            var downed = CreateParticipant("Roster Downed", false, 1);
            var eliminated = CreateParticipant("Roster Eliminated", false, 1);
            try
            {
                downed.SetPhase(ParticipantPhase.Downed);
                eliminated.SetPhase(ParticipantPhase.Eliminated);
                Assert.That(MatchHUD.TeamStateLabel(active), Is.EqualTo("ATIVO"));
                Assert.That(MatchHUD.TeamStateLabel(downed), Is.EqualTo("DERRUBADO"));
                Assert.That(MatchHUD.TeamStateLabel(eliminated), Is.EqualTo("ELIMINADO"));
            }
            finally
            {
                DestroyParticipants(active, downed, eliminated);
            }
        }

        [Test]
        public void TeamRosterPopulationReusesCallerBuffer()
        {
            var hudObject = new GameObject("Roster Buffer HUD");
            var hud = hudObject.AddComponent<MatchHUD>();
            var player = CreateParticipant("Roster Buffer Player", true, 40);
            var teammate = CreateParticipant("Roster Buffer Ally", false, 40);
            var enemy = CreateParticipant("Roster Buffer Enemy", false, 41);
            var participants = new System.Collections.Generic.List<BRParticipant>
                { player, teammate, enemy };
            var buffer = (System.Collections.Generic.List<BRParticipant>)hud.TeamRosterBuffer;
            try
            {
                var first = MatchHUD.PopulateTeamRosterBuffer(buffer, player, participants);
                Assert.That(first, Is.SameAs(buffer));
                Assert.That(first, Is.EqualTo(new[] { teammate }));

                participants.Remove(teammate);
                var second = MatchHUD.PopulateTeamRosterBuffer(buffer, player, participants);
                Assert.That(second, Is.SameAs(first));
                Assert.That(second, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(hudObject);
                DestroyParticipants(player, teammate, enemy);
            }
        }

        [Test]
        public void RuntimeSmokeHealingRequiresCompletedHealthIncrease()
        {
            Assert.That(RuntimeSmokeAcceptance.HealingIncreased(true, 60f, 100f, false), Is.True);
            Assert.That(RuntimeSmokeAcceptance.HealingIncreased(true, 60f, 55f, false), Is.False);
            Assert.That(RuntimeSmokeAcceptance.HealingIncreased(false, 60f, 100f, false), Is.False);
            Assert.That(RuntimeSmokeAcceptance.HealingIncreased(true, 60f, 100f, true), Is.False);
        }

        [Test]
        public void RuntimeSmokeIndoorFixtureRequiresExactKnownCollider()
        {
            Assert.That(RuntimeSmokeAcceptance.IsIndoorFixturePath(RuntimeSmokeAcceptance.IndoorFixturePath), Is.True);
            Assert.That(RuntimeSmokeAcceptance.IsIndoorFixturePath(
                "User Supplied Clock Tower Map/root/GLTF_SceneRootNode/Plane_0/Object_5"), Is.False);
            Assert.That(RuntimeSmokeAcceptance.IsIndoorFixturePath(null), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void VaultArcFinishesWithFiniteZeroOffset(bool compact)
        {
            var offset = BRCharacterMotor.VaultArcOffset(1f, compact);
            Assert.That(float.IsFinite(offset), Is.True);
            Assert.That(offset, Is.EqualTo(0f).Within(0.000001f));
        }

        [Test]
        public void ScopeRenderTextureRejectsHeadlessGraphicsDevice()
        {
            Assert.That(ScopeController.CanUseRenderTexture(true,
                UnityEngine.Rendering.GraphicsDeviceType.Null), Is.False);
            Assert.That(ScopeController.CanUseRenderTexture(false,
                UnityEngine.Rendering.GraphicsDeviceType.Direct3D11), Is.False);
            Assert.That(ScopeController.CanUseRenderTexture(true,
                UnityEngine.Rendering.GraphicsDeviceType.Direct3D11), Is.True);
        }

        [Test]
        public void ArmorReducesBodyDamageByLevel()
        {
            Assert.That(HealthArmorSystem.VestMultiplier(0), Is.EqualTo(1f));
            Assert.That(HealthArmorSystem.VestMultiplier(2), Is.EqualTo(0.64f));
            Assert.That(HealthArmorSystem.VestMultiplier(3), Is.EqualTo(0.50f));
        }

        [Test]
        public void HelmetReducesHeadshotDamageByLevel()
        {
            Assert.That(HealthArmorSystem.HelmetMultiplier(1), Is.EqualTo(0.72f));
            Assert.That(HealthArmorSystem.HelmetMultiplier(3), Is.EqualTo(0.44f));
        }

        [Test]
        public void CompositePelletDamageUsesVestAndHelmetSeparately()
        {
            var target = new GameObject("Composite Damage Target");
            var health = target.AddComponent<HealthArmorSystem>();
            health.InitializeFull();
            health.EquipVest(2);
            health.EquipHelmet(1);

            var report = health.ApplyCompositeDamage(10f, 20f, null);

            Assert.That(report.FinalDamage, Is.EqualTo(20.8f).Within(0.01f));
            Assert.That(report.Headshot, Is.True);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void InventoryConsumesOnlyAvailableAmmo()
        {
            var obj = new GameObject("Inventory Test");
            var inventory = obj.AddComponent<InventorySystem>();
            inventory.AddAmmo(AmmoKind.Medium, 3);
            Assert.That(inventory.TryConsumeAmmo(AmmoKind.Medium, 2), Is.True);
            Assert.That(inventory.TryConsumeAmmo(AmmoKind.Medium, 2), Is.False);
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void InventoryQuantitativeCapsRejectInvalidInputAndRaiseChangedOnlyForMutation()
        {
            var obj = new GameObject("Quantitative Inventory Test");
            try
            {
                var inventory = obj.AddComponent<InventorySystem>();
                var changed = 0;
                inventory.Changed += () => changed++;
                var ammoInitial = inventory.AddAmmoQuantitative(AmmoKind.Medium, 239);
                var ammoPartial = inventory.TryAddAmmo(AmmoKind.Medium, 5);
                var ammoFull = inventory.TryAddAmmo(AmmoKind.Medium, 1);
                var ammoZero = inventory.TryAddAmmo(AmmoKind.Medium, 0);
                var ammoNegative = inventory.TryAddAmmo(AmmoKind.Medium, -4);
                var kitsInitial = inventory.AddMedKitsQuantitative(4);
                var kitsPartial = inventory.TryAddMedKits(3);
                var kitsFull = inventory.TryAddMedKits(1);

                Assert.That(ammoInitial.AcceptedAmount, Is.EqualTo(239));
                Assert.That(ammoPartial.AcceptedAmount, Is.EqualTo(1));
                Assert.That(ammoPartial.RemainingAmount, Is.EqualTo(4));
                Assert.That(ammoFull.Success, Is.False);
                Assert.That(ammoZero.Success, Is.False);
                Assert.That(ammoNegative.Success, Is.False);
                Assert.That(inventory.AmmoFor(AmmoKind.Medium), Is.EqualTo(InventorySystem.MaxAmmoPerKind));
                Assert.That(kitsInitial.AcceptedAmount, Is.EqualTo(4));
                Assert.That(kitsPartial.AcceptedAmount, Is.EqualTo(1));
                Assert.That(kitsPartial.RemainingAmount, Is.EqualTo(2));
                Assert.That(kitsFull.Success, Is.False);
                Assert.That(inventory.MedKits, Is.EqualTo(InventorySystem.MaxMedKits));
                Assert.That(changed, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void InventoryStartsUnarmed()
        {
            var obj = new GameObject("Unarmed Inventory Test");
            var inventory = obj.AddComponent<InventorySystem>();
            Assert.That(inventory.ActiveWeapon, Is.Null);
            Assert.That(inventory.Weapons, Is.Empty);
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void StabilizerCapsAtThreeAndReducesSpreadAndRecoil()
        {
            var obj = new GameObject("Attachment Inventory Test");
            var inventory = obj.AddComponent<InventorySystem>();
            Assert.That(inventory.TryAddStabilizer(2), Is.True);
            Assert.That(inventory.TryAddStabilizer(2), Is.True);
            Assert.That(inventory.TryAddStabilizer(), Is.False);
            Assert.That(inventory.StabilizerLevel, Is.EqualTo(3));
            Assert.That(inventory.SpreadMultiplier, Is.EqualTo(0.64f).Within(0.001f));
            Assert.That(inventory.RecoilMultiplier, Is.EqualTo(0.55f).Within(0.001f));
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void InventorySelectsRequestedWeaponSlot()
        {
            var obj = new GameObject("Weapon Slot Test");
            var inventory = obj.AddComponent<InventorySystem>();
            var first = ScriptableObject.CreateInstance<WeaponDefinition>();
            var second = ScriptableObject.CreateInstance<WeaponDefinition>();
            inventory.AddWeapon(first);
            inventory.AddWeapon(second);
            Assert.That(inventory.ActiveWeapon, Is.SameAs(second));
            Assert.That(inventory.SelectWeapon(0), Is.True);
            Assert.That(inventory.ActiveWeapon, Is.SameAs(first));
            Assert.That(inventory.SelectWeapon(0), Is.False);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void ThirdWeaponReturnsTheReplacedActiveWeapon()
        {
            var obj = new GameObject("Weapon Replacement Test");
            var inventory = obj.AddComponent<InventorySystem>();
            var first = ScriptableObject.CreateInstance<WeaponDefinition>();
            var second = ScriptableObject.CreateInstance<WeaponDefinition>();
            var third = ScriptableObject.CreateInstance<WeaponDefinition>();
            inventory.AddWeapon(first);
            inventory.AddWeapon(second);

            var replaced = inventory.AddWeapon(third);

            Assert.That(replaced, Is.SameAs(second));
            Assert.That(inventory.Weapons.Count, Is.EqualTo(2));
            Assert.That(inventory.ActiveWeapon, Is.SameAs(third));
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(third);
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void CollectedWeaponBecomesActiveWithFullMagazine()
        {
            var obj = new GameObject("Weapon Pickup Test");
            obj.AddComponent<CharacterController>();
            obj.AddComponent<BRCharacterMotor>();
            obj.AddComponent<HealthArmorSystem>();
            var inventory = obj.AddComponent<InventorySystem>();
            var weaponSystem = obj.AddComponent<WeaponSystem>();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.displayName = "Test SMG";
            weapon.magazineSize = 30;

            weaponSystem.EquipFromLoot(weapon);

            Assert.That(inventory.ActiveWeapon, Is.SameAs(weapon));
            Assert.That(weaponSystem.Magazine, Is.EqualTo(30));
            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void GroundWeaponPickupGrantsLoadedWeaponAndReserveAmmo()
        {
            var participant = CreateParticipant("Ground Loot Receiver", true);
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.displayName = "Ground Rifle";
            weapon.magazineSize = 30;
            weapon.ammoKind = AmmoKind.Medium;
            var pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var pickup = pickupObject.AddComponent<LootPickup>();
            pickup.ConfigureWeapon(weapon);

            Assert.That(pickup.LoadedMagazine, Is.EqualTo(30));
            Assert.That(pickup.ReserveBonus, Is.EqualTo(30));
            Assert.That(pickup.Collect(participant), Is.True);
            Assert.That(participant.Inventory.ActiveWeapon, Is.SameAs(weapon));
            Assert.That(participant.Weapon.Magazine, Is.EqualTo(30));
            Assert.That(participant.Inventory.AmmoFor(AmmoKind.Medium), Is.EqualTo(30));
            Assert.That(pickup.Collected, Is.True);

            Object.DestroyImmediate(pickupObject);
            Object.DestroyImmediate(participant.gameObject);
            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void GroundWeaponRepeatedSwapsPreserveMagazineAndTotalAmmoAfterInitialBonus()
        {
            var participant = CreateParticipant("Repeated Ground Swap Receiver", true);
            var first = ScriptableObject.CreateInstance<WeaponDefinition>();
            var second = ScriptableObject.CreateInstance<WeaponDefinition>();
            var ground = ScriptableObject.CreateInstance<WeaponDefinition>();
            var pickupObject = new GameObject("Repeated Ground Weapon Pickup");
            try
            {
                first.displayName = "First";
                first.magazineSize = 20;
                first.ammoKind = AmmoKind.Medium;
                second.displayName = "Second";
                second.magazineSize = 12;
                second.ammoKind = AmmoKind.Medium;
                ground.displayName = "Ground";
                ground.magazineSize = 30;
                ground.ammoKind = AmmoKind.Medium;
                participant.Weapon.EquipInitial(first);
                participant.Weapon.EquipInitial(second);
                SetMagazine(participant.Weapon, first, 7);
                SetMagazine(participant.Weapon, second, 5);
                participant.Inventory.AddAmmo(AmmoKind.Medium, 11);
                var pickup = pickupObject.AddComponent<LootPickup>();
                pickup.ConfigureWeapon(ground);
                var initialTotal = TotalAmmo(participant, pickup, AmmoKind.Medium);

                Assert.That(initialTotal, Is.EqualTo(83));
                Assert.That(pickup.CollectQuantitative(participant).Success, Is.True);
                Assert.That(pickup.Weapon, Is.SameAs(second));
                Assert.That(pickup.LoadedMagazine, Is.EqualTo(5));
                Assert.That(pickup.ReserveBonus, Is.Zero);
                Assert.That(TotalAmmo(participant, pickup, AmmoKind.Medium), Is.EqualTo(initialTotal));

                Assert.That(pickup.CollectQuantitative(participant).Success, Is.True);
                Assert.That(pickup.Weapon, Is.SameAs(ground));
                Assert.That(pickup.LoadedMagazine, Is.EqualTo(30));
                Assert.That(pickup.ReserveBonus, Is.Zero);
                Assert.That(participant.Weapon.MagazineFor(second), Is.EqualTo(5));
                Assert.That(TotalAmmo(participant, pickup, AmmoKind.Medium), Is.EqualTo(initialTotal));

                Assert.That(pickup.CollectQuantitative(participant).Success, Is.True);
                Assert.That(pickup.Weapon, Is.SameAs(second));
                Assert.That(pickup.LoadedMagazine, Is.EqualTo(5));
                Assert.That(pickup.ReserveBonus, Is.Zero);
                Assert.That(participant.Weapon.MagazineFor(ground), Is.EqualTo(30));
                Assert.That(TotalAmmo(participant, pickup, AmmoKind.Medium), Is.EqualTo(initialTotal));
            }
            finally
            {
                Object.DestroyImmediate(pickupObject);
                DestroyParticipants(participant);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(ground);
            }
        }

        [Test]
        public void GroundWeaponWithoutSwapSpawnsRegisteredAmmoForRejectedReserveBonus()
        {
            var spawnerObject = new GameObject("No Swap Overflow Spawner");
            var participant = CreateParticipant("No Swap Overflow Receiver", true);
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                var spawner = spawnerObject.AddComponent<LootSpawner>();
                participant.Inventory.AddAmmo(AmmoKind.Medium, 230);
                weapon.displayName = "Overflow Rifle";
                weapon.magazineSize = 30;
                weapon.ammoKind = AmmoKind.Medium;
                var pickup = AddTestPickup(spawner, "No Swap Weapon", value => value.ConfigureWeapon(weapon));
                var totalBefore = TotalSpawnerAmmo(participant, spawner, AmmoKind.Medium);

                var result = pickup.CollectQuantitative(participant);
                var overflow = FindPickup(spawner, LootKind.Ammo);

                Assert.That(result.Success, Is.True);
                Assert.That(participant.Weapon.MagazineFor(weapon), Is.EqualTo(30));
                Assert.That(participant.Inventory.AmmoFor(AmmoKind.Medium), Is.EqualTo(240));
                Assert.That(pickup.Collected, Is.True);
                Assert.That(pickup.gameObject.activeSelf, Is.False);
                Assert.That(overflow, Is.Not.Null);
                Assert.That(overflow.Amount, Is.EqualTo(20));
                Assert.That(overflow.AmmoKind, Is.EqualTo(AmmoKind.Medium));
                Assert.That(overflow.gameObject.activeSelf, Is.True);
                Assert.That(spawner.FindNearestAmmo(overflow.transform.position, 1f, AmmoKind.Medium),
                    Is.SameAs(overflow));
                Assert.That(TotalSpawnerAmmo(participant, spawner, AmmoKind.Medium), Is.EqualTo(totalBefore));
            }
            finally
            {
                Object.DestroyImmediate(spawnerObject);
                DestroyParticipants(participant);
                Object.DestroyImmediate(weapon);
            }
        }

        [Test]
        public void GroundWeaponSwapKeepsExactReturnedMagazineAndRegistersBonusOverflow()
        {
            var spawnerObject = new GameObject("Swap Overflow Spawner");
            var participant = CreateParticipant("Swap Overflow Receiver", true);
            var first = ScriptableObject.CreateInstance<WeaponDefinition>();
            var second = ScriptableObject.CreateInstance<WeaponDefinition>();
            var incoming = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                var spawner = spawnerObject.AddComponent<LootSpawner>();
                foreach (var weapon in new[] { first, second, incoming })
                {
                    weapon.magazineSize = 30;
                    weapon.ammoKind = AmmoKind.Medium;
                }
                first.displayName = "Swap First";
                second.displayName = "Swap Second";
                incoming.displayName = "Swap Incoming";
                participant.Weapon.EquipInitial(first);
                participant.Weapon.EquipInitial(second);
                SetMagazine(participant.Weapon, first, 7);
                SetMagazine(participant.Weapon, second, 5);
                participant.Inventory.AddAmmo(AmmoKind.Medium, 230);
                var pickup = AddTestPickup(spawner, "Swap Weapon", value => value.ConfigureWeapon(incoming));
                var totalBefore = TotalSpawnerAmmo(participant, spawner, AmmoKind.Medium);

                Assert.That(pickup.CollectQuantitative(participant).Success, Is.True);
                var overflow = FindPickup(spawner, LootKind.Ammo);

                Assert.That(participant.Inventory.AmmoFor(AmmoKind.Medium), Is.EqualTo(240));
                Assert.That(participant.Weapon.MagazineFor(incoming), Is.EqualTo(30));
                Assert.That(pickup.Weapon, Is.SameAs(second));
                Assert.That(pickup.LoadedMagazine, Is.EqualTo(5));
                Assert.That(pickup.ReserveBonus, Is.Zero);
                Assert.That(pickup.gameObject.activeSelf, Is.True);
                Assert.That(overflow, Is.Not.Null);
                Assert.That(overflow.Amount, Is.EqualTo(20));
                Assert.That(spawner.FindNearestAmmo(overflow.transform.position, 1f, AmmoKind.Medium),
                    Is.SameAs(overflow));
                Assert.That(TotalSpawnerAmmo(participant, spawner, AmmoKind.Medium), Is.EqualTo(totalBefore));
            }
            finally
            {
                Object.DestroyImmediate(spawnerObject);
                DestroyParticipants(participant);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(incoming);
            }
        }

        [Test]
        public void GroundAmmoPickupTransfersPartiallyAndFullInventoryLeavesRemainderVisible()
        {
            var participant = CreateParticipant("Partial Ground Loot Receiver", true);
            var pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                participant.Inventory.AddAmmo(AmmoKind.Long, 230);
                var pickup = pickupObject.AddComponent<LootPickup>();
                pickup.ConfigureAmmo(AmmoKind.Long, 25);
                var partial = pickup.CollectQuantitative(participant);
                var full = pickup.CollectQuantitative(participant);

                Assert.That(pickup.Amount, Is.EqualTo(15));
                Assert.That(pickup.AmmoKind, Is.EqualTo(AmmoKind.Long));
                Assert.That(pickup.DisplayName, Does.Contain("x15"));
                Assert.That(pickup.Collected, Is.False);
                Assert.That(pickupObject.activeSelf, Is.True);
                Assert.That(partial.AcceptedAmount, Is.EqualTo(10));
                Assert.That(partial.RemainingAmount, Is.EqualTo(15));
                Assert.That(full.Success, Is.False);
                Assert.That(full.RemainingAmount, Is.EqualTo(15));
                Assert.That(participant.Inventory.AmmoFor(AmmoKind.Long), Is.EqualTo(240));
            }
            finally
            {
                Object.DestroyImmediate(pickupObject);
                DestroyParticipants(participant);
            }
        }

        [Test]
        public void DownedParticipantCannotCollectGroundLoot()
        {
            var participant = CreateParticipant("Downed Ground Loot Receiver", true, 12);
            var pickupObject = new GameObject("Downed Pickup");
            try
            {
                participant.Health.ConfigureLifeCycle(30f, 30f, () => true);
                participant.Health.ApplyDamage(1000f, false, null);
                var pickup = pickupObject.AddComponent<LootPickup>();
                pickup.ConfigureHeal(3);
                var result = pickup.CollectQuantitative(participant);

                Assert.That(participant.IsDowned, Is.True);
                Assert.That(result.Success, Is.False);
                Assert.That(pickup.Amount, Is.EqualTo(3));
                Assert.That(pickupObject.activeSelf, Is.True);
                Assert.That(participant.Inventory.MedKits, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(pickupObject);
                DestroyParticipants(participant);
            }
        }

        [Test]
        public void ConfigStartsWithTwentyParticipants()
        {
            var config = new BRGameConfig();
            Assert.That(config.participantCount, Is.EqualTo(20));
            Assert.That(config.botCount, Is.EqualTo(19));
            Assert.That(config.selectedMode, Is.EqualTo(BRMatchMode.Solo));
            Assert.That(config.playlist, Is.EqualTo(BRPlaylist.Casual));
            Assert.That(config.safeZonePhases.Length, Is.GreaterThanOrEqualTo(4));
        }

        [TestCase(BRMatchMode.Solo, 1, 20)]
        [TestCase(BRMatchMode.Duo, 2, 20)]
        [TestCase(BRMatchMode.Trio, 3, 18)]
        [TestCase(BRMatchMode.Squad, 4, 20)]
        public void MatchModesHaveExpectedTeamSizesAndDefaults(BRMatchMode mode, int teamSize, int participantCount)
        {
            Assert.That(BRMatchRules.TeamSize(mode), Is.EqualTo(teamSize));
            Assert.That(BRMatchRules.NormalizeParticipantCount(mode), Is.EqualTo(participantCount));
        }

        [TestCase(BRMatchMode.Solo, 1, 2)]
        [TestCase(BRMatchMode.Duo, 3, 4)]
        [TestCase(BRMatchMode.Trio, 4, 6)]
        [TestCase(BRMatchMode.Trio, 20, 21)]
        [TestCase(BRMatchMode.Squad, 5, 8)]
        public void ParticipantCountRoundsUpToAtLeastTwoCompleteTeams(
            BRMatchMode mode, int requested, int expected)
        {
            Assert.That(BRMatchRules.NormalizeParticipantCount(mode, requested), Is.EqualTo(expected));
        }

        [TestCase(BRMatchMode.Solo, 100)]
        [TestCase(BRMatchMode.Duo, 100)]
        [TestCase(BRMatchMode.Trio, 99)]
        [TestCase(BRMatchMode.Squad, 100)]
        public void ParticipantCountCapsAtLargestCompleteTeamMultiple(BRMatchMode mode, int expectedMaximum)
        {
            Assert.That(BRMatchRules.MaxParticipantCount, Is.EqualTo(100));
            Assert.That(BRMatchRules.NormalizeParticipantCount(mode, BRMatchRules.MaxParticipantCount),
                Is.EqualTo(expectedMaximum));
            Assert.That(BRMatchRules.NormalizeParticipantCount(mode, int.MaxValue),
                Is.EqualTo(expectedMaximum));
            Assert.That(expectedMaximum % BRMatchRules.TeamSize(mode), Is.Zero);

            var config = new BRGameConfig
            {
                selectedMode = mode,
                participantCount = int.MaxValue,
                botCount = 0
            };
            config.Normalize();
            Assert.That(config.participantCount, Is.EqualTo(expectedMaximum));
            Assert.That(config.botCount, Is.EqualTo(expectedMaximum - 1));
        }

        [Test]
        public void ConfigNormalizeKeepsTeamsCompleteAndSynchronizesBotCount()
        {
            var config = new BRGameConfig
            {
                selectedMode = BRMatchMode.Trio,
                participantCount = 20,
                botCount = 2
            };

            config.Normalize();

            Assert.That(config.participantCount, Is.EqualTo(21));
            Assert.That(config.botCount, Is.EqualTo(20));
        }

        [Test]
        public void TeamAssignmentIsDeterministicAndContiguous()
        {
            Assert.That(BRMatchRules.AssignTeamId(0, BRMatchMode.Trio), Is.EqualTo(0));
            Assert.That(BRMatchRules.AssignTeamId(2, BRMatchMode.Trio), Is.EqualTo(0));
            Assert.That(BRMatchRules.AssignTeamId(3, BRMatchMode.Trio), Is.EqualTo(1));
            Assert.That(BRMatchRules.AssignTeamId(8, BRMatchMode.Trio), Is.EqualTo(2));
        }

        [Test]
        public void EnemyPolicyUsesTeamIdentity()
        {
            Assert.That(BRMatchRules.AreEnemies(4, 4), Is.False);
            Assert.That(BRMatchRules.AreEnemies(4, 5), Is.True);
        }

        [Test]
        public void SurvivingTeamCountDeduplicatesLivingTeams()
        {
            Assert.That(BRMatchRules.CountSurvivingTeams(new[] { 0, 0, 2, 2, 5 }), Is.EqualTo(3));
            Assert.That(BRMatchRules.CountSurvivingTeams(System.Array.Empty<int>()), Is.Zero);
        }

        [Test]
        public void SurvivingTeamCountIgnoresDeadParticipants()
        {
            var first = CreateParticipant("Survivor Team Zero", true, 0);
            var teammate = CreateParticipant("Dead Team Zero", false, 0);
            var opponent = CreateParticipant("Survivor Team One", false, 1);
            teammate.Health.ApplyDamage(1000f, false, opponent.gameObject);

            Assert.That(BRMatchRules.CountSurvivingTeams(new[] { first, teammate, opponent }), Is.EqualTo(2));

            opponent.Health.ApplyDamage(1000f, false, first.gameObject);
            Assert.That(BRMatchRules.CountSurvivingTeams(new[] { first, teammate, opponent }), Is.EqualTo(1));

            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(teammate.gameObject);
            Object.DestroyImmediate(opponent.gameObject);
        }

        [Test]
        public void FriendlyFireAndAimAssistRejectTeammates()
        {
            var attacker = CreateParticipant("Friendly Fire Attacker", true, 7);
            var teammate = CreateParticipant("Friendly Fire Teammate", false, 7);
            var enemy = CreateParticipant("Friendly Fire Enemy", false, 8);
            teammate.SetPhase(ParticipantPhase.Grounded);
            enemy.SetPhase(ParticipantPhase.Grounded);

            Assert.That(WeaponSystem.CanDamage(attacker.gameObject, teammate.Health), Is.False);
            Assert.That(WeaponSystem.CanDamage(attacker.gameObject, enemy.Health), Is.True);
            Assert.That(WeaponSystem.IsAimAssistCandidate(attacker.gameObject, teammate), Is.False);
            Assert.That(WeaponSystem.IsAimAssistCandidate(attacker.gameObject, enemy), Is.True);

            Object.DestroyImmediate(attacker.gameObject);
            Object.DestroyImmediate(teammate.gameObject);
            Object.DestroyImmediate(enemy.gameObject);
        }

        [Test]
        public void PlayerTeamWinsWhenDeadPlayerHasSurvivingTeammate()
        {
            var player = CreateParticipant("Dead Winning Player", true, 3);
            var teammate = CreateParticipant("Winning Teammate", false, 3);
            var defeatedEnemy = CreateParticipant("Defeated Enemy", false, 4);
            player.Health.ApplyDamage(1000f, false, defeatedEnemy.gameObject);
            defeatedEnemy.Health.ApplyDamage(1000f, false, teammate.gameObject);

            Assert.That(BRMatchRules.CalculateTeamPlacement(player,
                new[] { player, teammate, defeatedEnemy }), Is.EqualTo(1));

            Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(teammate.gameObject);
            Object.DestroyImmediate(defeatedEnemy.gameObject);
        }

        [Test]
        public void TeamDefeatPlacementUsesRemainingTeamsInsteadOfParticipants()
        {
            var player = CreateParticipant("Defeated Placement Player", true, 0);
            var teammate = CreateParticipant("Defeated Placement Teammate", false, 0);
            var firstTeamMember = CreateParticipant("Remaining Team One A", false, 1);
            var secondTeamMember = CreateParticipant("Remaining Team One B", false, 1);
            var otherTeam = CreateParticipant("Remaining Team Two", false, 2);
            player.Health.ApplyDamage(1000f, false, firstTeamMember.gameObject);
            teammate.Health.ApplyDamage(1000f, false, firstTeamMember.gameObject);

            Assert.That(BRMatchRules.CalculateTeamPlacement(player,
                new[] { player, teammate, firstTeamMember, secondTeamMember, otherTeam }), Is.EqualTo(3));

            Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(teammate.gameObject);
            Object.DestroyImmediate(firstTeamMember.gameObject);
            Object.DestroyImmediate(secondTeamMember.gameObject);
            Object.DestroyImmediate(otherTeam.gameObject);
        }

        [Test]
        public void CombatCandidatePoliciesMatchEnemyPolicy()
        {
            var owner = CreateParticipant("Candidate Policy Owner", true, 10);
            var teammate = CreateParticipant("Candidate Policy Teammate", false, 10);
            var enemy = CreateParticipant("Candidate Policy Enemy", false, 11);
            teammate.SetPhase(ParticipantPhase.Grounded);
            enemy.SetPhase(ParticipantPhase.Grounded);

            Assert.That(BotController.IsTargetCandidate(owner, teammate),
                Is.EqualTo(BRMatchRules.AreEnemies(owner, teammate)));
            Assert.That(BotController.IsTargetCandidate(owner, enemy),
                Is.EqualTo(BRMatchRules.AreEnemies(owner, enemy)));
            Assert.That(MatchManager.IsAimAssistCandidate(owner, teammate),
                Is.EqualTo(BRMatchRules.AreEnemies(owner, teammate)));
            Assert.That(MatchManager.IsAimAssistCandidate(owner, enemy),
                Is.EqualTo(BRMatchRules.AreEnemies(owner, enemy)));
            Assert.That(WeaponSystem.IsAimAssistCandidate(owner.gameObject, teammate),
                Is.EqualTo(BRMatchRules.AreEnemies(owner, teammate)));
            Assert.That(WeaponSystem.IsAimAssistCandidate(owner.gameObject, enemy),
                Is.EqualTo(BRMatchRules.AreEnemies(owner, enemy)));
            Assert.That(WeaponSystem.CanDamage(owner.gameObject, teammate.Health),
                Is.EqualTo(BRMatchRules.AreEnemies(owner, teammate)));
            Assert.That(WeaponSystem.CanDamage(owner.gameObject, enemy.Health),
                Is.EqualTo(BRMatchRules.AreEnemies(owner, enemy)));

            Object.DestroyImmediate(owner.gameObject);
            Object.DestroyImmediate(teammate.gameObject);
            Object.DestroyImmediate(enemy.gameObject);
        }

        [Test]
        public void ShotgunAggregationRejectsFriendlyPellets()
        {
            var owner = CreateParticipant("Shotgun Policy Owner", true, 20);
            var teammate = CreateParticipant("Shotgun Policy Teammate", false, 20);
            var enemy = CreateParticipant("Shotgun Policy Enemy", false, 21);
            var bodyDamage = 0f;
            var headDamage = 0f;

            Assert.That(WeaponSystem.TryAggregateShotgunHit(owner.gameObject, teammate.Health,
                8f, false, ref bodyDamage, ref headDamage), Is.False);
            Assert.That(bodyDamage, Is.Zero);
            Assert.That(headDamage, Is.Zero);
            Assert.That(WeaponSystem.TryAggregateShotgunHit(owner.gameObject, enemy.Health,
                12f, true, ref bodyDamage, ref headDamage), Is.True);
            Assert.That(bodyDamage, Is.Zero);
            Assert.That(headDamage, Is.EqualTo(12f));

            Object.DestroyImmediate(owner.gameObject);
            Object.DestroyImmediate(teammate.gameObject);
            Object.DestroyImmediate(enemy.gameObject);
        }

        [Test]
        public void DownedConfigDefaultsAndNormalizationAreReasonable()
        {
            var config = new BRGameConfig();
            Assert.That(config.bleedoutSeconds, Is.EqualTo(24f));
            Assert.That(config.reviveSeconds, Is.EqualTo(4f));
            Assert.That(config.reviveHealth, Is.EqualTo(35f));
            Assert.That(config.reviveRadius, Is.EqualTo(2.2f));
            Assert.That(config.downedMoveSpeed, Is.EqualTo(1.35f));

            config.bleedoutSeconds = -1f;
            config.reviveSeconds = 0f;
            config.reviveHealth = float.PositiveInfinity;
            config.reviveRadius = -5f;
            config.downedMoveSpeed = 0f;
            config.Normalize();

            Assert.That(config.bleedoutSeconds, Is.InRange(1f, 300f));
            Assert.That(config.reviveSeconds, Is.InRange(0.25f, 30f));
            Assert.That(config.reviveHealth, Is.InRange(1f, 100f));
            Assert.That(config.reviveRadius, Is.InRange(0.5f, 8f));
            Assert.That(config.downedMoveSpeed, Is.InRange(0.25f, 4f));
        }

        [Test]
        public void SoloLethalDamageEliminatesImmediately()
        {
            BRParticipant victim = null;
            BRParticipant teammate = null;
            try
            {
                victim = CreateParticipant("Solo Victim", true, 0);
                teammate = CreateParticipant("Nominal Teammate", false, 0);
                var team = new[] { victim, teammate };
                victim.Health.ConfigureLifeCycle(24f, 35f,
                    () => BRMatchRules.ShouldDownOnLethal(BRMatchMode.Solo, victim, team));

                victim.Health.ApplyDamage(1000f, false, teammate.gameObject);

                Assert.That(victim.IsEliminated, Is.True);
                Assert.That(victim.IsDowned, Is.False);
            }
            finally
            {
                DestroyParticipants(victim, teammate);
            }
        }

        [Test]
        public void SquadLethalDamageDownsWhenActiveTeammateExists()
        {
            BRParticipant victim = null;
            BRParticipant teammate = null;
            BRParticipant enemy = null;
            try
            {
                victim = CreateParticipant("Squad Victim", true, 2);
                teammate = CreateParticipant("Active Squad Teammate", false, 2);
                enemy = CreateParticipant("Squad Enemy", false, 3);
                var participants = new[] { victim, teammate, enemy };
                victim.Health.ConfigureLifeCycle(24f, 35f,
                    () => BRMatchRules.ShouldDownOnLethal(BRMatchMode.Squad, victim, participants));

                victim.Health.ApplyDamage(1000f, false, enemy.gameObject);

                Assert.That(victim.IsDowned, Is.True);
                Assert.That(victim.IsEliminated, Is.False);
                Assert.That(victim.Phase, Is.EqualTo(ParticipantPhase.Downed));
            }
            finally
            {
                DestroyParticipants(victim, teammate, enemy);
            }
        }

        [Test]
        public void LastActiveTeamMemberIsEliminatedInsteadOfDowned()
        {
            BRParticipant victim = null;
            BRParticipant teammate = null;
            BRParticipant enemy = null;
            try
            {
                victim = CreateParticipant("Last Active", true, 4);
                teammate = CreateParticipant("Already Eliminated", false, 4);
                enemy = CreateParticipant("Last Active Enemy", false, 5);
                teammate.Health.ApplyDamage(1000f, false, enemy.gameObject);
                var participants = new[] { victim, teammate, enemy };
                victim.Health.ConfigureLifeCycle(24f, 35f,
                    () => BRMatchRules.ShouldDownOnLethal(BRMatchMode.Duo, victim, participants));

                victim.Health.ApplyDamage(1000f, false, enemy.gameObject);

                Assert.That(victim.IsEliminated, Is.True);
                Assert.That(victim.IsDowned, Is.False);
            }
            finally
            {
                DestroyParticipants(victim, teammate, enemy);
            }
        }

        [Test]
        public void DownedParticipantStillCountsAsSurvivingTeam()
        {
            BRParticipant downed = null;
            BRParticipant teammate = null;
            BRParticipant enemy = null;
            try
            {
                downed = CreateParticipant("Counting Downed", true, 6);
                teammate = CreateParticipant("Counting Teammate", false, 6);
                enemy = CreateParticipant("Counting Enemy", false, 7);
                downed.Health.ConfigureLifeCycle(24f, 35f, () => true);
                downed.Health.ApplyDamage(1000f, false, enemy.gameObject);

                Assert.That(BRMatchRules.CountSurvivingTeams(new[] { downed, teammate, enemy }), Is.EqualTo(2));
            }
            finally
            {
                DestroyParticipants(downed, teammate, enemy);
            }
        }

        [Test]
        public void PositiveDamageFinishesDownedAndEmitsDiedOnce()
        {
            BRParticipant victim = null;
            BRParticipant enemy = null;
            try
            {
                victim = CreateParticipant("Finish Victim", true, 8);
                enemy = CreateParticipant("Finish Enemy", false, 9);
                var died = 0;
                victim.Health.Died += _ => died++;
                victim.Health.ConfigureLifeCycle(24f, 35f, () => true);
                victim.Health.ApplyDamage(1000f, false, enemy.gameObject);

                victim.Health.ApplyDamage(1f, false, enemy.gameObject);
                victim.Health.ApplyDamage(1f, false, enemy.gameObject);

                Assert.That(victim.IsEliminated, Is.True);
                Assert.That(died, Is.EqualTo(1));
            }
            finally
            {
                DestroyParticipants(victim, enemy);
            }
        }

        [Test]
        public void BleedoutExpirationEliminatesDownedParticipant()
        {
            BRParticipant victim = null;
            try
            {
                victim = CreateParticipant("Bleedout Victim", true, 10);
                victim.Health.ConfigureLifeCycle(24f, 35f, () => true);
                victim.Health.ApplyDamage(1000f, false, null);

                victim.Health.TickBleedout(12f);
                Assert.That(victim.Health.BleedoutRemaining, Is.EqualTo(12f).Within(0.001f));
                Assert.That(victim.Health.BleedoutRatio, Is.EqualTo(0.5f).Within(0.001f));
                victim.Health.TickBleedout(12f);

                Assert.That(victim.IsEliminated, Is.True);
            }
            finally
            {
                DestroyParticipants(victim);
            }
        }

        [Test]
        public void ReviveRestoresExactHealthAndEmitsLifecycleEvents()
        {
            BRParticipant victim = null;
            try
            {
                victim = CreateParticipant("Revive Victim", true, 11);
                var downedEvents = 0;
                var revivedEvents = 0;
                var diedEvents = 0;
                victim.Health.Downed += _ => downedEvents++;
                victim.Health.Revived += _ => revivedEvents++;
                victim.Health.Died += _ => diedEvents++;
                victim.Health.EquipVest(2);
                victim.Health.EquipHelmet(3);
                victim.Health.ConfigureLifeCycle(24f, 35f, () => true);
                victim.Health.ApplyDamage(1000f, false, null);

                victim.Health.Revive();

                Assert.That(victim.Health.Health, Is.EqualTo(35f));
                Assert.That(victim.Health.VestLevel, Is.EqualTo(2));
                Assert.That(victim.Health.HelmetLevel, Is.EqualTo(3));
                Assert.That(victim.IsDowned, Is.False);
                Assert.That(victim.IsEliminated, Is.False);
                Assert.That(victim.Phase, Is.EqualTo(ParticipantPhase.Grounded));
                Assert.That(downedEvents, Is.EqualTo(1));
                Assert.That(revivedEvents, Is.EqualTo(1));
                Assert.That(diedEvents, Is.Zero);
                Assert.That(victim.Health.ApplyDamage(10f, false, null).FinalDamage, Is.Zero);
                Assert.That(victim.Health.Health, Is.EqualTo(35f));
            }
            finally
            {
                DestroyParticipants(victim);
            }
        }

        [Test]
        public void ReviveEligibilityRequiresSameTeamDistanceAndUndamagedReviver()
        {
            BRParticipant reviver = null;
            BRParticipant ally = null;
            BRParticipant enemy = null;
            try
            {
                reviver = CreateParticipant("Eligible Reviver", true, 12);
                ally = CreateParticipant("Downed Ally", false, 12);
                enemy = CreateParticipant("Downed Enemy", false, 13);
                reviver.SetPhase(ParticipantPhase.Grounded);
                ally.Health.ConfigureLifeCycle(24f, 35f, () => true);
                enemy.Health.ConfigureLifeCycle(24f, 35f, () => true);
                ally.Health.ApplyDamage(1000f, false, enemy.gameObject);
                enemy.Health.ApplyDamage(1000f, false, reviver.gameObject);
                ally.transform.position = Vector3.right * 2f;
                enemy.transform.position = Vector3.forward;
                var sequence = reviver.Health.DamageSequence;

                Assert.That(BRMatchRules.CanRevive(reviver, ally, 2.2f), Is.True);
                Assert.That(BRMatchRules.CanRevive(reviver, enemy, 2.2f), Is.False);
                ally.transform.position = Vector3.right * 2.21f;
                Assert.That(BRMatchRules.CanRevive(reviver, ally, 2.2f), Is.False);
                ally.transform.position = Vector3.right * 2f;
                reviver.Health.ApplyDamage(1f, false, enemy.gameObject);
                Assert.That(BRMatchRules.CanContinueRevive(reviver, ally, 2.2f, sequence), Is.False);
            }
            finally
            {
                DestroyParticipants(reviver, ally, enemy);
            }
        }

        [Test]
        public void DownedActionGatesBlockWeaponsInventoryAndHealing()
        {
            BRParticipant participant = null;
            WeaponDefinition weapon = null;
            try
            {
                participant = CreateParticipant("Downed Action Gates", true, 14);
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                weapon.magazineSize = 30;
                participant.Weapon.EquipInitial(weapon);
                participant.Inventory.AddMedKit(1);
                participant.Health.ConfigureLifeCycle(24f, 35f, () => true);
                participant.Health.ApplyDamage(1000f, false, null);
                var ammoBefore = participant.Inventory.AmmoFor(AmmoKind.Medium);

                participant.Inventory.AddAmmo(AmmoKind.Medium, 20);

                Assert.That(participant.IsCombatCapable, Is.False);
                Assert.That(participant.Inventory.CanUseInventory, Is.False);
                Assert.That(participant.Inventory.AmmoFor(AmmoKind.Medium), Is.EqualTo(ammoBefore));
                Assert.That(participant.Inventory.TryUseMedKit(participant.Health), Is.False);
                Assert.That(participant.Weapon.TryFire(null, false, participant.gameObject), Is.False);
            }
            finally
            {
                if (weapon != null) Object.DestroyImmediate(weapon);
                DestroyParticipants(participant);
            }
        }

        [Test]
        public void TeamWipeRuleEliminatesRemainingDownedMembers()
        {
            BRParticipant first = null;
            BRParticipant second = null;
            try
            {
                first = CreateParticipant("Wiped Downed One", true, 15);
                second = CreateParticipant("Wiped Downed Two", false, 15);
                first.Health.ConfigureLifeCycle(24f, 35f, () => true);
                second.Health.ConfigureLifeCycle(24f, 35f, () => true);
                first.Health.ApplyDamage(1000f, false, null);
                second.Health.ApplyDamage(1000f, false, null);
                var team = new[] { first, second };

                Assert.That(BRMatchRules.ShouldEliminateDownedTeamMember(first, team), Is.True);
                Assert.That(BRMatchRules.ShouldEliminateDownedTeamMember(second, team), Is.True);
                first.Health.Eliminate();
                second.Health.Eliminate();

                Assert.That(BRMatchRules.CountSurvivingTeams(team), Is.Zero);
            }
            finally
            {
                DestroyParticipants(first, second);
            }
        }

        [Test]
        public void PlayerHoldReviveProgressesForFourSecondsAndTakesPriorityOverOpenLoot()
        {
            var fixture = CreateReviveManagerFixture("Hold Completion");
            BRParticipant lootVictim = null;
            try
            {
                lootVictim = CreateParticipant("Revive Priority Loot Victim", false, 99);
                lootVictim.Inventory.AddMedKit(1);
                var container = DeathLootContainer.Create(lootVictim);
                fixture.manager.OpenDeathLoot(container);
                Assert.That(fixture.manager.ActiveDeathLoot, Is.SameAs(container));

                Assert.That(InvokePlayerReviveTick(fixture.manager, true, 0f, fixture.reviver), Is.True);
                Assert.That(fixture.manager.ReviveTarget, Is.SameAs(fixture.target));
                Assert.That(fixture.manager.ActiveDeathLoot, Is.Null);
                Assert.That(BRMatchRules.ResolveInteractionPriority(true, true, true),
                    Is.EqualTo(InteractionPriority.Revive));
                Assert.That(BRMatchRules.ResolveInteractionPriority(true, false, true),
                    Is.EqualTo(InteractionPriority.Loot));

                for (var second = 1; second <= 3; second++)
                {
                    Assert.That(InvokePlayerReviveTick(fixture.manager, true, 1f, fixture.reviver), Is.True);
                    Assert.That(fixture.manager.ReviveProgress, Is.EqualTo(second / 4f).Within(0.001f));
                    Assert.That(fixture.target.IsDowned, Is.True);
                }

                Assert.That(InvokePlayerReviveTick(fixture.manager, true, 1f, fixture.reviver), Is.True);
                Assert.That(fixture.manager.ReviveTarget, Is.Null);
                Assert.That(fixture.manager.ReviveProgress, Is.Zero);
                Assert.That(fixture.target.IsDowned, Is.False);
                Assert.That(fixture.target.Health.Health, Is.EqualTo(35f));
            }
            finally
            {
                DestroyParticipants(lootVictim);
                DestroyReviveManagerFixture(fixture);
            }
        }

        [TestCase("release")]
        [TestCase("distance")]
        [TestCase("damage")]
        [TestCase("target-state")]
        public void PlayerHoldReviveCancelsForEveryRuntimeInvalidation(string reason)
        {
            var fixture = CreateReviveManagerFixture($"Hold Cancel {reason}");
            try
            {
                Assert.That(InvokePlayerReviveTick(fixture.manager, true, 0f, fixture.reviver), Is.True);
                Assert.That(InvokePlayerReviveTick(fixture.manager, true, 1f, fixture.reviver), Is.True);
                Assert.That(fixture.manager.ReviveProgress, Is.EqualTo(0.25f).Within(0.001f));
                var holding = true;
                if (reason == "release") holding = false;
                else if (reason == "distance") fixture.target.transform.position = Vector3.right * 2.21f;
                else if (reason == "damage") fixture.reviver.Health.ApplyDamage(1f, false, null);
                else fixture.target.Health.Revive();

                Assert.That(InvokePlayerReviveTick(fixture.manager, holding, 1f, fixture.reviver), Is.False);
                Assert.That(fixture.manager.ReviveTarget, Is.Null);
                Assert.That(fixture.manager.ReviveProgress, Is.Zero);
            }
            finally
            {
                DestroyReviveManagerFixture(fixture);
            }
        }

        [Test]
        public void ManagerCreatesKillsAndBoxesOnlyOnTerminalDeathAndCascadesTeamWipe()
        {
            DeathLootContainer.ClearAll();
            var managerObject = new GameObject("Team Wipe Match Manager");
            var manager = managerObject.AddComponent<MatchManager>();
            BRParticipant first = null;
            BRParticipant lastActive = null;
            BRParticipant enemy = null;
            System.Action<HealthArmorSystem> diedHandler = null;
            try
            {
                typeof(MatchManager).GetProperty("State")?.SetValue(manager, MatchState.Active);
                first = CreateParticipant("First Downed Teammate", true, 21);
                lastActive = CreateParticipant("Last Active Teammate", false, 21);
                enemy = CreateParticipant("Team Wipe Enemy", false, 22);
                first.SetPhase(ParticipantPhase.Grounded);
                lastActive.SetPhase(ParticipantPhase.Grounded);
                enemy.SetPhase(ParticipantPhase.Grounded);
                var participants = ManagerParticipants(manager);
                participants.Add(first);
                participants.Add(lastActive);
                participants.Add(enemy);
                first.Health.ConfigureLifeCycle(24f, 35f,
                    () => BRMatchRules.ShouldDownOnLethal(BRMatchMode.Squad, first, participants));
                lastActive.Health.ConfigureLifeCycle(24f, 35f,
                    () => BRMatchRules.ShouldDownOnLethal(BRMatchMode.Squad, lastActive, participants));
                var onDied = typeof(MatchManager).GetMethod("OnParticipantDied",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(onDied, Is.Not.Null);
                diedHandler = health => onDied.Invoke(manager, new object[] { health });
                first.Health.Died += diedHandler;
                lastActive.Health.Died += diedHandler;

                first.Health.ApplyDamage(1000f, false, enemy.gameObject);

                Assert.That(first.IsDowned, Is.True);
                Assert.That(first.gameObject.activeSelf, Is.True);
                Assert.That(enemy.Eliminations, Is.Zero);
                Assert.That(Object.FindObjectsByType<DeathLootContainer>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length, Is.Zero);

                lastActive.Health.ApplyDamage(1000f, false, enemy.gameObject);

                Assert.That(lastActive.IsEliminated, Is.True);
                Assert.That(first.IsEliminated, Is.True);
                Assert.That(enemy.Eliminations, Is.EqualTo(2));
                Assert.That(Object.FindObjectsByType<DeathLootContainer>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length, Is.EqualTo(2));
                Assert.That(manager.AliveTeamCount, Is.EqualTo(1));
            }
            finally
            {
                if (first != null && diedHandler != null) first.Health.Died -= diedHandler;
                if (lastActive != null && diedHandler != null) lastActive.Health.Died -= diedHandler;
                DeathLootContainer.ClearAll();
                DestroyParticipants(first, lastActive, enemy);
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void MatchCompletionStopsSameTickDamageAndFreezesWinnerAndResult()
        {
            DeathLootContainer.ClearAll();
            DamageFeedbackSystem.ClearEvents();
            var managerObject = new GameObject("Idempotent Completion Manager");
            var zoneObject = new GameObject("Idempotent Completion Zone");
            var manager = managerObject.AddComponent<MatchManager>();
            var zone = zoneObject.AddComponent<SafeZoneController>();
            BRParticipant first = null;
            BRParticipant winner = null;
            System.Action<HealthArmorSystem> diedHandler = null;
            try
            {
                typeof(MatchManager).GetProperty("State")?.SetValue(manager, MatchState.Active);
                first = CreateParticipant("Same Tick Team One", true, 40);
                winner = CreateParticipant("Same Tick Team Two", false, 41);
                first.SetPhase(ParticipantPhase.Grounded);
                winner.SetPhase(ParticipantPhase.Grounded);
                first.transform.position = new Vector3(1200f, 0f, 0f);
                winner.transform.position = new Vector3(1210f, 0f, 0f);
                first.Health.ApplyDamage(99f, false, null);
                winner.Health.ApplyDamage(99f, false, null);
                var participants = ManagerParticipants(manager);
                participants.Add(first);
                participants.Add(winner);
                var onDied = typeof(MatchManager).GetMethod("OnParticipantDied",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(onDied, Is.Not.Null);
                diedHandler = health => onDied.Invoke(manager, new object[] { health });
                first.Health.Died += diedHandler;
                winner.Health.Died += diedHandler;
                var config = new BRGameConfig
                {
                    safeZonePhases = new[] { new SafeZonePhase(30f, 10f, 50f, 5f) }
                };
                zone.Configure(config);

                zone.Tick(participants, 1f, () => manager.State == MatchState.Active);

                Assert.That(first.IsEliminated, Is.True);
                Assert.That(winner.IsEliminated, Is.False);
                Assert.That(winner.Health.Health, Is.EqualTo(1f).Within(0.001f));
                Assert.That(manager.State, Is.EqualTo(MatchState.Complete));
                Assert.That(manager.AliveTeamCount, Is.EqualTo(1));
                Assert.That(manager.WinningTeamId, Is.EqualTo(41));
                Assert.That(CountDeathLootOwnedBy(first.DisplayName), Is.EqualTo(1));
                Assert.That(CountDeathLootOwnedBy(winner.DisplayName), Is.Zero);
                var frozenResult = manager.ResultText;

                InvokePrivate(manager, "Complete");
                InvokePrivate(manager, "Complete");
                InvokePrivate(manager, "UpdateAlive");
                zone.Tick(participants, 10f, () => manager.State == MatchState.Active);

                Assert.That(manager.ResultText, Is.EqualTo(frozenResult));
                Assert.That(manager.AliveTeamCount, Is.EqualTo(1));
                Assert.That(manager.WinningTeamId, Is.EqualTo(41));
                Assert.That(winner.IsEliminated, Is.False);
                Assert.That(CountDeathLootOwnedBy(first.DisplayName), Is.EqualTo(1));
                Assert.That(CountDeathLootOwnedBy(winner.DisplayName), Is.Zero);
            }
            finally
            {
                if (first != null && diedHandler != null) first.Health.Died -= diedHandler;
                if (winner != null && diedHandler != null) winner.Health.Died -= diedHandler;
                DeathLootContainer.ClearAll();
                DamageFeedbackSystem.ClearEvents();
                DestroyParticipants(first, winner);
                Object.DestroyImmediate(zoneObject);
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void SafeZoneShrinkInterpolationUsesFixedStartSnapshotAndDuration()
        {
            var startCenter = new Vector3(-10f, 0f, 4f);
            var destinationCenter = new Vector3(20f, 0f, -6f);
            const float startRadius = 100f;
            const float destinationRadius = 40f;
            const float duration = 8f;

            var start = SafeZoneController.InterpolateShrink(startCenter, startRadius,
                destinationCenter, destinationRadius, 0f, duration);
            var halfway = SafeZoneController.InterpolateShrink(startCenter, startRadius,
                destinationCenter, destinationRadius, duration * 0.5f, duration);
            var complete = SafeZoneController.InterpolateShrink(startCenter, startRadius,
                destinationCenter, destinationRadius, duration, duration);

            Assert.That(start.Center, Is.EqualTo(startCenter));
            Assert.That(start.Radius, Is.EqualTo(startRadius));
            Assert.That(halfway.Center, Is.EqualTo(Vector3.Lerp(startCenter, destinationCenter, 0.5f)));
            Assert.That(halfway.Radius, Is.EqualTo(70f));
            Assert.That(complete.Center, Is.EqualTo(destinationCenter));
            Assert.That(complete.Radius, Is.EqualTo(destinationRadius));
        }

        [Test]
        public void SafeZoneRealTickFinishesDownedParticipantOutsideRing()
        {
            var zoneObject = new GameObject("Downed Safe Zone");
            BRParticipant participant = null;
            try
            {
                var zone = zoneObject.AddComponent<SafeZoneController>();
                zone.Configure(new BRGameConfig
                {
                    safeZonePhases = new[] { new SafeZonePhase(30f, 10f, 50f, 5f) }
                });
                participant = CreateParticipant("Downed Outside Zone", true, 50);
                participant.SetPhase(ParticipantPhase.Grounded);
                participant.transform.position = new Vector3(1400f, 0f, 0f);
                participant.Health.ConfigureLifeCycle(24f, 35f, () => true);
                participant.Health.ApplyDamage(1000f, false, null);
                var diedEvents = 0;
                participant.Health.Died += _ => diedEvents++;

                zone.Tick(new[] { participant }, 1f, null);

                Assert.That(participant.IsEliminated, Is.True);
                Assert.That(diedEvents, Is.EqualTo(1));
            }
            finally
            {
                DestroyParticipants(participant);
                Object.DestroyImmediate(zoneObject);
            }
        }

        [Test]
        public void BotBlockedReviveApproachCancelsIntoRecoveryBeforeBleedout()
        {
            BRParticipant botParticipant = null;
            BRParticipant ally = null;
            try
            {
                botParticipant = CreateParticipant("Blocked Rescue Bot", false, 60);
                ally = CreateParticipant("Blocked Rescue Ally", false, 60);
                botParticipant.SetPhase(ParticipantPhase.Grounded);
                ally.SetPhase(ParticipantPhase.Grounded);
                ally.transform.position = Vector3.right * 4f;
                ally.Health.ConfigureLifeCycle(24f, 35f, () => true);
                ally.Health.ApplyDamage(1000f, false, null);
                var bot = botParticipant.gameObject.AddComponent<BotController>();
                bot.Configure(botParticipant, null, null, new BRGameConfig());
                InvokePrivate(bot, "Think", (object)new[] { botParticipant, ally });
                Assert.That(bot.State, Is.EqualTo(BotState.Reviving));

                var firstPosition = botParticipant.transform.position;
                bot.TickRevive(0.5f);
                var radius = Vector3.Distance(firstPosition, ally.transform.position);
                var firstAngle = 0.1f;
                var firstLateralPosition = ally.transform.position
                    + new Vector3(-radius * Mathf.Cos(firstAngle), 0f, radius * Mathf.Sin(firstAngle));
                Assert.That(Vector3.Distance(firstPosition, firstLateralPosition), Is.GreaterThan(0.25f));
                Assert.That(Vector3.Distance(firstLateralPosition, ally.transform.position), Is.EqualTo(radius).Within(0.0001f));
                botParticipant.Motor.Teleport(firstLateralPosition);
                bot.TickRevive(0.5f);
                Assert.That(bot.State, Is.EqualTo(BotState.Reviving));
                var secondAngle = 0.2f;
                var secondLateralPosition = ally.transform.position
                    + new Vector3(-radius * Mathf.Cos(secondAngle), 0f, radius * Mathf.Sin(secondAngle));
                Assert.That(Vector3.Distance(firstLateralPosition, secondLateralPosition), Is.GreaterThan(0.25f));
                Assert.That(Vector3.Distance(secondLateralPosition, ally.transform.position), Is.EqualTo(radius).Within(0.0001f));
                botParticipant.Motor.Teleport(secondLateralPosition);
                bot.TickRevive(0.5f);

                Assert.That(bot.State, Is.EqualTo(BotState.Recovering));
                Assert.That(bot.ReviveTarget, Is.Null);
                Assert.That(ally.IsDowned, Is.True);
                Assert.That(ally.Health.BleedoutRemaining, Is.EqualTo(24f));
            }
            finally
            {
                DestroyParticipants(botParticipant, ally);
            }
        }

        [Test]
        public void BotRealApproachResetsReviveTimeoutWindow()
        {
            BRParticipant botParticipant = null;
            BRParticipant ally = null;
            try
            {
                botParticipant = CreateParticipant("Approaching Rescue Bot", false, 61);
                ally = CreateParticipant("Approaching Rescue Ally", false, 61);
                botParticipant.SetPhase(ParticipantPhase.Grounded);
                ally.SetPhase(ParticipantPhase.Grounded);
                ally.transform.position = Vector3.right * 4f;
                ally.Health.ConfigureLifeCycle(24f, 35f, () => true);
                ally.Health.ApplyDamage(1000f, false, null);
                var bot = botParticipant.gameObject.AddComponent<BotController>();
                bot.Configure(botParticipant, null, null, new BRGameConfig());
                InvokePrivate(bot, "Think", (object)new[] { botParticipant, ally });

                var initialPosition = botParticipant.transform.position;
                bot.TickRevive(1f);
                var approachedPosition = initialPosition + Vector3.right
                    * (BotController.ReviveApproachProgressDistance + 0.01f);
                botParticipant.Motor.Teleport(approachedPosition);
                bot.TickRevive(0.6f);
                Assert.That(bot.State, Is.EqualTo(BotState.Reviving));

                botParticipant.Motor.Teleport(approachedPosition);
                bot.TickRevive(1f);
                Assert.That(bot.State, Is.EqualTo(BotState.Reviving));
                botParticipant.Motor.Teleport(approachedPosition);
                bot.TickRevive(0.5f);

                Assert.That(bot.State, Is.EqualTo(BotState.Recovering));
                Assert.That(bot.ReviveTarget, Is.Null);
                Assert.That(ally.IsDowned, Is.True);
            }
            finally
            {
                DestroyParticipants(botParticipant, ally);
            }
        }

        [Test]
        public void BotRuntimeRescueSelectionAndCancellationUseSharedReviveRules()
        {
            BRParticipant botParticipant = null;
            BRParticipant ally = null;
            try
            {
                botParticipant = CreateParticipant("Rescue Bot", false, 30);
                ally = CreateParticipant("Rescue Ally", false, 30);
                botParticipant.SetPhase(ParticipantPhase.Grounded);
                ally.SetPhase(ParticipantPhase.Grounded);
                ally.transform.position = Vector3.right * 2f;
                ally.Health.ConfigureLifeCycle(24f, 35f, () => true);
                ally.Health.ApplyDamage(1000f, false, null);
                var config = new BRGameConfig();
                var bot = botParticipant.gameObject.AddComponent<BotController>();
                bot.Configure(botParticipant, null, null, config);
                var participants = new[] { botParticipant, ally };

                InvokePrivate(bot, "Think", (object)participants);
                Assert.That(bot.State, Is.EqualTo(BotState.Reviving));
                Assert.That(bot.ReviveTarget, Is.SameAs(ally));
                Assert.That(BotController.SelectReviveTarget(botParticipant, participants, 8f, false), Is.SameAs(ally));
                Assert.That(BotController.SelectReviveTarget(botParticipant, participants, 8f, true), Is.Null);

                botParticipant.Health.ApplyDamage(1f, false, null);
                bot.TickRevive(0f);
                Assert.That(bot.State, Is.EqualTo(BotState.Roaming));
                Assert.That(bot.ReviveTarget, Is.Null);

                ally.transform.position = Vector3.right * 2f;
                InvokePrivate(bot, "Think", (object)participants);
                Assert.That(bot.ReviveTarget, Is.SameAs(ally));
                ally.transform.position = Vector3.right * 8.01f;
                bot.TickRevive(0f);
                Assert.That(bot.State, Is.EqualTo(BotState.Roaming));
                Assert.That(bot.ReviveTarget, Is.Null);
            }
            finally
            {
                DestroyParticipants(botParticipant, ally);
            }
        }

        [Test]
        public void BotTickReviveCompletesAfterFourSecondsAndRestoresThirtyFiveHealth()
        {
            BRParticipant botParticipant = null;
            BRParticipant ally = null;
            try
            {
                botParticipant = CreateParticipant("Completing Rescue Bot", false, 31);
                ally = CreateParticipant("Completing Rescue Ally", false, 31);
                botParticipant.SetPhase(ParticipantPhase.Grounded);
                ally.SetPhase(ParticipantPhase.Grounded);
                ally.transform.position = Vector3.right * 2f;
                ally.Health.ConfigureLifeCycle(24f, 35f, () => true);
                ally.Health.ApplyDamage(1000f, false, null);
                var revivedEvents = 0;
                ally.Health.Revived += _ => revivedEvents++;
                var bot = botParticipant.gameObject.AddComponent<BotController>();
                bot.Configure(botParticipant, null, null, new BRGameConfig());
                var participants = new[] { botParticipant, ally };
                InvokePrivate(bot, "Think", (object)participants);

                for (var second = 1; second <= 3; second++)
                {
                    bot.TickRevive(1f);
                    Assert.That(bot.ReviveProgress, Is.EqualTo(second / 4f).Within(0.001f));
                    Assert.That(ally.IsDowned, Is.True);
                }

                bot.TickRevive(1f);

                Assert.That(bot.State, Is.EqualTo(BotState.Roaming));
                Assert.That(bot.ReviveTarget, Is.Null);
                Assert.That(bot.ReviveProgress, Is.Zero);
                Assert.That(ally.IsDowned, Is.False);
                Assert.That(ally.Health.Health, Is.EqualTo(35f));
                Assert.That(revivedEvents, Is.EqualTo(1));
            }
            finally
            {
                DestroyParticipants(botParticipant, ally);
            }
        }

        [Test]
        public void RealWeaponShotsDownThenFinishEnemyThroughDeterministicHitbox()
        {
            BRParticipant bot = null;
            BRParticipant enemy = null;
            BRParticipant enemyTeammate = null;
            WeaponDefinition weapon = null;
            try
            {
                bot = CreateParticipant("Finishing Bot", false, 32);
                enemy = CreateParticipant("Downed Enemy Target", false, 33);
                enemyTeammate = CreateParticipant("Active Enemy Teammate", false, 33);
                bot.SetPhase(ParticipantPhase.Grounded);
                enemy.SetPhase(ParticipantPhase.Grounded);
                enemyTeammate.SetPhase(ParticipantPhase.Grounded);
                bot.transform.position = new Vector3(900f, 0f, 0f);
                enemy.transform.position = new Vector3(900f, 0f, 8f);
                enemyTeammate.transform.position = new Vector3(910f, 0f, 8f);
                bot.transform.rotation = Quaternion.identity;
                bot.GetComponent<CharacterController>().enabled = false;
                var hitbox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hitbox.name = "Deterministic Downed Finish Hitbox";
                hitbox.transform.SetParent(enemy.transform, false);
                hitbox.transform.localPosition = Vector3.up * CharacterPresentationProfile.MuzzleHeight;
                hitbox.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                var participants = new[] { bot, enemy, enemyTeammate };
                enemy.Health.ConfigureLifeCycle(24f, 35f,
                    () => BRMatchRules.ShouldDownOnLethal(BRMatchMode.Squad, enemy, participants));
                var diedEvents = 0;
                enemy.Health.Died += _ => diedEvents++;
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                weapon.damage = 150f;
                weapon.magazineSize = 2;
                weapon.range = 30f;
                weapon.fireInterval = 0f;
                weapon.standingSpreadDegrees = 0f;
                weapon.movingSpreadDegrees = 0f;
                bot.Weapon.EquipInitial(weapon);
                Physics.SyncTransforms();

                Assert.That(bot.Weapon.TryFire(null, false, bot.gameObject), Is.True);
                Assert.That(bot.Weapon.LastShotHit, Is.True);
                Assert.That(enemy.IsDowned, Is.True);
                Assert.That(enemy.IsEliminated, Is.False);

                Assert.That(BotController.IsTargetCandidate(bot, enemy), Is.True);
                Assert.That(WeaponSystem.CanDamage(bot.gameObject, enemy.Health), Is.True);
                Assert.That(bot.Weapon.TryFire(null, false, bot.gameObject), Is.True);
                Assert.That(bot.Weapon.LastShotHit, Is.True);
                Assert.That(enemy.IsEliminated, Is.True);
                Assert.That(diedEvents, Is.EqualTo(1));
            }
            finally
            {
                if (weapon != null) Object.DestroyImmediate(weapon);
                DestroyParticipants(bot, enemy, enemyTeammate);
                DestroyWeaponFeedback();
            }
        }

        [Test]
        public void DownedParticipantStaysActiveAndMotorUsesCrawlSpeedWithoutJump()
        {
            BRParticipant participant = null;
            var cameraObject = new GameObject("Downed Motor Camera");
            try
            {
                participant = CreateParticipant("Downed Motor Participant", true, 33);
                participant.SetPhase(ParticipantPhase.Grounded);
                participant.Health.ConfigureLifeCycle(24f, 35f, () => true);
                participant.Health.ApplyDamage(1000f, false, null);
                var config = new BRGameConfig();
                var input = new BRInputFrame(Vector2.up, Vector2.zero, true, false, true, true,
                    false, false, false, false, false, false, -1, false, false);

                participant.Motor.Move(input, cameraObject.transform, config, true);

                Assert.That(participant.gameObject.activeSelf, Is.True);
                Assert.That(participant.Motor.RequestedMoveSpeed, Is.EqualTo(1.35f));
                Assert.That(BRCharacterMotor.ResolveMoveSpeed(true, config, true, false, true), Is.EqualTo(1.35f));
                Assert.That(BRCharacterMotor.CanStartJump(true, true, true, true), Is.False);
                Assert.That(participant.Motor.Vaulting, Is.False);
                Assert.That(participant.Motor.Sprinting, Is.False);
                Assert.That(participant.Motor.Aiming, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                DestroyParticipants(participant);
            }
        }

        [Test]
        public void ConfigDefinesStagingAndDeploymentPhases()
        {
            var config = new BRGameConfig();
            Assert.That(config.stagingSeconds, Is.GreaterThanOrEqualTo(10f));
            Assert.That(config.dropHeight, Is.GreaterThan(config.parachuteHeight));
            Assert.That(config.freefallSpeed, Is.GreaterThan(config.parachuteSpeed));
            Assert.That(System.Enum.IsDefined(typeof(MatchState), MatchState.Staging), Is.True);
        }

        [Test]
        public void BotRotatesBeforeUpcomingZoneCloses()
        {
            Assert.That(BotController.NeedsZoneRotation(new Vector3(60f, 0f, 0f), Vector3.zero, 90f,
                new Vector3(8f, 0f, 0f), 52f), Is.True);
            Assert.That(BotController.NeedsZoneRotation(new Vector3(20f, 0f, 0f), Vector3.zero, 90f,
                new Vector3(8f, 0f, 0f), 52f), Is.False);
        }

        [Test]
        public void BuildingCollisionUsesNonConvexMeshCollider()
        {
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "Collision Test Building";
            var count = BuildingCollisionBuilder.Build(building);
            var meshCollider = building.GetComponent<MeshCollider>();
            Assert.That(count, Is.EqualTo(1));
            Assert.That(meshCollider, Is.Not.Null);
            Assert.That(meshCollider.convex, Is.False);
            Object.DestroyImmediate(building);
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, true)]
        public void CharacterFacesCameraWhileAimingOrFiring(bool aiming, bool firing, bool expected)
        {
            Assert.That(BRCharacterMotor.ShouldFaceCamera(aiming, firing), Is.EqualTo(expected));
        }

        [Test]
        public void HipFireFacesHorizontalCameraForwardWithoutMovement()
        {
            var character = new GameObject("Hip Fire Facing Test");
            CharacterPresentationProfile.Apply(character.AddComponent<CharacterController>());
            var motor = character.AddComponent<BRCharacterMotor>();
            var cameraObject = new GameObject("Hip Fire Facing Camera");
            cameraObject.transform.rotation = Quaternion.Euler(24f, 90f, 0f);
            var input = new BRInputFrame(Vector2.zero, Vector2.zero, false, false, false, false,
                true, true, false, false, false, false, -1, false, false);

            motor.Move(input, cameraObject.transform, new BRGameConfig(), false);

            var expectedForward = Vector3.ProjectOnPlane(cameraObject.transform.forward, Vector3.up).normalized;
            Assert.That(Vector3.Dot(character.transform.forward, expectedForward), Is.GreaterThan(0.999f));
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(character);
        }

        [Test]
        public void VaultProbesMatchResizedCharacterProfile()
        {
            Assert.That(BRCharacterMotor.LowVaultProbeHeight, Is.EqualTo(0.40f));
            Assert.That(BRCharacterMotor.HighVaultProbeHeight, Is.EqualTo(1.24f));
            Assert.That(BRCharacterMotor.VaultForwardProbeDistance, Is.EqualTo(0.92f));
            Assert.That(BRCharacterMotor.MaxVaultLandingHeight, Is.EqualTo(0.82f));
            Assert.That(BRCharacterMotor.CompactVaultHeight, Is.EqualTo(0.58f));
            Assert.That(BRCharacterMotor.CompactVaultArcHeight, Is.EqualTo(0.60f));
            Assert.That(BRCharacterMotor.CompactVaultGroundClearance, Is.EqualTo(0.10f));
            Assert.That(BRCharacterMotor.CompactVaultRadius, Is.EqualTo(0.19f));
            Assert.That(BRCharacterMotor.CompactVaultForwardDistance, Is.EqualTo(1.58f));
            Assert.That(BRCharacterMotor.LowVaultProbeHeight, Is.LessThan(CharacterPresentationProfile.ControllerCenterY));
            Assert.That(BRCharacterMotor.HighVaultProbeHeight, Is.GreaterThan(CharacterPresentationProfile.ControllerHeight));
            Assert.That(BRCharacterMotor.MaxVaultLandingHeight, Is.LessThan(BRCharacterMotor.HighVaultProbeHeight));
        }

        [Test]
        public void VaultStartsAcrossClearLowObstacle()
        {
            var fixture = CreateVaultFixture(500f);

            Assert.That(InvokeTryStartVault(fixture.motor), Is.True);
            Assert.That(fixture.motor.Vaulting, Is.True);
            Assert.That(fixture.controller.enabled, Is.False);

            DestroyVaultFixture(fixture);
        }

        [Test]
        public void FiringFacesCameraOnVaultStartFrame()
        {
            var fixture = CreateVaultFixture(505f);
            fixture.obstacle.transform.position = new Vector3(fixture.x, 0.30f, 0.68f);
            fixture.obstacle.transform.localScale = new Vector3(0.9f, 0.60f, 0.18f);
            Physics.SyncTransforms();
            fixture.controller.Move(Vector3.down * 0.05f);
            Assert.That(fixture.controller.isGrounded, Is.True);
            var cameraObject = new GameObject("Vault Start Fire Camera");
            cameraObject.transform.rotation = Quaternion.Euler(18f, 90f, 0f);
            var input = new BRInputFrame(Vector2.left, Vector2.zero, true, false, false, false,
                true, true, false, false, false, false, -1, false, false);

            fixture.motor.Move(input, cameraObject.transform, new BRGameConfig(), true);

            Assert.That(fixture.motor.Vaulting, Is.True);
            Assert.That(Vector3.Dot(fixture.character.transform.forward, Vector3.right), Is.GreaterThan(0.999f));
            Object.DestroyImmediate(cameraObject);
            DestroyVaultFixture(fixture);
        }

        [Test]
        public void FiringFacesCameraDuringActiveVaultUpdate()
        {
            var fixture = CreateVaultFixture(507f);
            Assert.That(InvokeTryStartVault(fixture.motor), Is.True);
            var cameraObject = new GameObject("Active Vault Fire Camera");
            cameraObject.transform.rotation = Quaternion.Euler(-12f, 90f, 0f);
            var input = new BRInputFrame(Vector2.zero, Vector2.zero, false, false, false, false,
                true, true, false, false, false, false, -1, false, false);

            fixture.motor.Move(input, cameraObject.transform, new BRGameConfig(), true);

            Assert.That(Vector3.Dot(fixture.character.transform.forward, Vector3.right), Is.GreaterThan(0.999f));
            Object.DestroyImmediate(cameraObject);
            DestroyVaultFixture(fixture);
        }

        [TestCase("Side")]
        [TestCase("Ceiling")]
        [TestCase("Landing")]
        public void VaultRejectsBlockedCapsulePath(string blockerKind)
        {
            var fixture = CreateVaultFixture(blockerKind == "Side" ? 510f : blockerKind == "Ceiling" ? 520f : 530f);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = $"Vault {blockerKind} Blocker";
            if (blockerKind == "Side")
            {
                blocker.transform.position = new Vector3(fixture.x + 0.19f, 0.65f, 0.72f);
                blocker.transform.localScale = new Vector3(0.1f, 1.3f, 0.5f);
            }
            else if (blockerKind == "Ceiling")
            {
                blocker.transform.position = new Vector3(fixture.x, 0.98f, 0.72f);
                blocker.transform.localScale = new Vector3(1f, 0.14f, 1f);
            }
            else
            {
                blocker.transform.position = new Vector3(fixture.x + 0.19f, 0.55f, 1.32f);
                blocker.transform.localScale = new Vector3(0.1f, 1.1f, 0.2f);
            }
            Physics.SyncTransforms();

            Assert.That(InvokeTryStartVault(fixture.motor), Is.False);
            Assert.That(fixture.motor.Vaulting, Is.False);
            Assert.That(fixture.controller.enabled, Is.True);

            Object.DestroyImmediate(blocker);
            DestroyVaultFixture(fixture);
        }

        [Test]
        public void VaultUsesCompactPostureUnderPassableLintel()
        {
            var fixture = CreateVaultFixture(535f);
            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "Vault Passable Lintel";
            lintel.transform.position = new Vector3(fixture.x, 1.5f, 0.72f);
            lintel.transform.localScale = new Vector3(1f, 0.14f, 1f);
            Physics.SyncTransforms();

            Assert.That(InvokeTryStartVault(fixture.motor), Is.True);
            Assert.That(fixture.motor.CompactVaulting, Is.True);

            Object.DestroyImmediate(lintel);
            DestroyVaultFixture(fixture);
        }

        [Test]
        public void VaultLandingSkipsOutOfRangeSurfaceAboveValidGround()
        {
            var fixture = CreateVaultFixture(538f);
            var overhead = GameObject.CreatePrimitive(PrimitiveType.Cube);
            overhead.name = "Vault Landing Overhead Surface";
            overhead.transform.position = new Vector3(fixture.x, 1.75f, 1.32f);
            overhead.transform.localScale = new Vector3(1f, 0.12f, 0.28f);
            Physics.SyncTransforms();

            Assert.That(InvokeTryStartVault(fixture.motor), Is.True);
            Assert.That(fixture.motor.Vaulting, Is.True);

            Object.DestroyImmediate(overhead);
            DestroyVaultFixture(fixture);
        }

        [Test]
        public void TeleportCancelsVaultAndPreservesControllerEnabledState()
        {
            var fixture = CreateVaultFixture(540f);
            Assert.That(InvokeTryStartVault(fixture.motor), Is.True);

            fixture.motor.Teleport(new Vector3(540f, 0f, 3f));

            Assert.That(fixture.motor.Vaulting, Is.False);
            Assert.That(fixture.controller.enabled, Is.True);
            fixture.controller.enabled = false;
            fixture.motor.Teleport(new Vector3(540f, 0f, 4f));
            Assert.That(fixture.controller.enabled, Is.False);
            DestroyVaultFixture(fixture);
        }

        [Test]
        public void DisablingMotorCancelsVaultAndRestoresController()
        {
            var fixture = CreateVaultFixture(550f);
            Assert.That(InvokeTryStartVault(fixture.motor), Is.True);

            var onDisable = typeof(BRCharacterMotor).GetMethod("OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(onDisable, Is.Not.Null);
            onDisable.Invoke(fixture.motor, null);

            Assert.That(fixture.motor.Vaulting, Is.False);
            Assert.That(fixture.controller.enabled, Is.True);
            DestroyVaultFixture(fixture);
        }

        [Test]
        public void VaultCancelsWithoutMovingWhenTraversalBecomesBlocked()
        {
            var fixture = CreateVaultFixture(560f);
            Assert.That(InvokeTryStartVault(fixture.motor), Is.True);
            var positionBeforeTick = fixture.character.transform.position;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Dynamic Vault Blocker";
            blocker.transform.position = positionBeforeTick + Vector3.up * 0.55f;
            blocker.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            Physics.SyncTransforms();
            var tickVault = typeof(BRCharacterMotor).GetMethod("TickVault",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(tickVault, Is.Not.Null);

            tickVault.Invoke(fixture.motor, null);

            Assert.That(fixture.motor.Vaulting, Is.False);
            Assert.That(Vector3.Distance(fixture.character.transform.position, positionBeforeTick), Is.LessThan(0.0001f));
            Object.DestroyImmediate(blocker);
            DestroyVaultFixture(fixture);
        }

        [Test]
        public void MovementTurnStartsFromExternalTransformRotation()
        {
            var character = new GameObject("External Rotation Motor Test");
            CharacterPresentationProfile.Apply(character.AddComponent<CharacterController>());
            var motor = character.AddComponent<BRCharacterMotor>();
            var cameraObject = new GameObject("External Rotation Camera");
            character.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var input = new BRInputFrame(Vector2.up, Vector2.zero, false, false, false, false,
                false, false, false, false, false, false, -1, false, false);

            motor.Move(input, cameraObject.transform, new BRGameConfig(), false);

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, character.transform.eulerAngles.y)), Is.GreaterThan(1f));
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(character);
        }

        [Test]
        public void ExponentialTurnInterpolationIsFrameRateIndependent()
        {
            var whole = BRCharacterMotor.TurnInterpolationFactor(14f, 1f / 30f);
            var half = BRCharacterMotor.TurnInterpolationFactor(14f, 1f / 60f);

            Assert.That(whole, Is.EqualTo(1f - (1f - half) * (1f - half)).Within(0.00001f));
            Assert.That(BRCharacterMotor.TurnInterpolationFactor(14f, 0f), Is.Zero);
        }

        [Test]
        public void WorldTurnIsEquivalentAcrossMultipleTimeSteps()
        {
            var start = Quaternion.identity;
            var direction = new Vector3(1f, 0f, 1f);
            var whole = BRCharacterMotor.TurnTowardsWorldDirection(start, direction, 9f, 1f / 30f);
            var stepped = BRCharacterMotor.TurnTowardsWorldDirection(start, direction, 9f, 1f / 60f);
            stepped = BRCharacterMotor.TurnTowardsWorldDirection(stepped, direction, 9f, 1f / 60f);

            Assert.That(Quaternion.Angle(whole, stepped), Is.LessThan(0.0001f));
        }

        [Test]
        public void NormalShotDoesNotDamageTeammate()
        {
            BRParticipant shooter = null;
            BRParticipant teammate = null;
            WeaponDefinition weapon = null;
            try
            {
                shooter = CreateParticipant("Physical Friendly Fire Shooter", true, 30);
                teammate = CreateParticipant("Physical Friendly Fire Target", false, 30);
                shooter.transform.position = new Vector3(850f, 0f, 0f);
                teammate.transform.position = new Vector3(850f, 0f, 8f);
                shooter.transform.rotation = Quaternion.identity;
                shooter.GetComponent<CharacterController>().enabled = false;
                var hitbox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hitbox.name = "Physical Friendly Fire Hitbox";
                hitbox.transform.SetParent(teammate.transform, false);
                hitbox.transform.localPosition = Vector3.up * CharacterPresentationProfile.MuzzleHeight;
                hitbox.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                weapon.damage = 50f;
                weapon.magazineSize = 2;
                weapon.range = 30f;
                weapon.standingSpreadDegrees = 0f;
                weapon.movingSpreadDegrees = 0f;
                shooter.Weapon.EquipInitial(weapon);
                Physics.SyncTransforms();

                var shotOrigin = CharacterPresentationProfile.FallbackMuzzle(shooter.transform);
                Assert.That(Physics.Raycast(shotOrigin, shooter.transform.forward, out var trajectoryHit, weapon.range),
                    Is.True);
                Assert.That(trajectoryHit.collider.GetComponentInParent<BRParticipant>(), Is.SameAs(teammate));
                var magazineBefore = shooter.Weapon.Magazine;

                Assert.That(shooter.Weapon.TryFire(null, false, shooter.gameObject), Is.True);
                Assert.That(shooter.Weapon.Magazine, Is.EqualTo(magazineBefore - 1));
                Assert.That(teammate.Health.Health, Is.EqualTo(teammate.Health.MaxHealth));
                Assert.That(shooter.Weapon.LastShotHit, Is.False);
                Assert.That(shooter.Weapon.LastShotDamage, Is.Zero);
            }
            finally
            {
                if (weapon != null) Object.DestroyImmediate(weapon);
                DestroyParticipants(shooter, teammate);
                DestroyWeaponFeedback();
            }
        }

        [Test]
        public void WallBlocksMuzzleShotBeforeTarget()
        {
            var shooter = new GameObject("Ballistic Test Shooter");
            shooter.AddComponent<CharacterController>();
            shooter.AddComponent<BRCharacterMotor>();
            shooter.AddComponent<InventorySystem>();
            var weaponSystem = shooter.AddComponent<WeaponSystem>();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.damage = 50f;
            weapon.magazineSize = 10;
            weapon.range = 30f;
            weapon.standingSpreadDegrees = 0f;
            weapon.movingSpreadDegrees = 0f;
            weaponSystem.EquipInitial(weapon);

            var cameraObject = new GameObject("Ballistic Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(1f, CharacterPresentationProfile.MuzzleHeight, -2f);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Ballistic Test Wall";
            wall.transform.position = new Vector3(0f, CharacterPresentationProfile.MuzzleHeight, 0.8f);
            wall.transform.localScale = new Vector3(0.3f, 1f, 0.1f);
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "Ballistic Test Target";
            target.transform.position = new Vector3(0f, CharacterPresentationProfile.MuzzleHeight, 8f);
            var health = target.AddComponent<HealthArmorSystem>();
            health.InitializeFull();
            cameraObject.transform.LookAt(target.transform.position);
            Physics.SyncTransforms();

            Assert.That(weaponSystem.TryFire(camera, false, shooter), Is.True);
            Assert.That(health.Health, Is.EqualTo(health.MaxHealth));
            Assert.That(weaponSystem.LastShotEndPoint.z, Is.LessThan(1f));

            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(shooter);
            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(target);
            foreach (var line in Object.FindObjectsByType<LineRenderer>())
                if (line.name == "Shot Tracer") Object.DestroyImmediate(line.gameObject);
            foreach (var marker in Object.FindObjectsByType<Renderer>())
                if (marker.name is "Pooled Impact Marker" or "Muzzle Flash") Object.DestroyImmediate(marker.gameObject);
        }

        [Test]
        public void DenseIgnoredHitsDoNotHideNearestMuzzleBlocker()
        {
            var shooter = new GameObject("Dense Ballistic Shooter");
            shooter.transform.position = new Vector3(600f, 0f, 0f);
            shooter.AddComponent<CharacterController>();
            shooter.AddComponent<BRCharacterMotor>();
            shooter.AddComponent<InventorySystem>();
            var weaponSystem = shooter.AddComponent<WeaponSystem>();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.magazineSize = 2;
            weapon.range = 30f;
            weapon.standingSpreadDegrees = 0f;
            weapon.movingSpreadDegrees = 0f;
            weaponSystem.EquipInitial(weapon);
            for (var i = 0; i < 24; i++)
            {
                var ignored = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ignored.name = $"Dense Ignored Muzzle {i}";
                ignored.transform.SetParent(shooter.transform, false);
                ignored.transform.localPosition = new Vector3(0f, CharacterPresentationProfile.MuzzleHeight,
                    CharacterPresentationProfile.MuzzleForward + 0.03f + i * 0.018f);
                ignored.transform.localScale = new Vector3(0.04f, 0.2f, 0.01f);
            }
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = new Vector3(600f, CharacterPresentationProfile.MuzzleHeight, 1.2f);
            blocker.transform.localScale = new Vector3(0.5f, 1f, 0.1f);
            Physics.SyncTransforms();

            Assert.That(weaponSystem.TryFire(null, false, shooter), Is.True);
            Assert.That(weaponSystem.LastShotEndPoint.z, Is.LessThan(1.3f));

            Object.DestroyImmediate(blocker);
            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(shooter);
        }

        [Test]
        public void AnimationStateUsesGroundingAndPlanarSpeed()
        {
            Assert.That(CharacterVisualAnimator.EvaluateState(0f, true), Is.EqualTo(CharacterAnimationState.Idle));
            Assert.That(CharacterVisualAnimator.EvaluateState(0.14f, true), Is.EqualTo(CharacterAnimationState.Idle));
            Assert.That(CharacterVisualAnimator.EvaluateState(0.15f, true), Is.EqualTo(CharacterAnimationState.Run));
            Assert.That(CharacterVisualAnimator.EvaluateState(5f, false), Is.EqualTo(CharacterAnimationState.Jump));
            Assert.That(CharacterVisualAnimator.EvaluateState(0f, true, true), Is.EqualTo(CharacterAnimationState.Crouch));
        }

        [Test]
        public void PresentationAnimationStatePrioritizesReloadTraversalAndAim()
        {
            Assert.That(CharacterVisualAnimator.EvaluatePresentationState(0f, true, false, true, false, true),
                Is.EqualTo(CharacterAnimationState.Reload));
            Assert.That(CharacterVisualAnimator.EvaluatePresentationState(5f, false, false, true, true, true),
                Is.EqualTo(CharacterAnimationState.Jump));
            Assert.That(CharacterVisualAnimator.EvaluatePresentationState(0f, true, false, true, true, false),
                Is.EqualTo(CharacterAnimationState.Aim));
            Assert.That(CharacterVisualAnimator.EvaluatePresentationState(2f, true, false, true, true, false),
                Is.EqualTo(CharacterAnimationState.Walk));
            Assert.That(CharacterVisualAnimator.EvaluatePresentationState(2f, true, false, true, false, false),
                Is.EqualTo(CharacterAnimationState.Walk));
            Assert.That(CharacterVisualAnimator.EvaluatePresentationState(4f, true, false, true, false, false),
                Is.EqualTo(CharacterAnimationState.Run));
        }

        [Test]
        public void ImportedPlayerProfileHasHumanoidAvatarAndCombatClips()
        {
            Assert.That(CharacterVisualCatalog.Player, Is.Not.SameAs(CharacterVisualCatalog.Bot));
            Assert.That(CharacterVisualCatalog.Player.Fallback, Is.SameAs(CharacterVisualCatalog.Lightweight));
            Assert.That(CharacterVisualCatalog.ResolveAvailable(CharacterVisualCatalog.Player),
                Is.SameAs(CharacterVisualCatalog.Player));

            var prefab = CharacterVisualCatalog.Player.LoadPrefab();
            Assert.That(prefab, Is.Not.Null);
            var animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.avatar.isHuman, Is.True);
            Assert.That(animator.GetBoneTransform(HumanBodyBones.RightHand), Is.Not.Null);

            var clips = CharacterVisualCatalog.Player.LoadClips();
            Assert.That(clips, Has.Some.Matches<AnimationClip>(clip => clip.name == "Standard Walk"));
            Assert.That(clips, Has.Some.Matches<AnimationClip>(clip => clip.name == "Rifle Aiming Idle"));
            Assert.That(clips, Has.Some.Matches<AnimationClip>(clip => clip.name == "Reloading"));
        }

        [TestCase("ScarH")]
        [TestCase("Kriss_Vector")]
        [TestCase("Spas_12")]
        [TestCase("L115_Awp")]
        [TestCase("Glock17")]
        public void ImportedWeaponHasRenderableGeometry(string prefabName)
        {
            var weapon = Resources.Load<GameObject>(ImportedGameplayVisuals.WeaponPrefabRoot + prefabName);
            Assert.That(weapon, Is.Not.Null);
            Assert.That(weapon.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
        }

        [TestCase(ImportedGameplayVisuals.ParachuteModel, ImportedGameplayVisuals.ParachuteTextureRoot)]
        [TestCase(ImportedGameplayVisuals.DeathCrateModel, ImportedGameplayVisuals.DeathCrateTextureRoot)]
        public void ImportedGameplayModelIncludesPbrTextures(string modelPath, string textureRoot)
        {
            var modelAsset = Resources.Load<GameObject>(modelPath);
            Assert.That(modelAsset, Is.Not.Null);
            Assert.That(modelAsset.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            Assert.That(Resources.Load<Texture2D>(textureRoot + "basecolor"), Is.Not.Null);
            Assert.That(Resources.Load<Texture2D>(textureRoot + "normal"), Is.Not.Null);
            Assert.That(Resources.Load<Texture2D>(textureRoot + "metallic"), Is.Not.Null);
            Assert.That(Resources.Load<Texture2D>(textureRoot + "roughness"), Is.Not.Null);
        }

        [Test]
        public void DeploymentVisibilityRestoresOriginalRendererAndColliderState()
        {
            var root = new GameObject("Hidden Passenger");
            root.AddComponent<CharacterController>();
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(root.transform);
            var visibility = root.AddComponent<DeploymentVisibility>();

            visibility.SetVisible(false);
            Assert.That(visual.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(visual.GetComponent<Collider>().enabled, Is.False);
            Assert.That(root.GetComponent<CharacterController>().enabled, Is.False);

            visibility.SetVisible(true);
            Assert.That(visual.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(visual.GetComponent<Collider>().enabled, Is.True);
            Assert.That(root.GetComponent<CharacterController>().enabled, Is.True);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void StartingActiveMatchEjectsEveryRemainingPassenger()
        {
            var dropObject = new GameObject("Drop Transition Test");
            var drop = dropObject.AddComponent<DropSystem>();
            drop.Configure(new BRGameConfig());
            drop.BeginRoute();
            var first = CreateParticipant("Waiting Player", true);
            var second = CreateParticipant("Waiting Bot", false);
            var participants = new[] { first, second };

            drop.EjectRemaining(participants);

            Assert.That(drop.CountPassengers(participants), Is.Zero);
            Assert.That(first.Phase, Is.EqualTo(ParticipantPhase.Freefall));
            Assert.That(second.Phase, Is.EqualTo(ParticipantPhase.Freefall));
            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(dropObject);
        }

        [Test]
        public void DamageFeedbackUsesYellowForBodyAndRedForHeadshot()
        {
            Assert.That(DamageFeedbackSystem.BodyColor.r, Is.GreaterThan(0.9f));
            Assert.That(DamageFeedbackSystem.BodyColor.g, Is.GreaterThan(0.7f));
            Assert.That(DamageFeedbackSystem.HeadshotColor.r, Is.GreaterThan(0.9f));
            Assert.That(DamageFeedbackSystem.HeadshotColor.g, Is.LessThan(0.3f));
        }

        [Test]
        public void BulletVisualUsesShortClassSpecificStreaks()
        {
            foreach (WeaponClass weaponClass in System.Enum.GetValues(typeof(WeaponClass)))
            {
                Assert.That(BulletVisual.StreakLengthFor(weaponClass), Is.GreaterThan(0.3f));
                Assert.That(BulletVisual.StreakLengthFor(weaponClass), Is.LessThanOrEqualTo(1.5f));
                Assert.That(BulletVisual.SpeedFor(weaponClass), Is.GreaterThan(90f));
            }
            Assert.That(BulletVisual.SpeedFor(WeaponClass.Sniper), Is.GreaterThan(BulletVisual.SpeedFor(WeaponClass.Shotgun)));
            Assert.That(BulletVisual.MaxActive, Is.LessThanOrEqualTo(40));
        }

        [Test]
        public void MobileActionButtonsDoNotOverlap()
        {
            var controls = new[]
            {
                MobileControlLayout.Fire, MobileControlLayout.Aim, MobileControlLayout.Jump,
                MobileControlLayout.Interact, MobileControlLayout.Reload, MobileControlLayout.Heal,
                MobileControlLayout.Swap
            };
            for (var i = 0; i < controls.Length; i++)
            for (var j = i + 1; j < controls.Length; j++)
                Assert.That(controls[i].Overlaps(controls[j]), Is.False, $"Touch controls {i} and {j} overlap");
        }

        [Test]
        public void MobilePointerCapturesFireOnlyInsideRectWhenFree()
        {
            var state = new MobilePointerState();
            var fireRect = new Rect(100f, 100f, 80f, 80f);

            Assert.That(state.FireFingerId, Is.EqualTo(-1));
            Assert.That(state.TryBeginFire(4, new Vector2(50f, 50f), fireRect), Is.False);
            Assert.That(state.TryBeginFire(4, new Vector2(120f, 130f), fireRect), Is.True);
            Assert.That(state.TryBeginFire(8, new Vector2(120f, 130f), fireRect), Is.False);
            Assert.That(state.IsFiring(4), Is.True);
        }

        [Test]
        public void CapturedFireKeepsFiringAndLookingAfterDraggingOutsideButton()
        {
            var state = new MobilePointerState();
            Assert.That(state.TryBeginFire(7, new Vector2(120f, 130f), new Rect(100f, 100f, 80f, 80f)), Is.True);

            var look = state.FilterFireLook(7, new Vector2(8f, -5f), TouchPhase.Moved, 0.9f);

            Assert.That(state.IsFiring(7), Is.True);
            Assert.That(look, Is.EqualTo(new Vector2(8f, -5f) * 0.055f * 0.9f));
        }

        [Test]
        public void FireDragDeadzoneAndBeganPhasePreventCameraJump()
        {
            var state = new MobilePointerState();
            state.TryBeginFire(2, Vector2.one, new Rect(0f, 0f, 10f, 10f));

            Assert.That(state.FilterFireLook(2, new Vector2(20f, 10f), TouchPhase.Began, 1f), Is.EqualTo(Vector2.zero));
            Assert.That(state.FilterFireLook(2, new Vector2(3.5f, 0f), TouchPhase.Moved, 1f), Is.EqualTo(Vector2.zero));
            Assert.That(state.FilterFireLook(2, new Vector2(3.6f, 0f), TouchPhase.Moved, 1f).x,
                Is.EqualTo(3.6f * 0.055f).Within(0.0001f));
        }

        [Test]
        public void FireDragIgnoresMovementFromWrongPointer()
        {
            var state = new MobilePointerState();
            state.TryBeginFire(3, Vector2.one, new Rect(0f, 0f, 10f, 10f));

            Assert.That(state.FilterFireLook(9, new Vector2(12f, 4f), TouchPhase.Moved, 1f), Is.EqualTo(Vector2.zero));
            state.End(9);
            Assert.That(state.IsFiring(3), Is.True);
        }

        [Test]
        public void MobileLookPointerOwnershipIsExclusiveAndNeverStealsFire()
        {
            var state = new MobilePointerState();
            state.TryBeginFire(5, Vector2.one, new Rect(0f, 0f, 10f, 10f));

            Assert.That(state.TryBeginLook(5, false), Is.False);
            Assert.That(state.TryBeginLook(6, true), Is.False);
            Assert.That(state.TryBeginLook(6, false), Is.True);
            Assert.That(state.TryBeginLook(8, false), Is.False);
            Assert.That(state.IsLooking(6), Is.True);
        }

        [Test]
        public void DesktopCombatMapperMatchesProductionMouseControls()
        {
            var mapped = DesktopInputMapper.Map(new Vector2(3f, -2f), true, true, true, false);

            Assert.That(mapped.Look.x, Is.EqualTo(6.3f).Within(0.0001f));
            Assert.That(mapped.Look.y, Is.EqualTo(-4.2f).Within(0.0001f));
            Assert.That(mapped.Fire, Is.True);
            Assert.That(mapped.FirePressed, Is.True);
            Assert.That(mapped.Aim, Is.True);
        }

        [Test]
        public void DesktopCombatMapperSuppressesMouseControlsOnMobileRuntime()
        {
            var mapped = DesktopInputMapper.Map(new Vector2(3f, -2f), true, true, true, true);

            Assert.That(mapped.Look, Is.EqualTo(Vector2.zero));
            Assert.That(mapped.Fire, Is.False);
            Assert.That(mapped.FirePressed, Is.False);
            Assert.That(mapped.Aim, Is.False);
        }

        [Test]
        public void MobilePointerEndAndResetReleaseOwnedPointers()
        {
            var state = new MobilePointerState();
            state.TryBeginFire(1, Vector2.one, new Rect(0f, 0f, 10f, 10f));
            state.TryBeginLook(2, false);

            state.End(1);
            Assert.That(state.FireFingerId, Is.EqualTo(-1));
            Assert.That(state.LookFingerId, Is.EqualTo(2));

            state.Reset();
            Assert.That(state.FireFingerId, Is.EqualTo(-1));
            Assert.That(state.LookFingerId, Is.EqualTo(-1));
        }

        [Test]
        public void FireDragSensitivityClampsRoundsAndCanBeRestored()
        {
            const string key = "RavenDrop.FireDragSensitivity";
            var originalFireDrag = GamePreferences.FireDragSensitivity;
            var originalLook = GamePreferences.LookSensitivity;
            var originalAds = GamePreferences.AdsSensitivity;
            var originalFrameRate = GamePreferences.FrameRate;
            var hadStoredValue = PlayerPrefs.HasKey(key);
            var storedValue = PlayerPrefs.GetFloat(key);
            var originalQualityLevel = QualitySettings.GetQualityLevel();
            var originalAnisotropicFiltering = QualitySettings.anisotropicFiltering;
            var originalVSync = QualitySettings.vSyncCount;
            var originalTargetFrameRate = Application.targetFrameRate;
            try
            {
                PlayerPrefs.DeleteKey(key);
                GamePreferences.LoadAndApply();
                Assert.That(GamePreferences.FireDragSensitivity, Is.EqualTo(0.9f));

                GamePreferences.SetFireDragSensitivity(20f);
                Assert.That(GamePreferences.FireDragSensitivity, Is.EqualTo(1.4f));
                GamePreferences.SetFireDragSensitivity(-20f);
                Assert.That(GamePreferences.FireDragSensitivity, Is.EqualTo(0.45f));
                GamePreferences.SetFireDragSensitivity(0.93f);
                Assert.That(GamePreferences.FireDragSensitivity, Is.EqualTo(0.95f));
                Assert.That(PlayerPrefs.GetFloat(key), Is.EqualTo(0.95f));

                PlayerPrefs.SetFloat(key, 1.2f);
                GamePreferences.LoadAndApply();
                Assert.That(GamePreferences.FireDragSensitivity, Is.EqualTo(1.2f));
            }
            finally
            {
                if (hadStoredValue) PlayerPrefs.SetFloat(key, storedValue);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                SetGamePreference(nameof(GamePreferences.FireDragSensitivity), originalFireDrag);
                SetGamePreference(nameof(GamePreferences.LookSensitivity), originalLook);
                SetGamePreference(nameof(GamePreferences.AdsSensitivity), originalAds);
                SetGamePreference(nameof(GamePreferences.FrameRate), originalFrameRate);
                QualitySettings.SetQualityLevel(originalQualityLevel, true);
                QualitySettings.anisotropicFiltering = originalAnisotropicFiltering;
                QualitySettings.vSyncCount = originalVSync;
                Application.targetFrameRate = originalTargetFrameRate;
            }
        }

        [Test]
        public void MobileTouchRoutingCombinesMovementFireAndFireDragLook()
        {
            var state = new MobilePointerState();
            var controls = TestMobileControls();
            state.RouteTouches(new[]
            {
                new MobileTouchSample(1, new Vector2(100f, 80f), Vector2.zero, TouchPhase.Began),
                new MobileTouchSample(2, controls.Fire.center, Vector2.zero, TouchPhase.Began)
            }, controls, 0.9f);

            var frame = state.RouteTouches(new[]
            {
                new MobileTouchSample(1, new Vector2(100f, 80f), Vector2.zero, TouchPhase.Stationary),
                new MobileTouchSample(2, new Vector2(470f, 220f), new Vector2(8f, -4f), TouchPhase.Moved)
            }, controls, 0.9f);

            Assert.That(frame.Move.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(frame.Fire, Is.True);
            Assert.That(frame.FirePressed, Is.False);
            Assert.That(frame.Look, Is.EqualTo(new Vector2(8f, -4f) * 0.055f * 0.9f));
        }

        [Test]
        public void MobileTouchRoutingFreeLookNeverFires()
        {
            var state = new MobilePointerState();
            var controls = TestMobileControls();
            var began = state.RouteTouches(new[]
            {
                new MobileTouchSample(4, new Vector2(460f, 280f), new Vector2(12f, 3f), TouchPhase.Began)
            }, controls, 0.9f);
            var moved = state.RouteTouches(new[]
            {
                new MobileTouchSample(4, new Vector2(470f, 285f), new Vector2(10f, 5f), TouchPhase.Moved)
            }, controls, 0.9f);

            Assert.That(began.Look, Is.EqualTo(Vector2.zero));
            Assert.That(began.Fire, Is.False);
            Assert.That(moved.Look, Is.EqualTo(new Vector2(10f, 5f) * 0.055f));
            Assert.That(moved.Fire, Is.False);
        }

        [Test]
        public void MobileTouchRoutingKeepsHeldFireOutsideOriginalRect()
        {
            var state = new MobilePointerState();
            var controls = TestMobileControls();
            state.RouteTouches(new[]
            {
                new MobileTouchSample(6, controls.Fire.center, Vector2.zero, TouchPhase.Began)
            }, controls, 1f);

            var frame = state.RouteTouches(new[]
            {
                new MobileTouchSample(6, new Vector2(40f, 300f), new Vector2(-9f, 6f), TouchPhase.Moved)
            }, controls, 1f);

            Assert.That(frame.Fire, Is.True);
            Assert.That(frame.Look, Is.EqualTo(new Vector2(-9f, 6f) * 0.055f));
        }

        [TestCase(TouchPhase.Ended)]
        [TestCase(TouchPhase.Canceled)]
        public void MobileTouchRoutingTerminalPhaseReleasesFireOutsideButton(TouchPhase phase)
        {
            var state = new MobilePointerState();
            var controls = TestMobileControls();
            state.RouteTouches(new[]
            {
                new MobileTouchSample(7, controls.Fire.center, Vector2.zero, TouchPhase.Began)
            }, controls, 1f);

            var frame = state.RouteTouches(new[]
            {
                new MobileTouchSample(7, new Vector2(20f, 300f), new Vector2(20f, 20f), phase)
            }, controls, 1f);

            Assert.That(frame.Fire, Is.False);
            Assert.That(state.FireFingerId, Is.EqualTo(-1));
        }

        [TestCase(TouchPhase.Ended, true)]
        [TestCase(TouchPhase.Ended, false)]
        [TestCase(TouchPhase.Canceled, true)]
        [TestCase(TouchPhase.Canceled, false)]
        public void MobileFireReplacementOwnershipIsIndependentOfSampleOrder(TouchPhase phase, bool terminalFirst)
        {
            var state = new MobilePointerState();
            var controls = TestMobileControls();
            state.TryBeginFire(10, controls.Fire.center, controls.Fire);
            var terminal = new MobileTouchSample(10, controls.Aim.center, Vector2.one * 20f, phase);
            var replacement = new MobileTouchSample(11, controls.Fire.center, Vector2.one * 20f, TouchPhase.Began);
            var samples = terminalFirst
                ? new[] { terminal, replacement }
                : new[] { replacement, terminal };

            var frame = state.RouteTouches(samples, controls, 1f);

            Assert.That(state.FireFingerId, Is.EqualTo(11));
            Assert.That(frame.Fire, Is.True);
            Assert.That(frame.FirePressed, Is.True);
            Assert.That(frame.Look, Is.EqualTo(Vector2.zero));
            Assert.That(frame.Aim, Is.False);
        }

        [TestCase(TouchPhase.Ended, true)]
        [TestCase(TouchPhase.Ended, false)]
        [TestCase(TouchPhase.Canceled, true)]
        [TestCase(TouchPhase.Canceled, false)]
        public void MobileLookReplacementOwnershipIsIndependentOfSampleOrder(TouchPhase phase, bool terminalFirst)
        {
            var state = new MobilePointerState();
            var controls = TestMobileControls();
            state.TryBeginLook(20, false);
            var terminal = new MobileTouchSample(20, controls.Aim.center, Vector2.one * 20f, phase);
            var replacement = new MobileTouchSample(21, new Vector2(460f, 280f), Vector2.one * 20f, TouchPhase.Began);
            var samples = terminalFirst
                ? new[] { terminal, replacement }
                : new[] { replacement, terminal };

            var frame = state.RouteTouches(samples, controls, 1f);

            Assert.That(state.LookFingerId, Is.EqualTo(21));
            Assert.That(frame.Look, Is.EqualTo(Vector2.zero));
            Assert.That(frame.Fire, Is.False);
            Assert.That(frame.Aim, Is.False);
        }

        [Test]
        public void MobileTouchRoutingBeganDeltaDoesNotMoveCamera()
        {
            var state = new MobilePointerState();
            var controls = TestMobileControls();

            var frame = state.RouteTouches(new[]
            {
                new MobileTouchSample(9, controls.Fire.center, new Vector2(40f, -30f), TouchPhase.Began)
            }, controls, 1f);

            Assert.That(frame.Fire, Is.True);
            Assert.That(frame.FirePressed, Is.True);
            Assert.That(frame.Look, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void HiddenMobileControlsCannotCaptureOrTrigger()
        {
            var controls = TestMobileControls(false, false, false, false, false, false, false, false);

            var moveState = new MobilePointerState();
            var move = moveState.RouteTouches(new[]
            {
                new MobileTouchSample(1, controls.MoveCenter + Vector2.right * 20f, Vector2.zero, TouchPhase.Began)
            }, controls, 1f);
            Assert.That(move.Move, Is.EqualTo(Vector2.zero));
            Assert.That(moveState.MovementFingerId, Is.EqualTo(-1));

            var fireState = new MobilePointerState();
            var fire = fireState.RouteTouches(new[]
            {
                new MobileTouchSample(2, controls.Fire.center, Vector2.zero, TouchPhase.Began)
            }, controls, 1f);
            Assert.That(fire.Fire, Is.False);
            Assert.That(fireState.FireFingerId, Is.EqualTo(-1));

            var aimState = new MobilePointerState();
            var aim = aimState.RouteTouches(new[]
            {
                new MobileTouchSample(3, controls.Aim.center, Vector2.zero, TouchPhase.Began)
            }, controls, 1f);
            Assert.That(aim.Aim, Is.False);
            Assert.That(aimState.AimFingerId, Is.EqualTo(-1));

            var interactState = new MobilePointerState();
            var interact = interactState.RouteTouches(new[]
            {
                new MobileTouchSample(4, controls.Interact.center, Vector2.zero, TouchPhase.Began),
                new MobileTouchSample(5, controls.Jump.center, Vector2.zero, TouchPhase.Began),
                new MobileTouchSample(6, controls.Reload.center, Vector2.zero, TouchPhase.Began),
                new MobileTouchSample(7, controls.Heal.center, Vector2.zero, TouchPhase.Began),
                new MobileTouchSample(8, controls.Swap.center, Vector2.zero, TouchPhase.Began)
            }, controls, 1f);
            Assert.That(interact.Interact || interact.InteractHeld || interact.Jump || interact.Reload
                || interact.Heal || interact.Swap, Is.False);
            Assert.That(interactState.InteractFingerId, Is.EqualTo(-1));
        }

        [Test]
        public void DisablingCapturedMobileRolesReleasesTheirOwners()
        {
            var state = new MobilePointerState();
            var enabled = TestMobileControls();
            state.RouteTouches(new[]
            {
                new MobileTouchSample(1, enabled.Fire.center, Vector2.zero, TouchPhase.Began),
                new MobileTouchSample(2, enabled.Aim.center, Vector2.zero, TouchPhase.Began),
                new MobileTouchSample(3, enabled.Interact.center, Vector2.zero, TouchPhase.Began),
                new MobileTouchSample(4, enabled.MoveCenter + Vector2.right * 20f, Vector2.zero, TouchPhase.Began)
            }, enabled, 1f);

            var hidden = TestMobileControls(false, false, false, true, false);
            var frame = state.RouteTouches(new[]
            {
                new MobileTouchSample(1, enabled.Fire.center, Vector2.zero, TouchPhase.Stationary),
                new MobileTouchSample(2, enabled.Aim.center, Vector2.zero, TouchPhase.Stationary),
                new MobileTouchSample(3, enabled.Interact.center, Vector2.zero, TouchPhase.Stationary),
                new MobileTouchSample(4, enabled.MoveCenter, Vector2.zero, TouchPhase.Stationary)
            }, hidden, 1f);

            Assert.That(frame.Fire || frame.Aim || frame.InteractHeld || frame.Move != Vector2.zero, Is.False);
            Assert.That(state.FireFingerId, Is.EqualTo(-1));
            Assert.That(state.AimFingerId, Is.EqualTo(-1));
            Assert.That(state.InteractFingerId, Is.EqualTo(-1));
            Assert.That(state.MovementFingerId, Is.EqualTo(-1));
        }

        [Test]
        public void CapturedMovementAimAndInteractRetainRolesAcrossOtherControls()
        {
            var state = new MobilePointerState();
            var controls = TestMobileControls();
            state.RouteTouches(new[]
            {
                new MobileTouchSample(1, controls.MoveCenter + Vector2.right * 20f, Vector2.zero, TouchPhase.Began),
                new MobileTouchSample(2, controls.Aim.center, Vector2.zero, TouchPhase.Began),
                new MobileTouchSample(3, controls.Interact.center, Vector2.zero, TouchPhase.Began)
            }, controls, 1f);

            var frame = state.RouteTouches(new[]
            {
                new MobileTouchSample(1, controls.Fire.center, Vector2.right * 12f, TouchPhase.Moved),
                new MobileTouchSample(2, controls.Fire.center, Vector2.right * 12f, TouchPhase.Moved),
                new MobileTouchSample(3, controls.Jump.center, Vector2.right * 12f, TouchPhase.Moved)
            }, controls, 1f);

            Assert.That(frame.Move.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(frame.Aim, Is.True);
            Assert.That(frame.Interact, Is.False);
            Assert.That(frame.InteractHeld, Is.True);
            Assert.That(frame.Fire || frame.Jump, Is.False);
            Assert.That(state.MovementFingerId, Is.EqualTo(1));
            Assert.That(state.AimFingerId, Is.EqualTo(2));
            Assert.That(state.InteractFingerId, Is.EqualTo(3));
        }

        [Test]
        public void MultipleMovementFingersDoNotSumJoystickInput()
        {
            var state = new MobilePointerState();
            var controls = TestMobileControls();

            var frame = state.RouteTouches(new[]
            {
                new MobileTouchSample(1, controls.MoveCenter + Vector2.right * 20f, Vector2.zero, TouchPhase.Began),
                new MobileTouchSample(2, controls.MoveCenter + Vector2.left * 20f, Vector2.zero, TouchPhase.Began)
            }, controls, 1f);

            Assert.That(frame.Move, Is.EqualTo(Vector2.right * 0.5f));
            Assert.That(state.MovementFingerId, Is.EqualTo(1));
        }

        [Test]
        public void NonBeganTouchesCannotSlideIntoRolesOrOneShotButtons()
        {
            var state = new MobilePointerState();
            var controls = TestMobileControls();

            var frame = state.RouteTouches(new[]
            {
                new MobileTouchSample(1, controls.MoveCenter + Vector2.right * 20f, Vector2.zero, TouchPhase.Moved),
                new MobileTouchSample(2, controls.Fire.center, Vector2.zero, TouchPhase.Moved),
                new MobileTouchSample(3, controls.Aim.center, Vector2.zero, TouchPhase.Moved),
                new MobileTouchSample(4, controls.Interact.center, Vector2.zero, TouchPhase.Moved),
                new MobileTouchSample(5, controls.Jump.center, Vector2.zero, TouchPhase.Moved),
                new MobileTouchSample(6, controls.Reload.center, Vector2.zero, TouchPhase.Moved),
                new MobileTouchSample(7, controls.Heal.center, Vector2.zero, TouchPhase.Moved),
                new MobileTouchSample(8, controls.Swap.center, Vector2.zero, TouchPhase.Moved)
            }, controls, 1f);

            Assert.That(frame.Move, Is.EqualTo(Vector2.zero));
            Assert.That(frame.Fire || frame.Aim || frame.Interact || frame.InteractHeld || frame.Jump
                || frame.Reload || frame.Heal || frame.Swap, Is.False);
        }

        [Test]
        public void TwoCapturedHeldRolesRemainSimultaneousOutsideTheirRects()
        {
            var state = new MobilePointerState();
            var controls = TestMobileControls();
            var began = state.RouteTouches(new[]
            {
                new MobileTouchSample(1, controls.Aim.center, Vector2.zero, TouchPhase.Began),
                new MobileTouchSample(2, controls.Interact.center, Vector2.zero, TouchPhase.Began)
            }, controls, 1f);
            var held = state.RouteTouches(new[]
            {
                new MobileTouchSample(1, new Vector2(490f, 300f), Vector2.zero, TouchPhase.Stationary),
                new MobileTouchSample(2, new Vector2(20f, 300f), Vector2.zero, TouchPhase.Stationary)
            }, controls, 1f);

            Assert.That(began.Aim && began.Interact && began.InteractHeld, Is.True);
            Assert.That(held.Aim && held.InteractHeld, Is.True);
            Assert.That(held.Interact, Is.False);
        }

        [Test]
        public void LocalInputTransientResetClearsPointerAndVisualState()
        {
            var inputObject = new GameObject("Local Input Reset Test");
            var input = inputObject.AddComponent<LocalInputSource>();
            var field = typeof(LocalInputSource).GetField("pointerState",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var state = (MobilePointerState)field.GetValue(input);
            state.TryBeginFire(3, Vector2.one, new Rect(0f, 0f, 10f, 10f));
            state.TryBeginLook(4, false);
            MobileControlLayout.SetVisualState(Vector2.one, true, true);

            input.ResetTransientState();

            Assert.That(state.FireFingerId, Is.EqualTo(-1));
            Assert.That(state.LookFingerId, Is.EqualTo(-1));
            Assert.That(MobileControlLayout.MoveVisual, Is.EqualTo(Vector2.zero));
            Assert.That(MobileControlLayout.FireHeld, Is.False);
            Assert.That(MobileControlLayout.AimHeld, Is.False);
            Object.DestroyImmediate(inputObject);
        }

        [TestCase(500f)]
        [TestCase(505f)]
        public void SettingsLayoutsDoNotOverlapRowsOrBottomButtonsAtSmallLandscapeHeights(float height)
        {
            var safeArea = new Rect(0f, 0f, 900f, height);
            AssertSettingsLayoutDoesNotOverlap(MatchSettingsLayout.Lobby(safeArea, false));
            AssertSettingsLayoutDoesNotOverlap(MatchSettingsLayout.Lobby(safeArea, true));

            var pausePanel = new Rect(260f, (height - 310f) * 0.5f, 380f, 310f);
            AssertSettingsLayoutDoesNotOverlap(MatchSettingsLayout.Pause(pausePanel));
        }

        [Test]
        public void MobileControlLayoutKeepsAspectRatioAcrossLandscapeSafeAreas()
        {
            var sourceSafeArea = new Rect(0f, 0f, 1280f, 720f);
            var targetSafeArea = new Rect(40f, 20f, 1840f, 860f);
            var source = new Rect(1040f, 540f, 80f, 80f);

            var normalized = HUDLayoutProfile.NormalizeMobileControl(source, sourceSafeArea);
            var restored = HUDLayoutProfile.DenormalizeMobileControl(normalized, sourceSafeArea);
            var resized = HUDLayoutProfile.DenormalizeMobileControl(normalized, targetSafeArea);

            Assert.That(restored.x, Is.EqualTo(source.x).Within(0.001f));
            Assert.That(restored.y, Is.EqualTo(source.y).Within(0.001f));
            Assert.That(restored.width, Is.EqualTo(source.width).Within(0.001f));
            Assert.That(restored.height, Is.EqualTo(source.height).Within(0.001f));
            Assert.That(resized.width, Is.EqualTo(resized.height).Within(0.001f));
            Assert.That(resized.width, Is.EqualTo(source.width * 860f / 720f).Within(0.001f));
        }

        [Test]
        public void MobileProfileResolvePreservesEditedControlAspectAcrossSafeAreas()
        {
            var sourceSafeArea = new Rect(0f, 0f, 1280f, 720f);
            var targetSafeArea = new Rect(0f, 0f, 1600f, 900f);
            var profile = new HUDLayoutProfile { mobile = true };
            profile.elements.Add(new HUDLayoutEntry { id = HUDElementId.Fire, normalizedRect = new Rect(0f, 0f, 1f, 1f) });
            profile.SetRect(HUDElementId.Fire, new Rect(1050f, 560f, 84f, 70f), sourceSafeArea);

            var source = profile.Resolve(HUDElementId.Fire, sourceSafeArea, default);
            var resized = profile.Resolve(HUDElementId.Fire, targetSafeArea, default);

            Assert.That(source.width / source.height, Is.EqualTo(84f / 70f).Within(0.001f));
            Assert.That(resized.width / resized.height, Is.EqualTo(84f / 70f).Within(0.001f));
        }

        [Test]
        public void MobileControlGuiScreenConversionRoundTrips()
        {
            var gui = new Rect(840f, 510f, 78f, 64f);
            var screen = MobileControlLayout.ToScreen(gui, 720f);
            var restored = MobileControlLayout.ToGui(screen, 720f);

            Assert.That(restored.x, Is.EqualTo(gui.x).Within(0.001f));
            Assert.That(restored.y, Is.EqualTo(gui.y).Within(0.001f));
            Assert.That(restored.width, Is.EqualTo(gui.width).Within(0.001f));
            Assert.That(restored.height, Is.EqualTo(gui.height).Within(0.001f));
        }

        [Test]
        public void MatchCursorPolicyLeavesMenusVisibleAndLocksOnlyActiveGameplay()
        {
            Assert.That(MatchCursorPolicy.LockModeFor(MatchState.Lobby, false), Is.EqualTo(CursorLockMode.None));
            Assert.That(MatchCursorPolicy.IsVisible(MatchState.Lobby, false), Is.True);
            Assert.That(MatchCursorPolicy.LockModeFor(MatchState.Complete, false), Is.EqualTo(CursorLockMode.None));
            Assert.That(MatchCursorPolicy.IsVisible(MatchState.Complete, false), Is.True);
            Assert.That(MatchCursorPolicy.LockModeFor(MatchState.Active, true), Is.EqualTo(CursorLockMode.None));
            Assert.That(MatchCursorPolicy.IsVisible(MatchState.Active, true), Is.True);
            Assert.That(MatchCursorPolicy.LockModeFor(MatchState.Active, false), Is.EqualTo(CursorLockMode.Locked));
            Assert.That(MatchCursorPolicy.IsVisible(MatchState.Active, false), Is.False);
        }

        [Test]
        public void MatchInputPolicyRoutesOnlyUnpausedGameplayStates()
        {
            Assert.That(MatchInputPolicy.ShouldReadGameplayInput(MatchState.Lobby, false), Is.False);
            Assert.That(MatchInputPolicy.ShouldReadGameplayInput(MatchState.Complete, false), Is.False);
            Assert.That(MatchInputPolicy.ShouldReadGameplayInput(MatchState.Active, true), Is.False);
            Assert.That(MatchInputPolicy.ShouldReadGameplayInput(MatchState.Staging, false), Is.True);
            Assert.That(MatchInputPolicy.ShouldReadGameplayInput(MatchState.Plane, false), Is.True);
            Assert.That(MatchInputPolicy.ShouldReadGameplayInput(MatchState.Active, false), Is.True);
        }

        [Test]
        public void ConfiguredMobileVisibilityFlowsIntoRouterGeometry()
        {
            var layoutState = SnapshotStaticFields(typeof(MobileControlLayout));
            var move = new Rect(20f, 500f, 100f, 100f);
            var fire = new Rect(700f, 500f, 70f, 70f);
            var aim = new Rect(620f, 430f, 60f, 60f);
            var jump = new Rect(720f, 400f, 60f, 60f);
            var interact = new Rect(540f, 500f, 54f, 54f);
            var reload = new Rect(620f, 510f, 50f, 50f);
            var heal = new Rect(150f, 540f, 50f, 50f);
            var swap = new Rect(210f, 540f, 50f, 50f);
            try
            {
                MobileControlLayout.Configure(move, fire, aim, jump, interact, reload, heal, swap,
                    true, false, false, true, false, true, true, true);
                var controls = MobileControlLayout.Controls(400f);
                var state = new MobilePointerState();
                var frame = state.RouteTouches(new[]
                {
                    new MobileTouchSample(1, controls.Fire.center, Vector2.zero, TouchPhase.Began),
                    new MobileTouchSample(2, controls.Aim.center, Vector2.zero, TouchPhase.Began),
                    new MobileTouchSample(3, controls.Interact.center, Vector2.zero, TouchPhase.Began)
                }, controls, 1f);

                Assert.That(controls.FireEnabled || controls.AimEnabled || controls.InteractEnabled, Is.False);
                Assert.That(frame.Fire || frame.Aim || frame.Interact || frame.InteractHeld, Is.False);
            }
            finally
            {
                RestoreStaticFields(layoutState);
            }
        }

        [Test]
        public void MovementCaptureRejectsEveryEnabledButtonInsideExpandedJoystickZone()
        {
            var controls = new MobileControlRects(new Vector2(200f, 200f), 100f,
                new Rect(240f, 190f, 20f, 20f), new Rect(190f, 240f, 20f, 20f),
                new Rect(140f, 190f, 20f, 20f), new Rect(190f, 140f, 20f, 20f),
                new Rect(240f, 240f, 20f, 20f), new Rect(140f, 140f, 20f, 20f),
                new Rect(250f, 140f, 20f, 20f), 400f);
            var actionPoints = new[]
            {
                controls.Fire.center, controls.Aim.center, controls.Jump.center,
                controls.Interact.center, controls.Reload.center, controls.Heal.center,
                controls.Swap.center
            };

            for (var i = 0; i < actionPoints.Length; i++)
            {
                var state = new MobilePointerState();
                state.RouteTouches(new[]
                {
                    new MobileTouchSample(i + 1, actionPoints[i], Vector2.zero, TouchPhase.Began)
                }, controls, 1f);

                Assert.That(state.MovementFingerId, Is.EqualTo(-1), $"Action rectangle {i} captured movement");
            }

            var joystickState = new MobilePointerState();
            var frame = joystickState.RouteTouches(new[]
            {
                new MobileTouchSample(99, new Vector2(120f, 200f), Vector2.zero, TouchPhase.Began)
            }, controls, 1f);
            Assert.That(joystickState.MovementFingerId, Is.EqualTo(99));
            Assert.That(frame.Move, Is.EqualTo(new Vector2(-0.8f, 0f)));
        }

        [Test]
        public void PauseTransitionsResetTransientInputOnPauseAndResume()
        {
            var originalTimeScale = Time.timeScale;
            var originalLockMode = Cursor.lockState;
            var originalVisible = Cursor.visible;
            var obj = new GameObject("Pause Input Reset Test");
            try
            {
                var manager = obj.AddComponent<MatchManager>();
                var input = obj.AddComponent<LocalInputSource>();
                typeof(MatchManager).GetField("input",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(manager, input);
                typeof(MatchManager).GetProperty("State").SetValue(manager, MatchState.Active);
                var state = (MobilePointerState)typeof(LocalInputSource).GetField("pointerState",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(input);

                state.TryBeginFire(1, Vector2.one, new Rect(0f, 0f, 10f, 10f));
                manager.SetPaused(true);
                Assert.That(manager.Paused, Is.True);
                Assert.That(state.FireFingerId, Is.EqualTo(-1));

                state.TryBeginLook(2, false);
                manager.SetPaused(false);
                Assert.That(manager.Paused, Is.False);
                Assert.That(state.LookFingerId, Is.EqualTo(-1));
            }
            finally
            {
                Object.DestroyImmediate(obj);
                Time.timeScale = originalTimeScale;
                Cursor.lockState = originalLockMode;
                Cursor.visible = originalVisible;
            }
        }

        [Test]
        public void DisablingPausedManagerRestoresGlobalTimeAndCursorIdempotently()
        {
            var originalTimeScale = Time.timeScale;
            var originalLockMode = Cursor.lockState;
            var originalVisible = Cursor.visible;
            var obj = new GameObject("Disabled Manager Global Cleanup Test");
            try
            {
                var manager = obj.AddComponent<MatchManager>();
                typeof(MatchManager).GetProperty("State")?.SetValue(manager, MatchState.Active);
                manager.SetPaused(true);
                Assert.That(Time.timeScale, Is.Zero);

                obj.SetActive(false);
                InvokePrivate(manager, "OnDisable");

                Assert.That(manager.Paused, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                Assert.That(Cursor.visible, Is.True);

                InvokePrivate(manager, "OnDestroy");
                Object.DestroyImmediate(obj);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                Assert.That(Cursor.visible, Is.True);
            }
            finally
            {
                if (obj != null) Object.DestroyImmediate(obj);
                Time.timeScale = originalTimeScale;
                Cursor.lockState = originalLockMode;
                Cursor.visible = originalVisible;
            }
        }

        [Test]
        public void PreferencesClampSensitivityAndNormalizeFrameRate()
        {
            const string sensitivityKey = "RavenDrop.LookSensitivity";
            const string frameRateKey = "RavenDrop.FrameRate";
            var originalSensitivity = GamePreferences.LookSensitivity;
            var originalFrameRate = GamePreferences.FrameRate;
            var hadSensitivity = PlayerPrefs.HasKey(sensitivityKey);
            var storedSensitivity = PlayerPrefs.GetFloat(sensitivityKey);
            var hadFrameRate = PlayerPrefs.HasKey(frameRateKey);
            var storedFrameRate = PlayerPrefs.GetInt(frameRateKey);
            var originalVSync = QualitySettings.vSyncCount;
            var originalTargetFrameRate = Application.targetFrameRate;
            try
            {
                GamePreferences.SetLookSensitivity(20f);
                GamePreferences.SetFrameRate(47);
                Assert.That(GamePreferences.LookSensitivity, Is.EqualTo(1.65f));
                Assert.That(GamePreferences.FrameRate, Is.EqualTo(60));
            }
            finally
            {
                if (hadSensitivity) PlayerPrefs.SetFloat(sensitivityKey, storedSensitivity);
                else PlayerPrefs.DeleteKey(sensitivityKey);
                if (hadFrameRate) PlayerPrefs.SetInt(frameRateKey, storedFrameRate);
                else PlayerPrefs.DeleteKey(frameRateKey);
                PlayerPrefs.Save();
                SetGamePreference(nameof(GamePreferences.LookSensitivity), originalSensitivity);
                SetGamePreference(nameof(GamePreferences.FrameRate), originalFrameRate);
                QualitySettings.vSyncCount = originalVSync;
                Application.targetFrameRate = originalTargetFrameRate;
            }
        }

        [Test]
        public void BermudaMapKeepsFullImportedGeometryAndMaterials()
        {
            var map = Resources.Load<GameObject>(WorldMapGeometry.BermudaResourcePath);
            Assert.That(map, Is.Not.Null);
            var renderers = map.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(7));
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Assert.That(Mathf.Max(bounds.size.x, bounds.size.z), Is.InRange(130f, 160f));
            Assert.That(WorldMapGeometry.RequiredUniformScale(bounds), Is.InRange(12f, 16f));
            Assert.That(bounds.size.y, Is.GreaterThan(7f));
            var textured = 0;
            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
                if (material != null && material.mainTexture != null) textured++;
            Assert.That(textured, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void RuntimeTextureSamplingKeepsMipmapsSharp()
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Point,
                anisoLevel = 1
            };
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", texture);

            BRAssetVisuals.TuneTextureSampling(material);

            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(texture.anisoLevel, Is.GreaterThanOrEqualTo(4));
            Assert.That(texture.mipMapBias, Is.LessThan(0f));
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void CameraFieldOfViewCommunicatesAimAndSprint()
        {
            Assert.That(ThirdPersonCamera.TargetFieldOfView(false, false), Is.EqualTo(64f));
            Assert.That(ThirdPersonCamera.TargetFieldOfView(false, true), Is.EqualTo(66f));
            Assert.That(ThirdPersonCamera.TargetFieldOfView(true, false), Is.EqualTo(54f));
            Assert.That(ThirdPersonCamera.TargetFieldOfView(true, true), Is.EqualTo(54f));
        }

        [TestCase(false, false, 0.34f, 0.18f, -1.42f)]
        [TestCase(true, false, 0.34f, 0.16f, -1.30f)]
        [TestCase(false, true, 0.25f, 0.18f, -1.34f)]
        [TestCase(true, true, 0.30f, 0.14f, -1.04f)]
        public void CameraModeSelectsExpectedOffset(bool aiming, bool indoors,
            float expectedX, float expectedY, float expectedZ)
        {
            Assert.That(ThirdPersonCamera.OffsetFor(aiming, indoors),
                Is.EqualTo(new Vector3(expectedX, expectedY, expectedZ)));
        }

        [Test]
        public void DeploymentForwardInputFollowsCameraYaw()
        {
            var cameraForward = Quaternion.Euler(0f, 90f, 0f) * Vector3.forward;
            var cameraRight = Quaternion.Euler(0f, 90f, 0f) * Vector3.right;

            var travel = DeploymentSteering.CameraRelativeTravel(Vector2.up,
                cameraForward, cameraRight, Vector3.forward);

            Assert.That(Vector3.Dot(travel.normalized, Vector3.right), Is.GreaterThan(0.999f));
            Assert.That(travel.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void DeploymentLateralInputUsesCameraRight()
        {
            var travel = DeploymentSteering.CameraRelativeTravel(Vector2.right,
                Vector3.forward, Vector3.right, Vector3.forward);

            Assert.That(Vector3.Dot(travel.normalized, Vector3.right), Is.GreaterThan(0.999f));
        }

        [Test]
        public void DeploymentIdleMomentumIsReducedAndPlanar()
        {
            var travel = DeploymentSteering.IdleTravel(new Vector3(3f, 8f, 4f));

            Assert.That(travel.magnitude, Is.EqualTo(DeploymentSteering.IdleMomentum).Within(0.0001f));
            Assert.That(travel.y, Is.EqualTo(0f));
            Assert.That(travel.magnitude, Is.LessThan(0.25f));
        }

        [Test]
        public void DeploymentDegenerateCameraBasisUsesFiniteFallback()
        {
            var travel = DeploymentSteering.CameraRelativeTravel(Vector2.up,
                Vector3.up, Vector3.up, Vector3.right);

            Assert.That(float.IsFinite(travel.x) && float.IsFinite(travel.y) && float.IsFinite(travel.z), Is.True);
            Assert.That(Vector3.Dot(travel.normalized, Vector3.right), Is.GreaterThan(0.999f));
        }

        [Test]
        public void DeploymentCameraOffsetsStayCenteredBehindAndAbovePlayer()
        {
            var freefall = ThirdPersonCamera.DeploymentOffset(ParticipantPhase.Freefall);
            var parachute = ThirdPersonCamera.DeploymentOffset(ParticipantPhase.Parachute);

            Assert.That(freefall, Is.EqualTo(new Vector3(0f, 2.8f, -7f)));
            Assert.That(parachute, Is.EqualTo(new Vector3(0f, 2.7f, -6.5f)));
            Assert.That(parachute.magnitude, Is.InRange(6.5f, 7.2f));
        }

        [Test]
        public void DeploymentCameraYawRecentersWithoutSnapping()
        {
            var next = ThirdPersonCamera.DampedDeploymentYaw(0f, 90f, 5.5f, 1f / 60f);

            Assert.That(next, Is.GreaterThan(0f).And.LessThan(90f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(next, 90f)), Is.LessThan(90f));
        }

        [Test]
        public void DeploymentFacingTurnsTowardTravelWithinRateLimit()
        {
            var next = DeploymentSteering.SmoothFacing(Quaternion.identity, Vector3.right,
                90f, 0.25f);

            Assert.That(Quaternion.Angle(Quaternion.identity, next), Is.EqualTo(22.5f).Within(0.001f));
            Assert.That(Vector3.Dot(next * Vector3.forward, Vector3.right), Is.GreaterThan(0f));
        }

        [TestCase(3.35f, 0.08f, 0.12f, 0f)]
        [TestCase(3.35f, 0.5f, 0.12f, 0.38f)]
        [TestCase(3.35f, 4f, 0.12f, 3.35f)]
        public void CameraCollisionClearanceNeverCrossesHit(float desiredDistance,
            float nearestHit, float skin, float expectedDistance)
        {
            Assert.That(ThirdPersonCamera.CorrectedCollisionDistance(desiredDistance, nearestHit, skin),
                Is.EqualTo(expectedDistance).Within(0.0001f));
        }

        [TestCase(true, false, 0f, false, 3.35f, false, true)]
        [TestCase(false, true, 3.35f, false, 1.1f, true, true)]
        [TestCase(false, true, 1.1f, true, 0.7f, true, true)]
        [TestCase(false, true, 0.7f, true, 3.35f, false, false)]
        [TestCase(false, true, 3.35f, false, 3.35f, false, false)]
        public void CameraSnapsOnlyForFirstFrameOrObstructedClearanceContraction(bool firstFrame,
            bool hasPrevious, float previousClearance, bool previousObstructed,
            float currentClearance, bool currentObstructed, bool expectedSnap)
        {
            Assert.That(ThirdPersonCamera.ShouldSnapForClearance(firstFrame, hasPrevious,
                previousClearance, previousObstructed, currentClearance, currentObstructed), Is.EqualTo(expectedSnap));
        }

        [Test]
        public void CameraUnobstructedTargetMotionUsesSymmetricFollowSmoothing()
        {
            var directions = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            var referenceProgress = -1f;
            foreach (var direction in directions)
            {
                var velocity = Vector3.zero;
                var next = ThirdPersonCamera.SmoothedFollowPosition(Vector3.zero, direction * 2f,
                    ref velocity, 0.055f, 1f / 60f);
                var progress = Vector3.Dot(next, direction);

                Assert.That(progress, Is.GreaterThan(0f).And.LessThan(2f));
                if (referenceProgress < 0f) referenceProgress = progress;
                else Assert.That(progress, Is.EqualTo(referenceProgress).Within(0.0001f));
            }
        }

        [Test]
        public void CameraSafeLookRotationPreservesFallbackForZeroDirection()
        {
            var fallback = Quaternion.Euler(13f, 47f, 2f);

            Assert.That(Quaternion.Angle(ThirdPersonCamera.SafeLookRotation(Vector3.zero, fallback), fallback),
                Is.LessThan(0.001f));
        }

        [Test]
        public void CameraAdsRotationUsesBoundedAnglesAndRecoil()
        {
            var rotation = ThirdPersonCamera.FinalViewRotation(true, -7f, 24f, 2f, -1.5f,
                Vector3.back, Quaternion.identity);
            var expected = Quaternion.Euler(-9f, 22.5f, 0f);

            Assert.That(Quaternion.Angle(rotation, expected), Is.LessThan(0.001f));
        }

        [Test]
        public void CameraAimAssistStepIsBoundedAndWeakenedByManualLook()
        {
            var targetDirection = Quaternion.Euler(-20f, 60f, 0f) * Vector3.forward;
            var assisted = ThirdPersonCamera.BoundedAimAngles(0f, 0f, targetDirection, 0f, 0.01f, -35f, 62f);
            var manual = ThirdPersonCamera.BoundedAimAngles(0f, 0f, targetDirection, 5f, 0.01f, -35f, 62f);
            var maxYawStep = 118f * AimAssistTargeting.MagnetismStrength(0f) * 0.01f;

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, assisted.x)), Is.LessThanOrEqualTo(maxYawStep + 0.0001f));
            Assert.That(Mathf.Abs(assisted.y), Is.LessThanOrEqualTo(maxYawStep * 0.72f + 0.0001f));
            Assert.That(Mathf.Abs(manual.x), Is.LessThan(Mathf.Abs(assisted.x)));
            Assert.That(Mathf.Abs(manual.y), Is.LessThan(Mathf.Abs(assisted.y)));
        }

        [Test]
        public void CameraShoulderSwapMirrorsOnlyHorizontalOffset()
        {
            var right = ThirdPersonCamera.ShoulderOffsetFor(false, false, false);
            var left = ThirdPersonCamera.ShoulderOffsetFor(false, false, true);

            Assert.That(left.x, Is.EqualTo(-right.x));
            Assert.That(left.y, Is.EqualTo(right.y));
            Assert.That(left.z, Is.EqualTo(right.z));
        }

        [Test]
        public void CameraTargetAssignmentResetsViewState()
        {
            var cameraObject = new GameObject("Reset Camera Test");
            var firstTarget = new GameObject("First Camera Target");
            var secondTarget = new GameObject("Second Camera Target");
            var camera = cameraObject.AddComponent<ThirdPersonCamera>();
            firstTarget.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            secondTarget.transform.rotation = Quaternion.Euler(0f, 123f, 0f);

            camera.SetTarget(firstTarget.transform);
            camera.Tick(new BRInputFrame(Vector2.zero, Vector2.zero, false, false, false, true,
                false, false, false, false, false, false, -1, false, false));
            camera.AddRecoil(2f, 1f);
            Assert.That(camera.Aiming, Is.True);
            Assert.That(camera.RecoilSettled, Is.False);

            camera.SetTarget(secondTarget.transform);

            Assert.That(camera.CurrentYaw, Is.EqualTo(123f).Within(0.001f));
            Assert.That(camera.RecoilSettled, Is.True);
            Assert.That(camera.Aiming, Is.False);
            Assert.That(camera.Indoors, Is.False);
            Assert.That(camera.AimAssistTarget, Is.Null);

            Object.DestroyImmediate(secondTarget);
            Object.DestroyImmediate(firstTarget);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void CameraFirstTargetFrameSnapsDirectlyToDesiredPosition()
        {
            var cameraObject = new GameObject("First Frame Camera Test");
            var targetObject = new GameObject("First Frame Target");
            var camera = cameraObject.AddComponent<ThirdPersonCamera>();
            cameraObject.transform.position = new Vector3(-80f, 40f, 90f);
            targetObject.transform.SetPositionAndRotation(new Vector3(12f, 3f, -8f), Quaternion.Euler(0f, 31f, 0f));
            camera.SetTarget(targetObject.transform);

            camera.Tick(default);

            var pivot = targetObject.transform.position + Vector3.up * CharacterPresentationProfile.CameraPivotHeight;
            var expected = pivot + Quaternion.Euler(-4f, 31f, 0f) * ThirdPersonCamera.OffsetFor(false, false);
            Assert.That(Vector3.Distance(cameraObject.transform.position, expected), Is.LessThan(0.001f));

            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void CurrentFrameLookUpdatesCameraIntentBeforeFire()
        {
            var shooter = new GameObject("Current Frame Fire Shooter");
            shooter.transform.position = new Vector3(650f, 0f, 0f);
            CharacterPresentationProfile.Apply(shooter.AddComponent<CharacterController>());
            var motor = shooter.AddComponent<BRCharacterMotor>();
            shooter.AddComponent<InventorySystem>();
            var weaponSystem = shooter.AddComponent<WeaponSystem>();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.magazineSize = 2;
            weapon.range = 30f;
            weapon.standingSpreadDegrees = 0f;
            weapon.movingSpreadDegrees = 0f;
            weaponSystem.EquipInitial(weapon);
            var cameraObject = new GameObject("Current Frame Fire Camera");
            var viewCamera = cameraObject.AddComponent<Camera>();
            var camera = cameraObject.AddComponent<ThirdPersonCamera>();
            camera.SetTarget(shooter.transform);
            camera.Tick(default);
            var frame = new BRInputFrame(Vector2.zero, new Vector2(10f, 0f), false, false, false, false,
                true, true, false, false, false, false, -1, false, false);

            camera.PreCombatTick(frame);
            motor.Move(frame, cameraObject.transform, new BRGameConfig(), false);
            Assert.That(weaponSystem.TryFire(viewCamera, false, shooter), Is.True);

            var shotTravel = weaponSystem.LastShotEndPoint - shooter.transform.position;
            Assert.That(camera.CurrentYaw, Is.GreaterThan(1f));
            Assert.That(shotTravel.x, Is.GreaterThan(3f));
            camera.PostMovementTick(frame);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(shooter);
        }

        [Test]
        public void CameraCloseWallClearanceWinsOverFramingMinimum()
        {
            var cameraObject = new GameObject("Close Wall Camera Test");
            var targetObject = new GameObject("Close Wall Target");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var camera = cameraObject.AddComponent<ThirdPersonCamera>();
            var pivot = Vector3.up * CharacterPresentationProfile.CameraPivotHeight;
            wall.name = "Close Camera Wall";
            wall.transform.position = pivot + Vector3.back * 0.3f;
            wall.transform.localScale = new Vector3(3f, 3f, 0.08f);
            camera.SetTarget(targetObject.transform);
            Physics.SyncTransforms();

            camera.Tick(default);

            Assert.That(Vector3.Distance(cameraObject.transform.position, pivot), Is.LessThan(0.1f));
            Assert.That(cameraObject.transform.position.z, Is.GreaterThan(wall.transform.position.z));

            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void CameraWallCollisionKeepsBoundedSameSidePositionAndClearPath()
        {
            var cameraObject = new GameObject("Wall Camera Test");
            var targetObject = new GameObject("Wall Camera Target");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var camera = cameraObject.AddComponent<ThirdPersonCamera>();
            var pivot = Vector3.up * CharacterPresentationProfile.CameraPivotHeight;
            wall.name = "Camera Recovery Wall";
            wall.transform.position = pivot + Vector3.back;
            wall.transform.localScale = new Vector3(4f, 3f, 0.15f);
            camera.SetTarget(targetObject.transform);
            Physics.SyncTransforms();

            camera.Tick(default);

            var offset = cameraObject.transform.position - pivot;
            Assert.That(offset.z, Is.LessThan(0f));
            Assert.That(offset.magnitude, Is.LessThan(1f));
            Assert.That(Physics.CheckSphere(cameraObject.transform.position, 0.22f, ~0,
                QueryTriggerInteraction.Ignore), Is.False);
            AssertCameraPathClear(pivot, cameraObject.transform.position, 0.22f);

            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void CameraTargetEnteringWallReusesValidatedSameSideAnchor()
        {
            var cameraObject = new GameObject("Stable Anchor Camera Test");
            var targetObject = new GameObject("Stable Anchor Target");
            var camera = cameraObject.AddComponent<ThirdPersonCamera>();
            camera.SetTarget(targetObject.transform);
            Physics.SyncTransforms();
            camera.Tick(default);
            var stable = cameraObject.transform.position;
            var stablePivot = targetObject.transform.position
                + Vector3.up * CharacterPresentationProfile.CameraPivotHeight;

            targetObject.transform.position = Vector3.right * 0.80f;
            var overlappedPivot = targetObject.transform.position
                + Vector3.up * CharacterPresentationProfile.CameraPivotHeight;
            var wall = CreateCameraWall("Target Entry Wall", overlappedPivot, new Vector3(0.18f, 3f, 2f));
            Physics.SyncTransforms();

            camera.Tick(default);

            Assert.That(Vector3.Distance(cameraObject.transform.position, stable), Is.LessThan(0.001f));
            Assert.That(Vector3.Dot(cameraObject.transform.position - overlappedPivot,
                stable - stablePivot), Is.GreaterThan(0f));
            Assert.That(Physics.CheckSphere(cameraObject.transform.position, 0.22f, ~0,
                QueryTriggerInteraction.Ignore), Is.False);
            AssertCameraPathClear(stablePivot, cameraObject.transform.position, 0.22f);

            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void CameraOriginInThinWallUsesBoundedLocalSameSideRecovery()
        {
            var cameraObject = new GameObject("Local Recovery Camera Test");
            var targetObject = new GameObject("Local Recovery Target");
            var camera = cameraObject.AddComponent<ThirdPersonCamera>();
            var pivot = Vector3.up * CharacterPresentationProfile.CameraPivotHeight;
            var wall = CreateCameraWall("Local Recovery Wall", pivot, new Vector3(3f, 3f, 0.16f));
            camera.SetTarget(targetObject.transform);
            Physics.SyncTransforms();

            camera.Tick(default);

            var offset = cameraObject.transform.position - pivot;
            Assert.That(offset.z, Is.LessThan(0f));
            Assert.That(offset.magnitude, Is.LessThanOrEqualTo(1.25f));
            Assert.That(Physics.CheckSphere(cameraObject.transform.position, 0.22f, ~0,
                QueryTriggerInteraction.Ignore), Is.False);

            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void CameraCeilingCollisionRemainsBelowCeilingWithoutOverlap()
        {
            var cameraObject = new GameObject("Ceiling Camera Test");
            var targetObject = new GameObject("Ceiling Camera Target");
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var camera = cameraObject.AddComponent<ThirdPersonCamera>();
            var pivot = Vector3.up * CharacterPresentationProfile.CameraPivotHeight;
            ceiling.name = "Low Camera Ceiling";
            ceiling.transform.position = pivot + new Vector3(0f, 0.55f, -1.4f);
            ceiling.transform.localScale = new Vector3(5f, 0.16f, 5f);
            camera.SetTarget(targetObject.transform);
            Physics.SyncTransforms();

            camera.Tick(default);

            Assert.That(cameraObject.transform.position.y, Is.LessThan(ceiling.GetComponent<Collider>().bounds.min.y));
            Assert.That(cameraObject.transform.position.z, Is.LessThan(pivot.z));
            Assert.That(Physics.CheckSphere(cameraObject.transform.position, 0.22f, ~0,
                QueryTriggerInteraction.Ignore), Is.False);
            AssertCameraPathClear(pivot, cameraObject.transform.position, 0.22f);

            Object.DestroyImmediate(ceiling);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void CameraInsideRoomCannotCrossEnclosingWall()
        {
            var cameraObject = new GameObject("Room Camera Test");
            var targetObject = new GameObject("Room Camera Target");
            var camera = cameraObject.AddComponent<ThirdPersonCamera>();
            var pivot = Vector3.up * CharacterPresentationProfile.CameraPivotHeight;
            var walls = new[]
            {
                CreateCameraWall("Room Back", new Vector3(0f, pivot.y, -2f), new Vector3(5f, 3f, 0.15f)),
                CreateCameraWall("Room Front", new Vector3(0f, pivot.y, 2f), new Vector3(5f, 3f, 0.15f)),
                CreateCameraWall("Room Left", new Vector3(-2.5f, pivot.y, 0f), new Vector3(0.15f, 3f, 4f)),
                CreateCameraWall("Room Right", new Vector3(2.5f, pivot.y, 0f), new Vector3(0.15f, 3f, 4f)),
                CreateCameraWall("Room Ceiling", new Vector3(0f, pivot.y + 1.25f, 0f), new Vector3(5f, 0.15f, 4f))
            };
            camera.SetTarget(targetObject.transform);
            Physics.SyncTransforms();

            camera.Tick(default);

            Assert.That(cameraObject.transform.position.z, Is.InRange(-1.7f, 0f));
            Assert.That(Physics.CheckSphere(cameraObject.transform.position, 0.22f, ~0,
                QueryTriggerInteraction.Ignore), Is.False);
            AssertCameraPathClear(pivot, cameraObject.transform.position, 0.22f);

            foreach (var wall in walls) Object.DestroyImmediate(wall);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void StaticCameraZeroLookVectorKeepsExistingRotation()
        {
            var cameraObject = new GameObject("Static Zero Look Camera Test");
            var camera = cameraObject.AddComponent<ThirdPersonCamera>();
            var rotation = Quaternion.Euler(11f, 73f, 0f);
            var position = new Vector3(4f, 5f, 6f);
            cameraObject.transform.SetPositionAndRotation(position, rotation);

            camera.SetStaticView(position, position);

            Assert.That(Quaternion.Angle(cameraObject.transform.rotation, rotation), Is.LessThan(0.001f));
            Assert.That(cameraObject.transform.position, Is.EqualTo(position));
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void CharacterPresentationFitsImportedDoorScale()
        {
            Assert.That(CharacterPresentationProfile.VisualHeight, Is.EqualTo(0.84f));
            Assert.That(CharacterPresentationProfile.AnimatedNormalizationHeight, Is.EqualTo(0.87f));
            Assert.That(CharacterPresentationProfile.AnimatedNormalizationHeight,
                Is.GreaterThan(CharacterPresentationProfile.VisualHeight));
            Assert.That(CharacterPresentationProfile.ControllerHeight, Is.GreaterThanOrEqualTo(CharacterPresentationProfile.VisualHeight));
            Assert.That(CharacterPresentationProfile.ControllerRadius * 2f, Is.LessThan(CharacterPresentationProfile.ControllerHeight * 0.5f));
            Assert.That(CharacterPresentationProfile.HeadHeight, Is.InRange(0.73f, 0.77f));
            Assert.That(CharacterPresentationProfile.OpeningOccupancy(1.352f), Is.InRange(0.60f, 0.64f));
            Assert.That(CharacterPresentationProfile.CameraPivot(false), Is.EqualTo(0.78f));
            Assert.That(CharacterPresentationProfile.CameraPivot(true), Is.EqualTo(0.92f));
        }

        [Test]
        public void CharacterCameraOffsetsStayBehindHeadPivot()
        {
            Assert.That(CharacterPresentationProfile.CameraOffset(false, false), Is.EqualTo(new Vector3(0.34f, 0.18f, -1.42f)));
            Assert.That(CharacterPresentationProfile.CameraOffset(true, false), Is.EqualTo(new Vector3(0.34f, 0.16f, -1.30f)));
            Assert.That(CharacterPresentationProfile.CameraOffset(false, true), Is.EqualTo(new Vector3(0.25f, 0.18f, -1.34f)));
            Assert.That(CharacterPresentationProfile.CameraOffset(true, true), Is.EqualTo(new Vector3(0.30f, 0.14f, -1.04f)));
        }

        [Test]
        public void CharacterPresentationAppliesControllerDimensions()
        {
            var obj = new GameObject("Character Presentation Controller Test");
            var controller = obj.AddComponent<CharacterController>();

            CharacterPresentationProfile.Apply(controller);

            Assert.That(controller.radius, Is.EqualTo(CharacterPresentationProfile.ControllerRadius));
            Assert.That(controller.height, Is.EqualTo(CharacterPresentationProfile.ControllerHeight));
            Assert.That(controller.center, Is.EqualTo(Vector3.up * CharacterPresentationProfile.ControllerCenterY));
            Assert.That(controller.stepOffset, Is.EqualTo(CharacterPresentationProfile.StepOffset));
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void CharacterPresentationDerivesAimAndMuzzleAnchors()
        {
            var root = new GameObject("Character Presentation Anchor Test");
            root.transform.position = new Vector3(2f, 3f, 4f);
            root.transform.rotation = Quaternion.LookRotation(Vector3.right);

            var aimPoint = CharacterPresentationProfile.AimPoint(root.transform);
            var muzzle = CharacterPresentationProfile.FallbackMuzzle(root.transform);

            Assert.That(aimPoint, Is.EqualTo(new Vector3(2f, 3.75f, 4f)));
            Assert.That(muzzle.x, Is.EqualTo(2.30f).Within(0.0001f));
            Assert.That(muzzle.y, Is.EqualTo(3.68f).Within(0.0001f));
            Assert.That(muzzle.z, Is.EqualTo(4f).Within(0.0001f));
            Object.DestroyImmediate(root);
        }

        [Test]
        public void TargetingAndWeaponPresentationFollowCharacterProfile()
        {
            var participant = CreateParticipant("Character Presentation Behavior Test", false);
            participant.transform.SetPositionAndRotation(
                new Vector3(-3.25f, 2.5f, 7.75f),
                Quaternion.Euler(0f, 137f, 0f));

            var expectedAimPoint = participant.transform.position
                + Vector3.up * (CharacterPresentationProfile.VisualHeight * 0.68f);
            var expectedMuzzle = participant.transform.position
                + Vector3.up * CharacterPresentationProfile.MuzzleHeight
                + participant.transform.forward * CharacterPresentationProfile.MuzzleForward;

            Assert.That(Vector3.Distance(AimAssistTargeting.AimPoint(participant), expectedAimPoint),
                Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(CharacterPresentationProfile.FallbackMuzzle(participant.transform), expectedMuzzle),
                Is.LessThan(0.0001f));
            Assert.That(CharacterPresentationProfile.WeaponLength(WeaponClass.Pistol),
                Is.LessThan(CharacterPresentationProfile.WeaponLength(WeaponClass.Smg)));
            Object.DestroyImmediate(participant.gameObject);
        }

        [Test]
        public void RootMountedWeaponVisualUsesSingleProfileMuzzleOffset()
        {
            var participant = CreateParticipant("Root Mounted Weapon Visual Test", false);
            participant.transform.SetPositionAndRotation(new Vector3(4f, 1.5f, -2f), Quaternion.Euler(0f, 63f, 0f));
            var visual = participant.gameObject.AddComponent<ParticipantWeaponVisual>();
            visual.Configure(null);
            var mountedModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mountedModel.transform.SetParent(participant.transform, false);
            mountedModel.transform.localPosition = new Vector3(0.26f,
                CharacterPresentationProfile.MuzzleHeight, CharacterPresentationProfile.MuzzleForward);
            var modelField = typeof(ParticipantWeaponVisual).GetField("model",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(modelField, Is.Not.Null);
            modelField.SetValue(visual, mountedModel);
            Assert.That(Vector3.Distance(visual.MuzzlePosition,
                CharacterPresentationProfile.FallbackMuzzle(participant.transform)), Is.LessThan(0.0001f));
            Object.DestroyImmediate(participant.gameObject);
        }

        [Test]
        public void BotSightOriginPrefersWeaponMuzzleAndFallsBackToProfile()
        {
            var root = new GameObject("Bot Sight Origin Test");
            root.transform.SetPositionAndRotation(new Vector3(-2f, 0.75f, 6f), Quaternion.Euler(0f, 121f, 0f));
            var weaponMuzzle = new Vector3(8f, 9f, 10f);

            Assert.That(Vector3.Distance(BotController.SightOrigin(root.transform, null),
                CharacterPresentationProfile.FallbackMuzzle(root.transform)), Is.LessThan(0.0001f));
            Assert.That(BotController.SightOrigin(root.transform, weaponMuzzle), Is.EqualTo(weaponMuzzle));
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CharacterPresentationHandlesNullInputs()
        {
            Assert.DoesNotThrow(() => CharacterPresentationProfile.Apply(null));
            Assert.That(CharacterPresentationProfile.AimPoint(null), Is.EqualTo(Vector3.zero));
            Assert.That(CharacterPresentationProfile.FallbackMuzzle(null), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void CharacterPresentationUsesClassSpecificWeaponLengths()
        {
            Assert.That(CharacterPresentationProfile.WeaponLength(WeaponClass.Pistol), Is.EqualTo(0.23f));

            foreach (WeaponClass weaponClass in System.Enum.GetValues(typeof(WeaponClass)))
            {
                if (weaponClass == WeaponClass.Pistol) continue;
                Assert.That(CharacterPresentationProfile.WeaponLength(weaponClass), Is.EqualTo(0.42f));
            }
        }

        [Test]
        public void CameraCanEnterAndLeaveDedicatedStagingView()
        {
            var cameraObject = new GameObject("Staging Camera Test");
            var camera = cameraObject.AddComponent<ThirdPersonCamera>();

            camera.SetStagingView(true);
            Assert.That(camera.StagingViewEnabled, Is.True);
            camera.SetStagingView(false);
            Assert.That(camera.StagingViewEnabled, Is.False);

            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void AnimatedVisualCorrectionPlacesFeetAtMotorRoot()
        {
            Assert.That(CharacterVisualAnimator.GroundCorrection(2f, 3.4f), Is.EqualTo(-1.385f).Within(0.001f));
            Assert.That(CharacterVisualAnimator.GroundCorrection(2f, 1.4f), Is.EqualTo(0.615f).Within(0.001f));
        }

        [Test]
        public void CharacterVisualHeightIsNormalizedToControllerScale()
        {
            Assert.That(BRAssetVisuals.HeightScaleFactor(3.44f, 1.72f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(BRAssetVisuals.HeightScaleFactor(0f, 1.72f), Is.EqualTo(1f));

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Character Height Normalization Test";
            visual.transform.localScale = new Vector3(0.7f, 2.4f, 0.5f);

            BRAssetVisuals.NormalizeHeight(visual, CharacterPresentationProfile.VisualHeight);

            Assert.That(visual.GetComponent<Renderer>().bounds.size.y,
                Is.EqualTo(CharacterPresentationProfile.VisualHeight).Within(0.001f));
            Object.DestroyImmediate(visual);
        }

        [Test]
        public void CharacterVisualNormalizationIgnoresDisabledEmbeddedMeshes()
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visible Character Mesh";
            visual.transform.localScale = new Vector3(0.7f, 2f, 0.5f);
            var embedded = GameObject.CreatePrimitive(PrimitiveType.Cube);
            embedded.name = "Disabled Embedded Weapon";
            embedded.transform.SetParent(visual.transform, false);
            embedded.transform.localPosition = Vector3.up * 8f;
            embedded.GetComponent<Renderer>().enabled = false;

            BRAssetVisuals.NormalizeHeight(visual, CharacterPresentationProfile.VisualHeight);

            Assert.That(visual.GetComponent<Renderer>().bounds.size.y,
                Is.EqualTo(CharacterPresentationProfile.VisualHeight).Within(0.001f));
            Object.DestroyImmediate(visual);
        }

        [Test]
        public void AimAssistPrefersCenterAndYieldsToManualLook()
        {
            var centered = AimAssistTargeting.CandidateScore(1f, 30f, 11f, 65f, false);
            var edge = AimAssistTargeting.CandidateScore(9f, 30f, 11f, 65f, false);
            var retained = AimAssistTargeting.CandidateScore(9f, 30f, 11f, 65f, true);

            Assert.That(centered, Is.LessThan(edge));
            Assert.That(retained, Is.LessThan(edge));
            Assert.That(AimAssistTargeting.MagnetismStrength(0f), Is.GreaterThan(AimAssistTargeting.MagnetismStrength(5f)));
        }

        [Test]
        public void AimAssistChoosesEnemyOverBetterCenteredTeammateWithoutChangingRegistry()
        {
            var registryBeforeFixture = ParticipantRegistry.Count;
            BRParticipant owner = null;
            BRParticipant teammate = null;
            BRParticipant enemy = null;
            try
            {
                owner = CreateParticipant("Team Aim Assist Owner", true, 40);
                teammate = CreateParticipant("Centered Aim Assist Teammate", false, 40);
                enemy = CreateParticipant("Eligible Aim Assist Enemy", false, 41);
                ParticipantRegistry.Register(owner);
                ParticipantRegistry.Register(teammate);
                ParticipantRegistry.Register(enemy);
                CharacterPresentationProfile.Apply(owner.GetComponent<CharacterController>());
                CharacterPresentationProfile.Apply(teammate.GetComponent<CharacterController>());
                CharacterPresentationProfile.Apply(enemy.GetComponent<CharacterController>());
                owner.transform.position = new Vector3(1000f, 0f, 0f);
                teammate.transform.position = new Vector3(1000f, 0f, 8f);
                enemy.transform.position = new Vector3(1001f, 0f, 8f);
                owner.GetComponent<CharacterController>().enabled = false;
                teammate.GetComponent<CharacterController>().enabled = false;
                enemy.GetComponent<CharacterController>().enabled = false;
                var enemyHitbox = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                enemyHitbox.name = "Eligible Aim Assist Enemy Hitbox";
                enemyHitbox.transform.SetParent(enemy.transform, false);
                enemyHitbox.transform.localPosition = Vector3.up * CharacterPresentationProfile.HeadHeight;
                enemyHitbox.transform.localScale = Vector3.one * 0.5f;
                teammate.SetPhase(ParticipantPhase.Grounded);
                enemy.SetPhase(ParticipantPhase.Grounded);
                Physics.SyncTransforms();
                var registryBeforeSelection = ParticipantRegistry.Count;
                Assert.That(registryBeforeSelection, Is.EqualTo(registryBeforeFixture + 3));
                var origin = owner.transform.position + Vector3.up * CharacterPresentationProfile.HeadHeight;
                var teammateDirection = AimAssistTargeting.AimPoint(teammate) - origin;
                var enemyDirection = AimAssistTargeting.AimPoint(enemy) - origin;

                Assert.That(Vector3.Angle(Vector3.forward, teammateDirection),
                    Is.LessThan(Vector3.Angle(Vector3.forward, enemyDirection)));
                Assert.That(Physics.Raycast(origin, enemyDirection.normalized, out var enemySightHit,
                    enemyDirection.magnitude + 0.15f), Is.True);
                Assert.That(enemySightHit.collider.GetComponentInParent<BRParticipant>(), Is.SameAs(enemy));

                var selected = AimAssistTargeting.FindBest(owner.gameObject, origin, Vector3.forward);

                Assert.That(selected, Is.SameAs(enemy));
                Assert.That(ParticipantRegistry.Count, Is.EqualTo(registryBeforeSelection));
            }
            finally
            {
                DestroyParticipants(owner, teammate, enemy);
            }
            Assert.That(ParticipantRegistry.Count, Is.EqualTo(registryBeforeFixture));
        }

        [Test]
        public void AimAssistCannotTargetThroughCover()
        {
            var owner = new GameObject("Aim Assist LOS Owner");
            owner.transform.position = new Vector3(100f, 0f, 0f);
            var target = CreateParticipant("Aim Assist LOS Target", false);
            ParticipantRegistry.Register(target);
            CharacterPresentationProfile.Apply(target.GetComponent<CharacterController>());
            target.transform.position = new Vector3(100f, 0f, 8f);
            target.SetPhase(ParticipantPhase.Grounded);
            var origin = owner.transform.position + Vector3.up * CharacterPresentationProfile.HeadHeight;
            Physics.SyncTransforms();
            Assert.That(AimAssistTargeting.FindBest(owner, origin, Vector3.forward), Is.SameAs(target));

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Aim Assist LOS Wall";
            wall.transform.position = new Vector3(100f, CharacterPresentationProfile.HeadHeight, 4f);
            wall.transform.localScale = new Vector3(2f, 2f, 0.2f);
            Physics.SyncTransforms();

            Assert.That(AimAssistTargeting.FindBest(owner, origin, Vector3.forward), Is.Null);

            Object.DestroyImmediate(wall);
            ParticipantRegistry.Unregister(target);
            Object.DestroyImmediate(target.gameObject);
            Object.DestroyImmediate(owner);
        }

        [Test]
        public void DenseIgnoredHitsDoNotHideAimAssistCover()
        {
            var owner = new GameObject("Dense Aim Assist Owner");
            owner.transform.position = new Vector3(700f, 0f, 0f);
            for (var i = 0; i < 32; i++)
            {
                var ignored = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ignored.name = $"Dense Ignored Sight {i}";
                ignored.transform.SetParent(owner.transform, false);
                ignored.transform.localPosition = new Vector3(0f, CharacterPresentationProfile.HeadHeight,
                    0.15f + i * 0.025f);
                ignored.transform.localScale = new Vector3(0.04f, 0.12f, 0.01f);
            }
            var target = CreateParticipant("Dense Aim Assist Target", false);
            ParticipantRegistry.Register(target);
            CharacterPresentationProfile.Apply(target.GetComponent<CharacterController>());
            target.transform.position = new Vector3(700f, 0f, 8f);
            target.SetPhase(ParticipantPhase.Grounded);
            var origin = owner.transform.position + Vector3.up * CharacterPresentationProfile.HeadHeight;
            Physics.SyncTransforms();
            Assert.That(AimAssistTargeting.FindBest(owner, origin, Vector3.forward), Is.SameAs(target));

            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Dense Aim Assist Cover";
            blocker.transform.position = new Vector3(700f, CharacterPresentationProfile.HeadHeight, 2f);
            blocker.transform.localScale = new Vector3(1f, 1f, 0.1f);
            Physics.SyncTransforms();

            Assert.That(AimAssistTargeting.FindBest(owner, origin, Vector3.forward), Is.Null);
            Object.DestroyImmediate(blocker);
            ParticipantRegistry.Unregister(target);
            Object.DestroyImmediate(target.gameObject);
            Object.DestroyImmediate(owner);
        }

        [Test]
        public void ShotgunUsesMultipleBallisticPellets()
        {
            Assert.That(WeaponSystem.ShotgunPelletCount, Is.EqualTo(7));
            Assert.That(WeaponSystem.ShotgunPelletCount, Is.GreaterThan(1));
        }

        [Test]
        public void WeaponDamageFallsOffOnlyAfterConfiguredDistance()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.damage = 30f;
            weapon.range = 100f;
            weapon.falloffStartRatio = 0.5f;
            weapon.minimumDamageMultiplier = 0.6f;

            Assert.That(WeaponSystem.DamageAtDistance(weapon, 25f), Is.EqualTo(30f).Within(0.01f));
            Assert.That(WeaponSystem.DamageAtDistance(weapon, 75f), Is.EqualTo(24f).Within(0.01f));
            Assert.That(WeaponSystem.DamageAtDistance(weapon, 100f), Is.EqualTo(18f).Within(0.01f));
            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void DeathContainerTransfersOnlySelectedEntry()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.displayName = "Dropped Rifle";
            weapon.magazineSize = 30;
            var secondary = ScriptableObject.CreateInstance<WeaponDefinition>();
            secondary.displayName = "Dropped Sidearm";
            secondary.magazineSize = 12;
            var victim = CreateParticipant("Loot Victim", false);
            victim.Weapon.EquipInitial(weapon);
            victim.Weapon.EquipInitial(secondary);
            victim.Inventory.AddAmmo(AmmoKind.Medium, 42);
            victim.Inventory.AddMedKit(2);
            victim.Health.EquipVest(2);
            victim.Health.EquipHelmet(1);
            var receiver = CreateParticipant("Loot Receiver", true);

            var container = DeathLootContainer.Create(victim);
            var before = container.Entries.Count;
            var ammoIndex = -1;
            var weaponEntries = 0;
            for (var i = 0; i < container.Entries.Count; i++)
            {
                if (container.Entries[i].Kind == DeathLootKind.Weapon) weaponEntries++;
                if (container.Entries[i].Kind == DeathLootKind.Ammo) ammoIndex = i;
            }

            Assert.That(before, Is.GreaterThanOrEqualTo(6));
            Assert.That(weaponEntries, Is.EqualTo(2));
            Assert.That(ammoIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(container.Take(ammoIndex, receiver), Is.True);
            Assert.That(container.Entries.Count, Is.EqualTo(before - 1));
            Assert.That(receiver.Inventory.AmmoFor(AmmoKind.Medium), Is.EqualTo(42));

            Object.DestroyImmediate(container.gameObject);
            Object.DestroyImmediate(victim.gameObject);
            Object.DestroyImmediate(receiver.gameObject);
            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(secondary);
        }

        [Test]
        public void DeathContainerSnapshotUsesExactLoadedReserveAndKitAmounts()
        {
            DeathLootContainer.ClearAll();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            var victim = CreateParticipant("Exact Snapshot Victim", false);
            try
            {
                weapon.displayName = "Exact Rifle";
                weapon.magazineSize = 30;
                weapon.ammoKind = AmmoKind.Medium;
                victim.Weapon.EquipInitial(weapon);
                victim.Inventory.AddAmmo(AmmoKind.Medium, 42);
                victim.Inventory.AddMedKit(3);
                var magazinesField = typeof(WeaponSystem).GetField("magazines",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var magazines = (System.Collections.Generic.Dictionary<WeaponDefinition, int>)
                    magazinesField.GetValue(victim.Weapon);
                magazines[weapon] = 7;

                var container = DeathLootContainer.Create(victim);
                DeathLootEntry weaponEntry = null;
                DeathLootEntry ammoEntry = null;
                DeathLootEntry kitEntry = null;
                foreach (var entry in container.Entries)
                {
                    if (entry.Kind == DeathLootKind.Weapon) weaponEntry = entry;
                    else if (entry.Kind == DeathLootKind.Ammo && entry.AmmoKind == AmmoKind.Medium) ammoEntry = entry;
                    else if (entry.Kind == DeathLootKind.MedKit) kitEntry = entry;
                }

                Assert.That(weaponEntry.LoadedMagazine, Is.EqualTo(7));
                Assert.That(ammoEntry.Amount, Is.EqualTo(42));
                Assert.That(kitEntry.Amount, Is.EqualTo(3));
            }
            finally
            {
                DeathLootContainer.ClearAll();
                DestroyParticipants(victim);
                Object.DestroyImmediate(weapon);
            }
        }

        [Test]
        public void DeathContainerStabilizerPreservesAbsoluteLevelAndRejectsEqualLevel()
        {
            DeathLootContainer.ClearAll();
            var victim = CreateParticipant("Attachment Loot Victim", false);
            var receiver = CreateParticipant("Attachment Loot Receiver", true);
            try
            {
                Assert.That(victim.Inventory.TryAddStabilizer(2), Is.True);
                Assert.That(receiver.Inventory.TryAddStabilizer(2), Is.True);
                var container = DeathLootContainer.Create(victim);
                DeathLootEntry attachment = null;
                foreach (var entry in container.Entries)
                    if (entry.Kind == DeathLootKind.Attachment) attachment = entry;

                Assert.That(attachment, Is.Not.Null);
                var result = container.TakeById(attachment.EntryId, receiver);

                Assert.That(result.Success, Is.False);
                Assert.That(receiver.Inventory.StabilizerLevel, Is.EqualTo(2));
                Assert.That(container.Entries, Has.Some.Matches<DeathLootEntry>(entry =>
                    entry.EntryId == attachment.EntryId && entry.Level == 2));
            }
            finally
            {
                DeathLootContainer.ClearAll();
                DestroyParticipants(victim, receiver);
            }
        }

        [Test]
        public void DamageReportCapsAppliedDamageAndDoesNotScoreDownedFinisherDamage()
        {
            var target = new GameObject("Applied Damage Target");
            try
            {
                var health = target.AddComponent<HealthArmorSystem>();
                health.InitializeFull();
                health.ApplyDamage(99f, false, null);

                var lethal = health.ApplyDamage(30f, false, null);
                Assert.That(lethal.FinalDamage, Is.EqualTo(30f));
                Assert.That(lethal.AppliedDamage, Is.EqualTo(1f).Within(0.001f));

                health.InitializeFull();
                health.ConfigureLifeCycle(24f, 35f, () => true);
                health.ApplyDamage(1000f, false, null);
                var finisher = health.ApplyDamage(30f, false, null);
                Assert.That(finisher.FinalDamage, Is.EqualTo(30f));
                Assert.That(finisher.AppliedDamage, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void DeathLootEntryIdsAreUniqueAndRemovedIdIsIdempotent()
        {
            DeathLootContainer.ClearAll();
            var victim = CreateParticipant("Entry Id Victim", false);
            var receiver = CreateParticipant("Entry Id Receiver", true);
            try
            {
                victim.Inventory.AddAmmo(AmmoKind.Light, 10);
                victim.Inventory.AddMedKit(2);
                var container = DeathLootContainer.Create(victim);
                var firstId = container.Entries[0].EntryId;
                var secondId = container.Entries[1].EntryId;
                var first = container.TakeById(secondId, receiver);
                var repeated = container.TakeById(secondId, receiver);

                Assert.That(firstId, Is.GreaterThan(0));
                Assert.That(secondId, Is.Not.EqualTo(firstId));
                Assert.That(first.AcceptedAmount, Is.EqualTo(2));
                Assert.That(repeated.Success, Is.False);
                Assert.That(receiver.Inventory.MedKits, Is.EqualTo(2));
                Assert.That(container.Entries.Count, Is.EqualTo(1));
                Assert.That(container.Entries[0].EntryId, Is.EqualTo(firstId));
            }
            finally
            {
                DeathLootContainer.ClearAll();
                DestroyParticipants(victim, receiver);
            }
        }

        [Test]
        public void FailedDeathLootTransferDoesNotMutateContainerOrInventory()
        {
            DeathLootContainer.ClearAll();
            var victim = CreateParticipant("Atomic Loot Victim", false);
            var receiver = CreateParticipant("Atomic Loot Receiver", true);
            try
            {
                victim.Inventory.AddAmmo(AmmoKind.Shell, 12);
                receiver.Inventory.AddAmmo(AmmoKind.Shell, InventorySystem.MaxAmmoPerKind);
                var container = DeathLootContainer.Create(victim);
                var entry = container.Entries[0];
                var result = container.TakeById(entry.EntryId, receiver);

                Assert.That(result.Success, Is.False);
                Assert.That(result.RemainingAmount, Is.EqualTo(12));
                Assert.That(receiver.Inventory.AmmoFor(AmmoKind.Shell), Is.EqualTo(240));
                Assert.That(container.Entries.Count, Is.EqualTo(1));
                Assert.That(container.Entries[0].EntryId, Is.EqualTo(entry.EntryId));
                Assert.That(container.Entries[0].Amount, Is.EqualTo(12));
                Assert.That(container.gameObject.activeSelf, Is.True);
            }
            finally
            {
                DeathLootContainer.ClearAll();
                DestroyParticipants(victim, receiver);
            }
        }

        [Test]
        public void DownedReceiverCannotTakeDeathLootByIndexOrId()
        {
            DeathLootContainer.ClearAll();
            var victim = CreateParticipant("Downed Take Victim", false, 70);
            var receiver = CreateParticipant("Downed Take Receiver", true, 71);
            try
            {
                victim.Inventory.AddAmmo(AmmoKind.Long, 18);
                var container = DeathLootContainer.Create(victim);
                var entryId = container.Entries[0].EntryId;
                receiver.Health.ConfigureLifeCycle(30f, 30f, () => true);
                receiver.Health.ApplyDamage(1000f, false, null);

                Assert.That(container.Take(0, receiver), Is.False);
                var result = container.TakeById(entryId, receiver);

                Assert.That(receiver.IsDowned, Is.True);
                Assert.That(result.Success, Is.False);
                Assert.That(receiver.Inventory.AmmoFor(AmmoKind.Long), Is.Zero);
                Assert.That(container.Entries.Count, Is.EqualTo(1));
                Assert.That(container.Entries[0].EntryId, Is.EqualTo(entryId));
                Assert.That(container.Entries[0].Amount, Is.EqualTo(18));
                Assert.That(container.gameObject.activeSelf, Is.True);
            }
            finally
            {
                DeathLootContainer.ClearAll();
                DestroyParticipants(victim, receiver);
            }
        }

        [Test]
        public void PartialDeathLootTransferPreservesEntryId()
        {
            DeathLootContainer.ClearAll();
            var victim = CreateParticipant("Partial Id Victim", false);
            var receiver = CreateParticipant("Partial Id Receiver", true);
            try
            {
                victim.Inventory.AddAmmo(AmmoKind.Shell, 20);
                receiver.Inventory.AddAmmo(AmmoKind.Shell, 235);
                var container = DeathLootContainer.Create(victim);
                var entryId = container.Entries[0].EntryId;

                var result = container.TakeById(entryId, receiver);

                Assert.That(result.AcceptedAmount, Is.EqualTo(5));
                Assert.That(result.RemainingAmount, Is.EqualTo(15));
                Assert.That(container.Entries.Count, Is.EqualTo(1));
                Assert.That(container.Entries[0].EntryId, Is.EqualTo(entryId));
                Assert.That(container.Entries[0].Amount, Is.EqualTo(15));
            }
            finally
            {
                DeathLootContainer.ClearAll();
                DestroyParticipants(victim, receiver);
            }
        }

        [Test]
        public void ManagerDeathLootClickUsesCapturedIdAfterListIndexChanges()
        {
            DeathLootContainer.ClearAll();
            var managerObject = new GameObject("Death Loot Id Manager");
            var victim = CreateParticipant("Death Loot Id Victim", false);
            var player = CreateParticipant("Death Loot Id Player", true);
            var other = CreateParticipant("Death Loot Id Other", true);
            try
            {
                victim.Inventory.AddAmmo(AmmoKind.Light, 10);
                victim.Inventory.AddMedKit(1);
                var container = DeathLootContainer.Create(victim);
                var capturedId = container.Entries[0].EntryId;
                var shiftedEntryId = container.Entries[1].EntryId;
                var manager = managerObject.AddComponent<MatchManager>();
                typeof(MatchManager).GetProperty("Player").SetValue(manager, player);
                typeof(MatchManager).GetProperty("State").SetValue(manager, MatchState.Active);
                manager.OpenDeathLoot(container);

                Assert.That(container.TakeById(capturedId, other).Success, Is.True);
                Assert.That(container.Entries[0].EntryId, Is.EqualTo(shiftedEntryId));
                Assert.That(manager.TakeDeathLootEntryById(capturedId), Is.False);
                Assert.That(player.Inventory.MedKits, Is.Zero);
                Assert.That(container.Entries.Count, Is.EqualTo(1));
                Assert.That(container.Entries[0].EntryId, Is.EqualTo(shiftedEntryId));

                Assert.That(manager.TakeDeathLootEntry(0), Is.True);
                Assert.That(player.Inventory.MedKits, Is.EqualTo(1));
                Assert.That(container.Empty, Is.True);
            }
            finally
            {
                DeathLootContainer.ClearAll();
                Object.DestroyImmediate(managerObject);
                DestroyParticipants(victim, player, other);
            }
        }

        [Test]
        public void DeathContainerKeepsWeaponSwappedOutByReceiver()
        {
            DeathLootContainer.ClearAll();
            var dropped = ScriptableObject.CreateInstance<WeaponDefinition>();
            var first = ScriptableObject.CreateInstance<WeaponDefinition>();
            var second = ScriptableObject.CreateInstance<WeaponDefinition>();
            var victim = CreateParticipant("Swap Victim", false);
            var receiver = CreateParticipant("Swap Receiver", true);
            try
            {
                dropped.displayName = "Crate Rifle";
                dropped.magazineSize = 30;
                first.displayName = "Owned Primary";
                first.magazineSize = 20;
                second.displayName = "Owned Secondary";
                second.magazineSize = 12;
                victim.Weapon.EquipInitial(dropped);
                receiver.Weapon.EquipInitial(first);
                receiver.Weapon.EquipInitial(second);
                var magazinesField = typeof(WeaponSystem).GetField("magazines",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var magazines = (System.Collections.Generic.Dictionary<WeaponDefinition, int>)
                    magazinesField.GetValue(receiver.Weapon);
                magazines[second] = 4;
                var container = DeathLootContainer.Create(victim);
                var before = container.Entries.Count;
                var originalId = container.Entries[0].EntryId;

                Assert.That(container.TakeById(originalId, receiver).Success, Is.True);
                Assert.That(container.Entries.Count, Is.EqualTo(before));
                Assert.That(receiver.Inventory.ActiveWeapon, Is.SameAs(dropped));
                Assert.That(receiver.Weapon.MagazineFor(dropped), Is.EqualTo(30));
                Assert.That(container.Entries[0].Weapon, Is.SameAs(second));
                Assert.That(container.Entries[0].LoadedMagazine, Is.EqualTo(4));
                Assert.That(container.Entries[0].EntryId, Is.Not.EqualTo(originalId));
                Assert.That(container.TakeById(originalId, receiver).Success, Is.False);
            }
            finally
            {
                DeathLootContainer.ClearAll();
                DestroyParticipants(victim, receiver);
                Object.DestroyImmediate(dropped);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void TakeAllContinuesPastFailuresAndKeepsOnlyRealRemainders()
        {
            DeathLootContainer.ClearAll();
            var victim = CreateParticipant("Mixed Take All Victim", false);
            var receiver = CreateParticipant("Mixed Take All Receiver", true);
            try
            {
                victim.Inventory.AddAmmo(AmmoKind.Long, 20);
                victim.Inventory.AddMedKit(2);
                victim.Health.EquipVest(1);
                victim.Health.EquipHelmet(1);
                receiver.Inventory.AddAmmo(AmmoKind.Long, 235);
                receiver.Inventory.AddMedKit(5);
                receiver.Health.EquipVest(1);
                var container = DeathLootContainer.Create(victim);

                var successes = container.TakeAll(receiver);

                Assert.That(successes, Is.EqualTo(2));
                Assert.That(receiver.Inventory.AmmoFor(AmmoKind.Long), Is.EqualTo(240));
                Assert.That(receiver.Inventory.MedKits, Is.EqualTo(5));
                Assert.That(receiver.Health.HelmetLevel, Is.EqualTo(1));
                Assert.That(container.Entries.Count, Is.EqualTo(3));
                Assert.That(container.gameObject.activeSelf, Is.True);
                Assert.That(container.Entries[0].Kind, Is.EqualTo(DeathLootKind.Ammo));
                Assert.That(container.Entries[0].Amount, Is.EqualTo(15));
            }
            finally
            {
                DeathLootContainer.ClearAll();
                DestroyParticipants(victim, receiver);
            }
        }

        [Test]
        public void DeathContainerDeactivatesOnlyAfterItsLastEntryIsTaken()
        {
            DeathLootContainer.ClearAll();
            var victim = CreateParticipant("Empty Loot Victim", false);
            var receiver = CreateParticipant("Empty Loot Receiver", true);
            try
            {
                victim.Inventory.AddMedKit(1);
                var container = DeathLootContainer.Create(victim);
                var entryId = container.Entries[0].EntryId;

                Assert.That(container.TakeById(entryId, receiver).Success, Is.True);
                Assert.That(container.Empty, Is.True);
                Assert.That(container.gameObject.activeSelf, Is.False);
                Assert.That(DeathLootContainer.FindNearest(container.transform.position, 5f), Is.Null);
            }
            finally
            {
                DeathLootContainer.ClearAll();
                DestroyParticipants(victim, receiver);
            }
        }

        [Test]
        public void EmptySnapshotContainerIsImmediatelyInactiveAndNonInteractable()
        {
            DeathLootContainer.ClearAll();
            var victim = CreateParticipant("Empty Snapshot Victim", false);
            try
            {
                var container = DeathLootContainer.Create(victim);
                container.SetOpen(true);

                Assert.That(container, Is.Not.Null);
                Assert.That(container.Empty, Is.True);
                Assert.That(container.gameObject.activeSelf, Is.False);
                Assert.That(container.IsOpen, Is.False);
                Assert.That(DeathLootContainer.FindNearest(container.transform.position, 5f), Is.Null);
            }
            finally
            {
                DeathLootContainer.ClearAll();
                DestroyParticipants(victim);
            }
        }

        [Test]
        public void BotStyleLootSelectionPrioritizesWeaponWhenUnarmed()
        {
            var dropped = ScriptableObject.CreateInstance<WeaponDefinition>();
            dropped.displayName = "Priority Weapon";
            dropped.magazineSize = 24;
            var victim = CreateParticipant("Priority Victim", false);
            victim.Weapon.EquipInitial(dropped);
            victim.Inventory.AddMedKit(2);
            var receiver = CreateParticipant("Unarmed Receiver", false);
            var container = DeathLootContainer.Create(victim);

            Assert.That(container.TryTakeBest(receiver), Is.True);
            Assert.That(receiver.Inventory.ActiveWeapon, Is.SameAs(dropped));

            Object.DestroyImmediate(container.gameObject);
            Object.DestroyImmediate(victim.gameObject);
            Object.DestroyImmediate(receiver.gameObject);
            Object.DestroyImmediate(dropped);
        }

        [Test]
        public void AutoPickupHonorsPolicyIntervalAndDownedExclusion()
        {
            var managerObject = new GameObject("Auto Pickup Manager");
            var spawnerObject = new GameObject("Auto Pickup Spawner");
            var participant = CreateParticipant("Auto Pickup Player", true, 41);
            try
            {
                var manager = managerObject.AddComponent<MatchManager>();
                var spawner = spawnerObject.AddComponent<LootSpawner>();
                participant.SetPhase(ParticipantPhase.Grounded);
                typeof(MatchManager).GetField("lootSpawner",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(manager, spawner);
                typeof(MatchManager).GetProperty("Player").SetValue(manager, participant);
                typeof(MatchManager).GetProperty("State").SetValue(manager, MatchState.Active);

                var ammoObject = new GameObject("Auto Ammo");
                ammoObject.transform.SetParent(spawnerObject.transform);
                var ammo = ammoObject.AddComponent<LootPickup>();
                ammo.ConfigureAmmo(AmmoKind.Light, 2);
                ((System.Collections.Generic.List<LootPickup>)spawner.Pickups).Add(ammo);

                Assert.That(MatchManager.IsAutoPickupKind(LootKind.Ammo), Is.True);
                Assert.That(MatchManager.IsAutoPickupKind(LootKind.Heal), Is.True);
                Assert.That(MatchManager.IsAutoPickupKind(LootKind.Attachment), Is.True);
                Assert.That(MatchManager.IsAutoPickupKind(LootKind.Weapon), Is.False);
                Assert.That(MatchManager.IsAutoPickupKind(LootKind.Vest), Is.False);
                Assert.That(MatchManager.IsAutoPickupEligible(MatchState.Active, participant, true, false), Is.False);
                Assert.That(MatchManager.IsAutoPickupEligible(MatchState.Active, participant, false, true), Is.False);
                Assert.That(manager.TickAutoPickup(10f), Is.True);
                Assert.That(participant.Inventory.AmmoFor(AmmoKind.Light), Is.EqualTo(2));

                var healObject = new GameObject("Auto Heal");
                healObject.transform.SetParent(spawnerObject.transform);
                var heal = healObject.AddComponent<LootPickup>();
                heal.ConfigureHeal(2);
                ((System.Collections.Generic.List<LootPickup>)spawner.Pickups).Add(heal);
                Assert.That(manager.TickAutoPickup(10.1f), Is.False);
                Assert.That(heal.Amount, Is.EqualTo(2));
                Assert.That(manager.TickAutoPickup(10.15f), Is.True);
                Assert.That(participant.Inventory.MedKits, Is.EqualTo(2));

                participant.Inventory.AddAmmo(AmmoKind.Light, 238);
                var fullObject = new GameObject("Full Auto Ammo");
                fullObject.transform.SetParent(spawnerObject.transform);
                var fullAmmo = fullObject.AddComponent<LootPickup>();
                fullAmmo.ConfigureAmmo(AmmoKind.Light, 4);
                ((System.Collections.Generic.List<LootPickup>)spawner.Pickups).Add(fullAmmo);
                var feedbackBeforeCapacityFailure = manager.PickupFeedback;
                Assert.That(manager.TickAutoPickup(10.3f), Is.False);
                Assert.That(fullAmmo.Amount, Is.EqualTo(4));
                Assert.That(fullObject.activeSelf, Is.True);
                Assert.That(manager.PickupFeedback, Is.EqualTo(feedbackBeforeCapacityFailure));

                participant.Health.ConfigureLifeCycle(30f, 30f, () => true);
                participant.Health.ApplyDamage(1000f, false, null);
                var downedObject = new GameObject("Downed Auto Ammo");
                downedObject.transform.SetParent(spawnerObject.transform);
                var downedAmmo = downedObject.AddComponent<LootPickup>();
                downedAmmo.ConfigureAmmo(AmmoKind.Light, 4);
                ((System.Collections.Generic.List<LootPickup>)spawner.Pickups).Add(downedAmmo);
                Assert.That(manager.TickAutoPickup(10.45f), Is.False);
                Assert.That(downedAmmo.Amount, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(spawnerObject);
                DestroyParticipants(participant);
            }
        }

        [Test]
        public void AutoPickupRealPathEnforcesRangeStatePhaseKindsAndCollectsAttachment()
        {
            var managerObject = new GameObject("Auto Policy Manager");
            var spawnerObject = new GameObject("Auto Policy Spawner");
            var participant = CreateParticipant("Auto Policy Player", true, 81);
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                var manager = managerObject.AddComponent<MatchManager>();
                var spawner = spawnerObject.AddComponent<LootSpawner>();
                participant.SetPhase(ParticipantPhase.Grounded);
                typeof(MatchManager).GetField("lootSpawner",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(manager, spawner);
                typeof(MatchManager).GetProperty("Player").SetValue(manager, participant);
                typeof(MatchManager).GetProperty("State").SetValue(manager, MatchState.Active);

                var healObject = new GameObject("Out Of Range Heal");
                healObject.transform.SetParent(spawnerObject.transform);
                healObject.transform.position = Vector3.right * 1.36f;
                var heal = healObject.AddComponent<LootPickup>();
                heal.ConfigureHeal();
                ((System.Collections.Generic.List<LootPickup>)spawner.Pickups).Add(heal);
                Assert.That(manager.TickAutoPickup(1f), Is.False);
                Assert.That(heal.Collected, Is.False);

                healObject.transform.position = Vector3.zero;
                typeof(MatchManager).GetProperty("State").SetValue(manager, MatchState.Lobby);
                Assert.That(manager.TickAutoPickup(2f), Is.False);
                Assert.That(heal.Collected, Is.False);

                typeof(MatchManager).GetProperty("State").SetValue(manager, MatchState.Active);
                participant.SetPhase(ParticipantPhase.Freefall);
                Assert.That(manager.TickAutoPickup(3f), Is.False);
                Assert.That(heal.Collected, Is.False);

                participant.SetPhase(ParticipantPhase.Grounded);
                Assert.That(manager.TickAutoPickup(4f), Is.True);
                Assert.That(participant.Inventory.MedKits, Is.EqualTo(1));

                weapon.displayName = "Excluded Auto Weapon";
                weapon.magazineSize = 20;
                var weaponPickup = AddTestPickup(spawner, "Excluded Weapon", pickup => pickup.ConfigureWeapon(weapon));
                var vestPickup = AddTestPickup(spawner, "Excluded Vest", pickup => pickup.ConfigureVest(2));
                var helmetPickup = AddTestPickup(spawner, "Excluded Helmet", pickup => pickup.ConfigureHelmet(2));
                var attachmentPickup = AddTestPickup(spawner, "Auto Attachment", pickup => pickup.ConfigureAttachment());

                Assert.That(manager.TickAutoPickup(5f), Is.True);
                Assert.That(participant.Inventory.StabilizerLevel, Is.EqualTo(1));
                Assert.That(attachmentPickup.Collected, Is.True);
                Assert.That(manager.TickAutoPickup(6f), Is.False);
                Assert.That(weaponPickup.Collected, Is.False);
                Assert.That(vestPickup.Collected, Is.False);
                Assert.That(helmetPickup.Collected, Is.False);
                Assert.That(weaponPickup.gameObject.activeSelf, Is.True);
                Assert.That(vestPickup.gameObject.activeSelf, Is.True);
                Assert.That(helmetPickup.gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(spawnerObject);
                DestroyParticipants(participant);
                Object.DestroyImmediate(weapon);
            }
        }

        [Test]
        public void BotGroundAmmoTransferLeavesPartialPickupRemainder()
        {
            var spawnerObject = new GameObject("Bot Partial Loot Spawner");
            var participant = CreateParticipant("Partial Loot Bot", false, 91);
            try
            {
                var spawner = spawnerObject.AddComponent<LootSpawner>();
                participant.SetPhase(ParticipantPhase.Grounded);
                participant.Inventory.AddAmmo(AmmoKind.Long, 230);
                var pickup = AddTestPickup(spawner, "Bot Partial Ammo",
                    value => value.ConfigureAmmo(AmmoKind.Long, 25));
                var bot = participant.gameObject.AddComponent<BotController>();
                bot.Configure(participant, spawner, null, new BRGameConfig());

                InvokePrivate(bot, "TryCollectNearby");

                Assert.That(participant.Inventory.AmmoFor(AmmoKind.Long), Is.EqualTo(240));
                Assert.That(pickup.Amount, Is.EqualTo(15));
                Assert.That(pickup.Collected, Is.False);
                Assert.That(pickup.gameObject.activeSelf, Is.True);
                Assert.That(pickup.DisplayName, Does.Contain("x15"));
            }
            finally
            {
                Object.DestroyImmediate(spawnerObject);
                DestroyParticipants(participant);
            }
        }

        [Test]
        public void BotSkipsRejectedNearestPickupAndFallsBackToValidPickupThenContainer()
        {
            DeathLootContainer.ClearAll();
            var spawnerObject = new GameObject("Bot Candidate Spawner");
            var participant = CreateParticipant("Candidate Bot", false, 95);
            var victim = CreateParticipant("Candidate Box Victim", false, 96);
            try
            {
                var spawner = spawnerObject.AddComponent<LootSpawner>();
                participant.SetPhase(ParticipantPhase.Grounded);
                participant.Health.EquipVest(2);
                var rejected = AddTestPickup(spawner, "Rejected Near Vest", value => value.ConfigureVest(1));
                rejected.transform.position = participant.transform.position + Vector3.right * 0.1f;
                var valid = AddTestPickup(spawner, "Valid Far Ammo",
                    value => value.ConfigureAmmo(AmmoKind.Light, 8));
                valid.transform.position = participant.transform.position + Vector3.right * 0.7f;
                var bot = participant.gameObject.AddComponent<BotController>();
                bot.Configure(participant, spawner, null, new BRGameConfig());

                InvokePrivate(bot, "TryCollectNearby");

                Assert.That(rejected.Collected, Is.False);
                Assert.That(rejected.gameObject.activeSelf, Is.True);
                Assert.That(valid.Collected, Is.True);
                Assert.That(participant.Inventory.AmmoFor(AmmoKind.Light), Is.EqualTo(8));

                victim.Inventory.AddMedKit(1);
                Assert.That(victim.Inventory.MedKits, Is.EqualTo(1));
                victim.transform.position = participant.transform.position;
                var container = DeathLootContainer.Create(victim);
                container.transform.position = participant.transform.position;
                InvokePrivate(container, "OnEnable");
                Assert.That(container.Empty, Is.False);
                Assert.That(container.gameObject.activeSelf, Is.True);
                Assert.That(DeathLootContainer.FindNearest(participant.transform.position,
                    new BRGameConfig().interactRadius + 0.5f), Is.SameAs(container));
                typeof(BotController).GetField("nextCollect",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(bot, 0f);
                InvokePrivate(bot, "TryCollectNearby");

                Assert.That(participant.Inventory.MedKits, Is.EqualTo(1));
                Assert.That(container.Empty, Is.True);
                Assert.That(container.gameObject.activeSelf, Is.False);
            }
            finally
            {
                DeathLootContainer.ClearAll();
                Object.DestroyImmediate(spawnerObject);
                DestroyParticipants(participant, victim);
            }
        }

        [Test]
        public void ParticipantRegistryRejectsDuplicatesAndRemovesParticipants()
        {
            var initial = ParticipantRegistry.Count;
            var participant = CreateParticipant("Registry Participant", false);

            ParticipantRegistry.Register(participant);
            ParticipantRegistry.Register(participant);
            Assert.That(ParticipantRegistry.Count, Is.EqualTo(initial + 1));

            ParticipantRegistry.Unregister(participant);
            Assert.That(ParticipantRegistry.Count, Is.EqualTo(initial));
            Object.DestroyImmediate(participant.gameObject);
        }

        [Test]
        public void HudLayoutNormalizesAndRestoresInsideSafeArea()
        {
            var safe = new Rect(20f, 30f, 1000f, 600f);
            var original = new Rect(170f, 130f, 250f, 120f);
            var normalized = HUDLayoutProfile.Normalize(original, safe);
            var restored = HUDLayoutProfile.Denormalize(normalized, safe);

            Assert.That(restored.x, Is.EqualTo(original.x).Within(0.001f));
            Assert.That(restored.y, Is.EqualTo(original.y).Within(0.001f));
            Assert.That(restored.width, Is.EqualTo(original.width).Within(0.001f));
            Assert.That(restored.height, Is.EqualTo(original.height).Within(0.001f));
        }

        [Test]
        public void HudLayoutRepairAddsDefaultsAndClampsInvalidValues()
        {
            var safe = new Rect(0f, 0f, 800f, 450f);
            var profile = new HUDLayoutProfile { mobile = false };
            profile.elements.Add(new HUDLayoutEntry
            {
                id = HUDElementId.Vitals,
                normalizedRect = new Rect(float.NaN, -4f, -1f, 0f),
                opacity = float.PositiveInfinity
            });

            profile.Repair(safe, _ => new Rect(10f, 12f, 180f, 60f));

            Assert.That(profile.Get(HUDElementId.Vitals).normalizedRect.width, Is.GreaterThan(0f));
            Assert.That(profile.Get(HUDElementId.Vitals).opacity, Is.EqualTo(1f));
            Assert.That(profile.Get(HUDElementId.Minimap), Is.Not.Null);
            Assert.That(profile.Get(HUDElementId.Fire), Is.Null);
        }

        [Test]
        public void HudLayoutJsonRoundTripKeepsUserSettings()
        {
            var profile = new HUDLayoutProfile { mobile = true };
            profile.elements.Add(new HUDLayoutEntry
            {
                id = HUDElementId.Fire,
                normalizedRect = new Rect(0.8f, 0.7f, 0.1f, 0.12f),
                opacity = 0.63f,
                visible = false
            });

            var restored = JsonUtility.FromJson<HUDLayoutProfile>(JsonUtility.ToJson(profile));
            var fire = restored.Get(HUDElementId.Fire);
            Assert.That(fire.opacity, Is.EqualTo(0.63f).Within(0.001f));
            Assert.That(fire.visible, Is.False);
            Assert.That(fire.normalizedRect.x, Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void LegacyMobileProfileUsesCurrentDefaultsAndPersistsMigration()
        {
            const string key = "RavenDrop.HUD.Mobile.v1";
            var hadValue = PlayerPrefs.HasKey(key);
            var originalValue = PlayerPrefs.GetString(key, string.Empty);
            var safeArea = new Rect(0f, 0f, 360f, 640f);
            var expectedFire = new Rect(274f, 536f, 72f, 72f);
            try
            {
                var legacy = new HUDLayoutProfile { version = 1, mobile = true };
                legacy.elements.Add(new HUDLayoutEntry
                {
                    id = HUDElementId.Fire,
                    normalizedRect = new Rect(0.82f, 0.7f, 0.08f, 0.16f),
                    opacity = 0.63f,
                    visible = false
                });
                PlayerPrefs.SetString(key, JsonUtility.ToJson(legacy));
                PlayerPrefs.Save();

                Rect Defaults(HUDElementId id) => id == HUDElementId.Fire
                    ? expectedFire
                    : new Rect(12f, 16f, 40f, 40f);
                var loaded = HUDLayoutProfile.Load(true, safeArea, Defaults);
                var fire = loaded.Get(HUDElementId.Fire);
                var resolved = loaded.Resolve(HUDElementId.Fire, safeArea, default);

                Assert.That(loaded.version, Is.EqualTo(HUDLayoutProfile.CurrentVersion));
                Assert.That(resolved, Is.EqualTo(expectedFire));
                Assert.That(resolved.width, Is.EqualTo(resolved.height));
                Assert.That(fire.visible, Is.False);
                Assert.That(fire.opacity, Is.EqualTo(0.63f).Within(0.001f));

                var persisted = JsonUtility.FromJson<HUDLayoutProfile>(PlayerPrefs.GetString(key));
                var persistedFire = persisted.Get(HUDElementId.Fire);
                Assert.That(persisted.version, Is.EqualTo(HUDLayoutProfile.CurrentVersion));
                Assert.That(persisted.Resolve(HUDElementId.Fire, safeArea, default), Is.EqualTo(expectedFire));
                Assert.That(persistedFire.visible, Is.False);
                Assert.That(persistedFire.opacity, Is.EqualTo(0.63f).Within(0.001f));
            }
            finally
            {
                if (hadValue) PlayerPrefs.SetString(key, originalValue);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void MovementAccelerationAndAdsMultiplierAreResponsive()
        {
            var accelerated = GameplayTuning.AcceleratePlanar(Vector3.zero, Vector3.forward * 6f, true, 0.1f);
            var braking = GameplayTuning.AcceleratePlanar(Vector3.forward * 6f, Vector3.zero, true, 0.1f);
            Assert.That(accelerated.magnitude, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(braking.magnitude, Is.EqualTo(2.6f).Within(0.001f));
            Assert.That(GameplayTuning.MovementMultiplier(false, false, true), Is.EqualTo(0.72f).Within(0.001f));
            Assert.That(GameplayTuning.MovementMultiplier(true, false, true), Is.EqualTo(0.72f).Within(0.001f));
        }

        [Test]
        public void RecoilPatternIsDeterministicAndCrosshairTracksSpread()
        {
            Assert.That(GameplayTuning.RecoilStep(4, 1.1f, 0.5f), Is.EqualTo(GameplayTuning.RecoilStep(4, 1.1f, 0.5f)));
            Assert.That(GameplayTuning.RecoilStep(5, 1.1f, 0.5f), Is.Not.EqualTo(GameplayTuning.RecoilStep(4, 1.1f, 0.5f)));
            var tight = GameplayTuning.CrosshairGap(0.5f, 62f, 720f);
            var wide = GameplayTuning.CrosshairGap(4f, 62f, 720f);
            Assert.That(wide, Is.GreaterThan(tight));
            Assert.That(wide, Is.LessThanOrEqualTo(24f));
        }

        [Test]
        public void HudSizeControlScalesAroundElementCenter()
        {
            var current = new Rect(100f, 200f, 80f, 60f);
            var reference = new Rect(0f, 0f, 80f, 60f);
            var resized = HUDLayoutProfile.ScaleAroundCenter(current, reference, 1.5f);

            Assert.That(resized.center, Is.EqualTo(current.center));
            Assert.That(resized.size, Is.EqualTo(new Vector2(120f, 90f)));
            Assert.That(HUDLayoutProfile.UniformScale(resized, reference), Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void InputFrameCarriesHeldInteractionState()
        {
            var frame = new BRInputFrame(Vector2.zero, Vector2.zero, false, false, false, false,
                false, false, false, false, false, false, -1, false, false, true);
            Assert.That(frame.InteractHeld, Is.True);
        }

        private static GameObject CreateCameraWall(string name, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = position;
            wall.transform.localScale = scale;
            return wall;
        }

        private static void AssertCameraPathClear(Vector3 start, Vector3 end, float radius)
        {
            var path = end - start;
            Assert.That(path.sqrMagnitude, Is.GreaterThan(0.000001f));
            var hits = Physics.SphereCastAll(start, radius, path.normalized, path.magnitude,
                ~0, QueryTriggerInteraction.Ignore);
            Assert.That(hits, Is.Empty, "Camera path crossed blocking geometry");
        }

        private static (GameObject character, BRCharacterMotor motor, CharacterController controller,
            GameObject ground, GameObject obstacle, float x) CreateVaultFixture(float x)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Vault Test Ground";
            ground.transform.position = new Vector3(x, -0.1f, 0.8f);
            ground.transform.localScale = new Vector3(5f, 0.2f, 4f);
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Vault Test Low Obstacle";
            obstacle.transform.position = new Vector3(x, 0.245f, 0.68f);
            obstacle.transform.localScale = new Vector3(0.9f, 0.49f, 0.18f);
            var character = new GameObject("Vault Test Character");
            character.transform.position = new Vector3(x, 0f, 0f);
            var controller = character.AddComponent<CharacterController>();
            CharacterPresentationProfile.Apply(controller);
            var motor = character.AddComponent<BRCharacterMotor>();
            var controllerField = typeof(BRCharacterMotor).GetField("controller",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(controllerField, Is.Not.Null);
            controllerField.SetValue(motor, controller);
            Physics.SyncTransforms();
            return (character, motor, controller, ground, obstacle, x);
        }

        private static bool InvokeTryStartVault(BRCharacterMotor motor)
        {
            var method = typeof(BRCharacterMotor).GetMethod("TryStartVault",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(motor, new object[] { Vector3.forward });
        }

        private static void DestroyVaultFixture((GameObject character, BRCharacterMotor motor,
            CharacterController controller, GameObject ground, GameObject obstacle, float x) fixture)
        {
            Object.DestroyImmediate(fixture.character);
            Object.DestroyImmediate(fixture.obstacle);
            Object.DestroyImmediate(fixture.ground);
        }

        private static BRParticipant CreateParticipant(string name, bool player)
        {
            return CreateParticipant(name, player, null);
        }

        private static (GameObject root, MatchManager manager, BRGameConfig config,
            BRParticipant reviver, BRParticipant target) CreateReviveManagerFixture(string name)
        {
            DeathLootContainer.ClearAll();
            var root = new GameObject($"{name} Manager");
            var manager = root.AddComponent<MatchManager>();
            var config = new BRGameConfig();
            typeof(MatchManager).GetField("config",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(manager, config);
            typeof(MatchManager).GetProperty("State")?.SetValue(manager, MatchState.Active);
            var reviver = CreateParticipant($"{name} Reviver", true, 80);
            var target = CreateParticipant($"{name} Target", false, 80);
            reviver.SetPhase(ParticipantPhase.Grounded);
            target.SetPhase(ParticipantPhase.Grounded);
            target.transform.position = Vector3.right * 2f;
            target.Health.ConfigureLifeCycle(24f, 35f, () => true);
            target.Health.ApplyDamage(1000f, false, null);
            var participants = ManagerParticipants(manager);
            participants.Add(reviver);
            participants.Add(target);
            return (root, manager, config, reviver, target);
        }

        private static void DestroyReviveManagerFixture((GameObject root, MatchManager manager,
            BRGameConfig config, BRParticipant reviver, BRParticipant target) fixture)
        {
            DeathLootContainer.ClearAll();
            DestroyParticipants(fixture.reviver, fixture.target);
            if (fixture.root != null) Object.DestroyImmediate(fixture.root);
        }

        private static System.Collections.Generic.List<BRParticipant> ManagerParticipants(MatchManager manager)
        {
            var field = typeof(MatchManager).GetField("participants",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (System.Collections.Generic.List<BRParticipant>)field.GetValue(manager);
        }

        private static int CountDeathLootOwnedBy(string ownerName)
        {
            var count = 0;
            foreach (var container in Object.FindObjectsByType<DeathLootContainer>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (container.OwnerName == ownerName) count++;
            return count;
        }

        private static LootPickup AddTestPickup(LootSpawner spawner, string name,
            System.Action<LootPickup> configure)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(spawner.transform);
            var pickup = obj.AddComponent<LootPickup>();
            configure(pickup);
            ((System.Collections.Generic.List<LootPickup>)spawner.Pickups).Add(pickup);
            return pickup;
        }

        private static void SetMagazine(WeaponSystem weaponSystem, WeaponDefinition weapon, int amount)
        {
            var field = typeof(WeaponSystem).GetField("magazines",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var magazines = (System.Collections.Generic.Dictionary<WeaponDefinition, int>)field.GetValue(weaponSystem);
            magazines[weapon] = amount;
        }

        private static int TotalAmmo(BRParticipant participant, LootPickup pickup, AmmoKind kind)
        {
            var total = participant.Inventory.AmmoFor(kind);
            foreach (var weapon in participant.Inventory.Weapons)
                if (weapon != null && weapon.ammoKind == kind) total += participant.Weapon.MagazineFor(weapon);
            if (pickup != null && pickup.Weapon != null && pickup.Weapon.ammoKind == kind)
                total += pickup.LoadedMagazine + pickup.ReserveBonus;
            return total;
        }

        private static LootPickup FindPickup(LootSpawner spawner, LootKind kind)
        {
            foreach (var pickup in spawner.Pickups)
                if (pickup != null && !pickup.Collected && pickup.Kind == kind) return pickup;
            return null;
        }

        private static int TotalSpawnerAmmo(BRParticipant participant, LootSpawner spawner, AmmoKind kind)
        {
            var total = participant.Inventory.AmmoFor(kind);
            foreach (var weapon in participant.Inventory.Weapons)
                if (weapon != null && weapon.ammoKind == kind) total += participant.Weapon.MagazineFor(weapon);
            foreach (var pickup in spawner.Pickups)
            {
                if (pickup == null || pickup.Collected) continue;
                if (pickup.Kind == LootKind.Ammo && pickup.AmmoKind == kind) total += pickup.Amount;
                else if (pickup.Kind == LootKind.Weapon && pickup.Weapon != null && pickup.Weapon.ammoKind == kind)
                    total += pickup.LoadedMagazine + pickup.ReserveBonus;
            }
            return total;
        }

        private static bool InvokePlayerReviveTick(MatchManager manager, bool holding, float deltaTime,
            BRParticipant reviver)
        {
            var method = typeof(MatchManager).GetMethod("TickPlayerRevive",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(bool), typeof(float), typeof(BRParticipant) }, null);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(manager, new object[] { holding, deltaTime, reviver });
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, arguments);
        }

        private static BRParticipant CreateParticipant(string name, bool player, int? teamId)
        {
            var obj = new GameObject(name);
            obj.AddComponent<CharacterController>();
            obj.AddComponent<BRCharacterMotor>();
            obj.AddComponent<HealthArmorSystem>();
            obj.AddComponent<InventorySystem>();
            obj.AddComponent<WeaponSystem>();
            var participant = obj.AddComponent<BRParticipant>();
            if (teamId.HasValue) participant.Configure(name, player, 0, teamId.Value);
            else participant.Configure(name, player, 0);
            participant.Health.InitializeFull();
            return participant;
        }

        private static void DestroyParticipants(params BRParticipant[] participants)
        {
            foreach (var participant in participants)
            {
                if (participant == null) continue;
                ParticipantRegistry.Unregister(participant);
                Object.DestroyImmediate(participant.gameObject);
            }
        }

        private static void DestroyWeaponFeedback()
        {
            foreach (var line in Object.FindObjectsByType<LineRenderer>())
                if (line.name == "Shot Tracer") Object.DestroyImmediate(line.gameObject);
            foreach (var marker in Object.FindObjectsByType<Renderer>())
                if (marker.name is "Pooled Impact Marker" or "Muzzle Flash") Object.DestroyImmediate(marker.gameObject);
        }

        private static MobileControlRects TestMobileControls(bool moveEnabled = true, bool fireEnabled = true,
            bool aimEnabled = true, bool jumpEnabled = true, bool interactEnabled = true,
            bool reloadEnabled = true, bool healEnabled = true, bool swapEnabled = true)
        {
            return new MobileControlRects(new Vector2(80f, 80f), 40f,
                new Rect(300f, 50f, 60f, 60f), new Rect(370f, 50f, 50f, 50f),
                new Rect(430f, 50f, 50f, 50f), new Rect(300f, 120f, 50f, 50f),
                new Rect(360f, 120f, 50f, 50f), new Rect(20f, 150f, 50f, 50f),
                new Rect(80f, 150f, 50f, 50f), 250f, moveEnabled, fireEnabled, aimEnabled,
                jumpEnabled, interactEnabled, reloadEnabled, healEnabled, swapEnabled);
        }

        private static void SetGamePreference(string propertyName, object value)
        {
            typeof(GamePreferences).GetProperty(propertyName).SetValue(null, value);
        }

        private static void AssertSettingsLayoutDoesNotOverlap(MatchSettingsRects layout)
        {
            var rows = new[] { layout.LookRow, layout.AdsRow, layout.FireDragRow, layout.FrameRateRow };
            var buttons = new[] { layout.EditHudButton, layout.PrimaryButton, layout.SecondaryButton };
            for (var i = 0; i < rows.Length; i++)
            {
                for (var j = i + 1; j < rows.Length; j++)
                    Assert.That(rows[i].Overlaps(rows[j]), Is.False, $"Settings rows {i} and {j} overlap");
                foreach (var button in buttons)
                {
                    if (button.width <= 0f || button.height <= 0f) continue;
                    Assert.That(rows[i].Overlaps(button), Is.False, $"Settings row {i} overlaps a bottom button");
                }
            }
        }

        private static void AssertSettingsRectsInside(Rect bounds, MatchSettingsRects layout)
        {
            var rects = new[]
            {
                layout.LookRow, layout.AdsRow, layout.FireDragRow, layout.FrameRateRow,
                layout.EditHudButton, layout.PrimaryButton, layout.SecondaryButton
            };
            foreach (var rect in rects)
            {
                if (rect.width <= 0f || rect.height <= 0f) continue;
                Assert.That(MatchAuxiliaryLayout.Contains(bounds, rect), Is.True,
                    $"Settings rect {rect} escaped bounds {bounds}");
            }
        }

        private static System.Collections.Generic.Dictionary<System.Reflection.FieldInfo, object>
            SnapshotStaticFields(System.Type type)
        {
            var fields = type.GetFields(System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var snapshot = new System.Collections.Generic.Dictionary<System.Reflection.FieldInfo, object>();
            foreach (var field in fields)
            {
                if (!field.IsLiteral && !field.IsInitOnly) snapshot[field] = field.GetValue(null);
            }
            return snapshot;
        }

        private static void RestoreStaticFields(
            System.Collections.Generic.Dictionary<System.Reflection.FieldInfo, object> snapshot)
        {
            foreach (var value in snapshot) value.Key.SetValue(null, value.Value);
        }
    }
}
