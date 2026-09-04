using NUnit.Framework;
using UnityEngine;

namespace BattleRoyale.Tests
{
    public sealed class AircraftAssetTests
    {
        private GameObject instance;

        [TearDown]
        public void TearDown()
        {
            if (instance != null) Object.DestroyImmediate(instance);
        }

        [Test]
        public void ImportedCargoPlanePrefabHasNormalizedDimensionsAndNoColliders()
        {
            var prefab = Resources.Load<GameObject>("Aircraft/CargoPlane_GameplayVisual");
            Assert.That(prefab, Is.Not.Null);
            instance = Object.Instantiate(prefab);
            var bounds = RendererBounds(instance);
            Assert.That(Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z), Is.EqualTo(1f).Within(0.02f));
            Assert.That(bounds.size.x, Is.GreaterThan(bounds.size.y));
            Assert.That(bounds.size.z, Is.GreaterThan(bounds.size.y));
            Assert.That(instance.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void ImportedCargoPlaneUsesBaseColorNormalAndMetallicTextures()
        {
            var prefab = Resources.Load<GameObject>("Aircraft/CargoPlane_GameplayVisual");
            instance = Object.Instantiate(prefab);
            var renderer = instance.GetComponentInChildren<Renderer>(true);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.shader.name, Does.Contain("Universal Render Pipeline/Lit"));
            Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
            Assert.That(renderer.sharedMaterial.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(renderer.sharedMaterial.GetTexture("_MetallicGlossMap"), Is.Not.Null);
        }

        [Test]
        public void DeploymentPlaneRootFacesItsRoute()
        {
            instance = new GameObject("Drop Test");
            var drop = instance.AddComponent<DropSystem>();
            drop.Configure(new BRGameConfig());
            drop.BeginRoute();
            Assert.That(drop.PlaneForwardAlignment, Is.GreaterThan(0.999f));
            Assert.That(GameObject.Find("Imported Deployment Cargo Plane"), Is.Not.Null);
        }

        [Test]
        public void AirdropUsesImportedCargoPlane()
        {
            instance = new GameObject("Airdrop Test");
            var plane = instance.AddComponent<AirdropPlane>();
            plane.Configure(Vector3.zero, Vector3.forward * 100f, Vector3.forward * 50f, 30f, _ => { });
            Assert.That(instance.transform.Find("Imported Airdrop Cargo Plane"), Is.Not.Null);
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
