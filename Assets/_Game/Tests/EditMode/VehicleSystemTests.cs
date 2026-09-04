using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BattleRoyale.Tests
{
    public sealed class VehicleSystemTests
    {
        private const string PrefabPath = "Assets/_Game/Resources/Vehicles/Car/Vehicle_Car_Gameplay.prefab";

        [Test]
        public void GameplayPrefabPreservesExpectedComponentBoundaries()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<VehicleController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<VehicleSeatManager>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<VehicleInputAdapter>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<VehicleHealth>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<VehicleFuel>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true).Length, Is.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<WheelCollider>(true).Length, Is.EqualTo(4));
        }

        [Test]
        public void ImportedCarIsNormalizedToRealisticDimensions()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                Assert.That(bounds.size.x, Is.InRange(1.8f, 2.3f));
                Assert.That(bounds.size.y, Is.InRange(1.3f, 1.8f));
                Assert.That(bounds.size.z, Is.InRange(4.4f, 4.8f));
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.04f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void WheelCollidersHaveValidArcadeSetup()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var wheels = prefab.GetComponentsInChildren<WheelCollider>(true);

            Assert.That(wheels.Length, Is.EqualTo(4));
            foreach (var wheel in wheels)
            {
                Assert.That(wheel.radius, Is.InRange(0.32f, 0.48f));
                Assert.That(wheel.suspensionDistance, Is.EqualTo(0.24f).Within(0.001f));
                Assert.That(wheel.suspensionSpring.spring, Is.GreaterThan(20000f));
            }
        }

        [Test]
        public void PlayerCanEnterDriverSeatAndExitAgain()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var vehicle = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            var player = new GameObject("Vehicle Test Player");
            player.transform.position = new Vector3(0f, 0.2f, 2.8f);
            var participant = player.AddComponent<BRParticipant>();
            participant.Configure("Tester", true, 0, 0);
            try
            {
                var seats = vehicle.GetComponent<VehicleSeatManager>();
                Assert.That(seats.RequestEnterVehicle(participant), Is.True);
                Assert.That(seats.Driver, Is.EqualTo(participant));
                Assert.That(player.transform.IsChildOf(vehicle.transform), Is.True);
                Assert.That(player.GetComponent<CharacterController>().enabled, Is.False);
                Assert.That(seats.RequestExitVehicle(participant), Is.True);
                Assert.That(seats.Driver, Is.Null);
                Assert.That(player.transform.parent, Is.Null);
                Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(vehicle);
            }
        }

        [Test]
        public void VehicleFuelCanBeDisabledOrConsumed()
        {
            var root = new GameObject("Fuel Test");
            var fuel = root.AddComponent<VehicleFuel>();
            try
            {
                fuel.Configure(false, 50f);
                fuel.Consume(1f, 80f, 100f);
                Assert.That(fuel.CanDrive, Is.True);
                Assert.That(fuel.NormalizedFuel, Is.EqualTo(1f));
                fuel.Configure(true, 50f);
                fuel.Consume(1f, 80f, 10f);
                Assert.That(fuel.CurrentFuel, Is.LessThan(50f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void VehicleHealthTransitionsToCriticalWithoutAffectingPlayerHealthSystem()
        {
            var root = new GameObject("Vehicle Health Test");
            var health = root.AddComponent<VehicleHealth>();
            try
            {
                health.Configure(800f);
                var applied = health.ApplyDamage(650f, DamageType.Weapon, null);
                Assert.That(applied, Is.EqualTo(650f));
                Assert.That(health.State, Is.EqualTo(VehicleDamageState.Critical));
                Assert.That(health.CurrentHealth, Is.EqualTo(150f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
