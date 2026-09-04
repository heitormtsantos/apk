using NUnit.Framework;
using UnityEngine;

namespace BattleRoyale.Tests
{
    public sealed class SafeZoneSystemTests
    {
        [Test]
        public void GeneratedZoneIsDeterministicAndContained()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(500f, 20f, 500f));
            var request = new SafeZoneGenerationRequest(new Vector3(12f, 0f, -7f), 100f, 45f,
                1f, 7521, 20, bounds, true);

            var first = SafeZoneGenerator.GenerateNextZone(request);
            var second = SafeZoneGenerator.GenerateNextZone(request);

            Assert.That(second.Center, Is.EqualTo(first.Center));
            Assert.That(second.Radius, Is.EqualTo(first.Radius));
            Assert.That(SafeZoneGenerator.ContainsCircle(request.Center, request.CurrentRadius,
                first.Center, first.Radius, 0.001f), Is.True);
        }

        [Test]
        public void WaitingThenShrinkingEndsExactlyAtNextZone()
        {
            var setup = CreateZone(1f, 2f, 40f);
            try
            {
                Assert.That(setup.zone.CurrentState, Is.EqualTo(SafeZoneState.Waiting));
                var expectedCenter = setup.zone.NextCenter;
                var expectedRadius = setup.zone.NextRadius;

                setup.zone.Tick(null, 1f, null);
                Assert.That(setup.zone.CurrentState, Is.EqualTo(SafeZoneState.Shrinking));
                setup.zone.Tick(null, 2f, null);

                Assert.That(setup.zone.CurrentState, Is.EqualTo(SafeZoneState.Finished));
                Assert.That(setup.zone.CurrentCenter, Is.EqualTo(expectedCenter));
                Assert.That(setup.zone.CurrentRadius, Is.EqualTo(expectedRadius).Within(0.0001f));
                Assert.That(setup.zone.HasNextZone, Is.False);
            }
            finally
            {
                DestroySetup(setup);
            }
        }

        [Test]
        public void ShrinkUsesFixedStartGeometryAtHalfTime()
        {
            var setup = CreateZone(0f, 4f, 40f);
            try
            {
                setup.zone.Tick(null, 0.01f, null);
                var startCenter = setup.zone.ShrinkStartCenter;
                var targetCenter = setup.zone.NextCenter;
                setup.zone.Tick(null, 1.99f, null);

                Assert.That(Vector3.Distance(setup.zone.CurrentCenter,
                    Vector3.Lerp(startCenter, targetCenter, 0.5f)), Is.LessThan(0.001f));
                Assert.That(setup.zone.CurrentRadius, Is.EqualTo(70f).Within(0.02f));
            }
            finally
            {
                DestroySetup(setup);
            }
        }

        [Test]
        public void ContainmentIgnoresHeightAndIncludesBoundaryTolerance()
        {
            var setup = CreateZone(10f, 10f, 40f);
            try
            {
                var center = setup.zone.CurrentCenter;
                Assert.That(setup.zone.IsInsideSafeZone(center + new Vector3(100.05f, 900f, 0f)), Is.True);
                Assert.That(setup.zone.IsInsideSafeZone(center + new Vector3(100.2f, -900f, 0f)), Is.False);
                Assert.That(setup.zone.GetDistanceToSafeZone(center + Vector3.right * 125f),
                    Is.EqualTo(25f).Within(0.001f));
                Assert.That(setup.zone.GetDirectionToSafeZone(center + Vector3.right * 125f),
                    Is.EqualTo(Vector3.left));
            }
            finally
            {
                DestroySetup(setup);
            }
        }

        [Test]
        public void SnapshotReconstructsAuthoritativeShrinkAtSynchronizedTime()
        {
            var setup = CreateZone(1f, 8f, 20f);
            var receiverObject = new GameObject("Snapshot Receiver");
            var receiverConfig = Object.Instantiate(setup.config);
            try
            {
                setup.zone.Tick(null, 1f, null);
                var snapshot = setup.zone.CreateSnapshot();
                var receiver = receiverObject.AddComponent<SafeZoneController>();
                receiver.Configure(receiverConfig, 3, false);
                receiver.ApplySnapshot(snapshot, snapshot.stateStartTime + 4d);

                Assert.That(receiver.CurrentState, Is.EqualTo(SafeZoneState.Shrinking));
                Assert.That(receiver.StateProgress, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(receiver.CurrentRadius,
                    Is.EqualTo(Mathf.Lerp(snapshot.startRadius, snapshot.targetRadius, 0.5f)).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(receiverObject);
                Object.DestroyImmediate(receiverConfig);
                DestroySetup(setup);
            }
        }

        [Test]
        public void StopMakesZoneInactiveAndPreventsClockProgress()
        {
            var setup = CreateZone(5f, 5f, 40f);
            try
            {
                setup.zone.StopSafeZone();
                setup.zone.Tick(null, 100f, null);
                Assert.That(setup.zone.CurrentState, Is.EqualTo(SafeZoneState.Inactive));
                Assert.That(setup.zone.StateTimeRemaining, Is.Zero);
                Assert.That(setup.zone.HasNextZone, Is.False);
            }
            finally
            {
                DestroySetup(setup);
            }
        }

        private static (GameObject gameObject, SafeZoneController zone, SafeZoneConfig config) CreateZone(
            float waiting, float shrinking, float targetRadius)
        {
            var config = ScriptableObject.CreateInstance<SafeZoneConfig>();
            config.initialRadius = 100f;
            config.useCustomInitialCenter = true;
            config.initialCenter = Vector3.zero;
            config.minimumFinalRadius = 1f;
            config.boundaryTolerance = 0.1f;
            config.phases.Add(new SafeZonePhaseData("Test", waiting, shrinking, targetRadius, 5f));
            var gameObject = new GameObject("Safe Zone Test");
            var zone = gameObject.AddComponent<SafeZoneController>();
            zone.Configure(config, 1234, true);
            return (gameObject, zone, config);
        }

        private static void DestroySetup((GameObject gameObject, SafeZoneController zone, SafeZoneConfig config) setup)
        {
            Object.DestroyImmediate(setup.gameObject);
            Object.DestroyImmediate(setup.config);
        }
    }
}
