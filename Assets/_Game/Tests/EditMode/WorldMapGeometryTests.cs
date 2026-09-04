using NUnit.Framework;
using UnityEngine;

namespace BattleRoyale.Tests
{
    public sealed class WorldMapGeometryTests
    {
        [TestCase("NeoParadise_Terrain_0/Object", WorldSurfaceRole.Terrain)]
        [TestCase("NeoParadise_Terrain_Road_02_UC_0/Object", WorldSurfaceRole.Road)]
        [TestCase("NeoParadise_Building_LOD4_1/Object", WorldSurfaceRole.Building)]
        [TestCase("NeoParadise_RockWall_0/Object", WorldSurfaceRole.Rock)]
        [TestCase("Stone_Bridge004_LOD0_0/Object", WorldSurfaceRole.Bridge)]
        [TestCase("Water_NeoParadise_1/Object", WorldSurfaceRole.Water)]
        public void BermudaHierarchyUsesStableSurfaceRoles(string path, WorldSurfaceRole expected)
            => Assert.That(WorldMapGeometry.Classify(path), Is.EqualTo(expected));

        [Test]
        public void WaterIsNeverCollidableOrPartOfPlayableBounds()
        {
            Assert.That(WorldMapGeometry.IsCollidable("Water_NeoParadise_1/Object"), Is.False);
            Assert.That(WorldMapGeometry.IsPlayableBoundsSurface("Water_NeoParadise_1/Object"), Is.False);
            Assert.That(WorldMapGeometry.IsCollidable("NeoParadise_Terrain_0/Object"), Is.True);
        }

        [TestCase(180f, 180f, 20, 200)]
        [TestCase(2032f, 2032f, 20, 320)]
        public void LootDensityScalesWithMapAndHasMobileCap(float width, float depth,
            int participants, int expected)
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(width, 100f, depth));
            Assert.That(WorldMapGeometry.RecommendedLootCount(bounds, participants), Is.EqualTo(expected));
        }

        [TestCase(180f, 3)]
        [TestCase(2032f, 7)]
        [TestCase(4000f, 8)]
        public void ShopCountScalesWithinSupportedRange(float size, int expected)
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(size, 100f, size));
            Assert.That(WorldMapGeometry.RecommendedShopCount(bounds), Is.EqualTo(expected));
        }

        [Test]
        public void BermudaScaleCompensatesImportedRootTransform()
        {
            var imported = new Bounds(Vector3.zero, new Vector3(142.493f, 8.6f, 142.493f));
            var scale = WorldMapGeometry.RequiredUniformScale(imported);
            Assert.That(imported.size.x * scale, Is.EqualTo(WorldMapGeometry.BermudaWorldDiameter).Within(0.01f));
            Assert.That(scale, Is.InRange(14f, 15f));
        }

        [Test]
        public void SafeZoneScalesAllRadiiToPlayableArea()
        {
            var config = ScriptableObject.CreateInstance<SafeZoneConfig>();
            config.initialRadius = 80f;
            config.minimumFinalRadius = 2f;
            config.phases.Add(new SafeZonePhaseData("First", 10f, 20f, 40f, 1f));

            config.AdaptToPlayableArea(new Bounds(Vector3.zero, new Vector3(2000f, 120f, 1600f)));

            Assert.That(config.initialRadius, Is.EqualTo(768f).Within(0.01f));
            Assert.That(config.phases[0].targetRadius, Is.EqualTo(384f).Within(0.01f));
            Assert.That(config.validateNextZoneGround, Is.True);
            Assert.That(config.allowZoneOverWater, Is.False);
            Object.DestroyImmediate(config);
        }
    }
}
