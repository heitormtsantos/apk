using UnityEngine;
using UnityEngine.Rendering;

namespace BattleRoyale
{
    public sealed class SafeZoneWorldVisual : MonoBehaviour
    {
        private SafeZoneController controller;
        private SafeZoneConfig config;
        private MeshFilter wallFilter;
        private MeshRenderer wallRenderer;
        private LineRenderer currentRing;
        private LineRenderer nextRing;
        private Material runtimeWallMaterial;
        private int builtSegments;

        public void Configure(SafeZoneController owner, SafeZoneConfig settings)
        {
            controller = owner;
            config = settings;
            EnsureVisuals();
            Refresh();
        }

        public void Refresh()
        {
            if (controller == null || config == null) return;
            EnsureVisuals();
            var visible = controller.CurrentState != SafeZoneState.Inactive && controller.CurrentRadius > 0f;
            wallRenderer.enabled = visible;
            currentRing.enabled = visible;
            nextRing.enabled = visible && controller.HasNextZone;
            if (!visible) return;

            var center = controller.CurrentCenter;
            transform.position = new Vector3(center.x, config.wallBottomOffset, center.z);
            wallFilter.transform.localScale = new Vector3(controller.CurrentRadius, config.wallHeight,
                controller.CurrentRadius);
            UpdateRing(currentRing, controller.CurrentCenter, controller.CurrentRadius, 0.09f);
            if (controller.HasNextZone)
                UpdateRing(nextRing, controller.NextCenter, controller.NextRadius, 0.07f);

            if (runtimeWallMaterial != null && runtimeWallMaterial.HasProperty("_BaseColor"))
            {
                var pulse = 1f + Mathf.Sin(Time.time * (controller.Shrinking ? 5f : 2f)) * config.pulseIntensity;
                var color = config.wallColor;
                color.a = Mathf.Min(0.035f, Mathf.Clamp01(color.a * pulse));
                runtimeWallMaterial.SetColor("_BaseColor", color);
                if (runtimeWallMaterial.HasProperty("_EmissionColor"))
                    runtimeWallMaterial.SetColor("_EmissionColor",
                        new Color(color.r, color.g, color.b) * color.a * pulse * 0.5f);
            }
        }

        private void EnsureVisuals()
        {
            if (wallFilter == null)
            {
                var wall = new GameObject("Energy Cylinder");
                wall.transform.SetParent(transform, false);
                wallFilter = wall.AddComponent<MeshFilter>();
                wallRenderer = wall.AddComponent<MeshRenderer>();
                wallRenderer.shadowCastingMode = ShadowCastingMode.Off;
                wallRenderer.receiveShadows = false;
            }
            if (builtSegments != config.wallSegments || wallFilter.sharedMesh == null)
            {
                if (wallFilter.sharedMesh != null) Destroy(wallFilter.sharedMesh);
                wallFilter.sharedMesh = BuildOpenCylinder(config.wallSegments);
                builtSegments = config.wallSegments;
            }
            if (wallRenderer.sharedMaterial == null)
            {
                runtimeWallMaterial = config.zoneMaterial != null
                    ? new Material(config.zoneMaterial)
                    : CreateWallMaterial(config.wallColor);
                runtimeWallMaterial.name = "Safe Zone Energy (Runtime)";
                ConfigureTransparent(runtimeWallMaterial, config.wallColor);
                wallRenderer.sharedMaterial = runtimeWallMaterial;
            }
            currentRing ??= CreateRing("Current Zone Ground Ring", new Color(0.05f, 0.85f, 1f, 0.86f));
            nextRing ??= CreateRing("Next Zone Ground Ring", new Color(1f, 0.76f, 0.05f, 0.78f));
        }

        private LineRenderer CreateRing(string objectName, Color color)
        {
            var ringObject = new GameObject(objectName);
            ringObject.transform.SetParent(transform.parent != null ? transform.parent : transform, true);
            var ring = ringObject.AddComponent<LineRenderer>();
            ring.loop = true;
            ring.useWorldSpace = true;
            ring.positionCount = 96;
            ring.widthMultiplier = 0.08f;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.sharedMaterial = BRMaterialFactory.CreateEmissive(objectName, color, 1.6f);
            return ring;
        }

        private static void UpdateRing(LineRenderer ring, Vector3 center, float radius, float height)
        {
            for (var i = 0; i < ring.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, height,
                    Mathf.Sin(angle) * radius));
            }
        }

        private static Mesh BuildOpenCylinder(int segmentCount)
        {
            var segments = Mathf.Clamp(segmentCount, 16, 128);
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (var i = 0; i <= segments; i++)
            {
                var ratio = i / (float)segments;
                var angle = ratio * Mathf.PI * 2f;
                var x = Mathf.Cos(angle);
                var z = Mathf.Sin(angle);
                vertices[i * 2] = new Vector3(x, 0f, z);
                vertices[i * 2 + 1] = new Vector3(x, 1f, z);
                uv[i * 2] = new Vector2(ratio, 0f);
                uv[i * 2 + 1] = new Vector2(ratio, 1f);
                if (i == segments) continue;
                var triangle = i * 6;
                var vertex = i * 2;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex + 2;
                triangles[triangle + 4] = vertex + 1;
                triangles[triangle + 5] = vertex + 3;
            }
            var mesh = new Mesh { name = $"Safe Zone Cylinder {segments}" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateWallMaterial(Color color)
        {
            var material = BRMaterialFactory.CreateEmissive("Safe Zone Energy", color, 2f);
            ConfigureTransparent(material, color);
            return material;
        }

        private static void ConfigureTransparent(Material material, Color color)
        {
            if (material == null) return;
            color.a = Mathf.Min(color.a, 0.035f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("DepthNormalsOnly", false);
        }

        private void OnDestroy()
        {
            if (wallFilter != null && wallFilter.sharedMesh != null) Destroy(wallFilter.sharedMesh);
            if (runtimeWallMaterial != null) Destroy(runtimeWallMaterial);
            if (currentRing != null && currentRing.sharedMaterial != null) Destroy(currentRing.sharedMaterial);
            if (nextRing != null && nextRing.sharedMaterial != null) Destroy(nextRing.sharedMaterial);
        }
    }
}
