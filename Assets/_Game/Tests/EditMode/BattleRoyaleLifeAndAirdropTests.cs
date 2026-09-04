using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BattleRoyale.Tests
{
    public sealed class BattleRoyaleLifeAndAirdropTests
    {
        [TearDown]
        public void TearDown() => DeathLootContainer.ClearAll();

        [Test]
        public void LifeStateMovesFromAliveToKnockedRevivedAndDead()
        {
            var root = new GameObject("Life State Test");
            try
            {
                var health = root.AddComponent<HealthArmorSystem>();
                health.ConfigureLifeCycle(20f, 35f, () => true);

                health.ApplyDamage(1000f, false, null);
                Assert.That(health.LifeState, Is.EqualTo(PlayerLifeState.Knocked));
                Assert.That(health.KnockdownCount, Is.EqualTo(1));

                health.Revive();
                Assert.That(health.LifeState, Is.EqualTo(PlayerLifeState.Alive));
                health.Eliminate();
                Assert.That(health.LifeState, Is.EqualTo(PlayerLifeState.Dead));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RepeatedKnockdownShortensBleedout()
        {
            var root = new GameObject("Repeated Knock Test");
            var config = BattleRoyaleLifeConfig.CreateRuntimeDefault();
            try
            {
                var health = root.AddComponent<HealthArmorSystem>();
                health.ConfigureLifeCycle(20f, 35f, () => true);
                health.ConfigureKnockdownRules(config);
                health.ApplyDamage(1000f, false, null);
                var first = health.BleedoutRemaining;
                health.Revive();
                typeof(HealthArmorSystem).GetField("invulnerableUntil",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(health, -1f);
                health.ApplyDamage(1000f, false, null);

                Assert.That(health.KnockdownCount, Is.EqualTo(2));
                Assert.That(health.BleedoutRemaining, Is.LessThan(first));
                Assert.That(health.BleedoutRemaining, Is.GreaterThanOrEqualTo(config.MinimumBleedoutTime));
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LevelFourArmorIsAcceptedAndHasStrongestProtection()
        {
            var root = new GameObject("Armor Level Four Test");
            try
            {
                var health = root.AddComponent<HealthArmorSystem>();
                health.EquipVest(4);
                health.EquipHelmet(4);

                Assert.That(health.VestLevel, Is.EqualTo(4));
                Assert.That(health.HelmetLevel, Is.EqualTo(4));
                Assert.That(HealthArmorSystem.VestMultiplier(4),
                    Is.LessThan(HealthArmorSystem.VestMultiplier(3)));
                Assert.That(HealthArmorSystem.HelmetMultiplier(4),
                    Is.LessThan(HealthArmorSystem.HelmetMultiplier(3)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AirdropPayloadContainsRareArmorHealingAndGlooWalls()
        {
            var container = DeathLootContainer.CreateAirdrop(Vector3.zero,
                new List<WeaponDefinition>(), 17, 120f);

            Assert.That(container.OwnerName, Is.EqualTo("Airdrop"));
            Assert.That(container.Entries, Has.Some.Matches<DeathLootEntry>(entry =>
                entry.Kind == DeathLootKind.Vest && entry.Level == 4));
            Assert.That(container.Entries, Has.Some.Matches<DeathLootEntry>(entry =>
                entry.Kind == DeathLootKind.MedKit && entry.Amount == 2));
            Assert.That(container.Entries, Has.Some.Matches<DeathLootEntry>(entry =>
                entry.Kind == DeathLootKind.GlooWall && entry.Amount == 3));
        }

        [Test]
        public void PlaneProgressOnlyReleasesAtOrAfterTarget()
        {
            Assert.That(AirdropPlane.HasReachedDropPoint(0.49f, 0.5f), Is.False);
            Assert.That(AirdropPlane.HasReachedDropPoint(0.5f, 0.5f), Is.True);
            Assert.That(AirdropPlane.HasReachedDropPoint(0.75f, 0.5f), Is.True);
        }

        [Test]
        public void AuthorityGatewayRejectsDuplicateDamageRequest()
        {
            var managerObject = new GameObject("Authority Manager");
            BRParticipant attacker = null;
            BRParticipant target = null;
            try
            {
                var manager = managerObject.AddComponent<MatchManager>();
                var authority = managerObject.AddComponent<BattleRoyaleAuthorityGateway>();
                typeof(MatchManager).GetProperty("State")?.SetValue(manager, MatchState.Active);
                attacker = CreateParticipant("Attacker", 1);
                target = CreateParticipant("Target", 2);

                var first = authority.RequestDamage(44, attacker, target, 10f, false);
                var healthAfterFirst = target.Health.Health;
                var duplicate = authority.RequestDamage(44, attacker, target, 10f, false);

                Assert.That(first.Accepted, Is.True);
                Assert.That(duplicate.Status, Is.EqualTo(AuthorityRequestStatus.Duplicate));
                Assert.That(target.Health.Health, Is.EqualTo(healthAfterFirst));
            }
            finally
            {
                if (attacker != null) Object.DestroyImmediate(attacker.gameObject);
                if (target != null) Object.DestroyImmediate(target.gameObject);
                Object.DestroyImmediate(managerObject);
            }
        }

        private static BRParticipant CreateParticipant(string displayName, int teamId)
        {
            var root = new GameObject(displayName);
            var participant = root.AddComponent<BRParticipant>();
            participant.Configure(displayName, false, teamId, teamId);
            participant.SetPhase(ParticipantPhase.Grounded);
            participant.Health.InitializeFull();
            return participant;
        }
    }
}
