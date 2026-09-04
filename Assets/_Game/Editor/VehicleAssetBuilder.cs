using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BattleRoyale.Editor
{
    public static class VehicleAssetBuilder
    {
        private const string Root = "Assets/_Game/Resources/Vehicles/Car";
        private const string Source = Root + "/Source";
        private const string ModelPath = Source + "/BattleRoyaleCar.fbx";
        private const string MaterialPath = Root + "/Car_URP.mat";
        private const string PrefabPath = Root + "/Vehicle_Car_Gameplay.prefab";
        private const string DefinitionPath = Root + "/Vehicle_Car.asset";

        [MenuItem("Raven Drop/Vehicles/Rebuild Car Gameplay Prefab")]
        public static void BuildFromCommandLine()
        {
            ConfigureTextures();
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Vehicle FBX was not imported.");
            var material = BuildMaterial();
            var root = BuildPrefabRoot(model, material, out var report);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            BuildDefinition(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report);
            Debug.Log($"VEHICLE_BUILD=PASS;PREFAB={PrefabPath};DEFINITION={DefinitionPath}");
        }

        private static GameObject BuildPrefabRoot(GameObject sourceModel, Material material, out string report)
        {
            var root = new GameObject("Vehicle_Car_Gameplay");
            var modelRoot = Child(root.transform, "Model");
            var orientation = Child(modelRoot, "ModelRotationFix");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(sourceModel, orientation);
            model.name = "ExistingCarModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var rawBounds = RendererBounds(model);
            var longestAxis = rawBounds.size.x >= rawBounds.size.y && rawBounds.size.x >= rawBounds.size.z ? 0
                : rawBounds.size.y >= rawBounds.size.z ? 1 : 2;
            orientation.localRotation = longestAxis switch
            {
                0 => Quaternion.Euler(0f, -90f, 0f),
                1 => Quaternion.Euler(-90f, 0f, 0f),
                _ => Quaternion.identity
            };
            var oriented = RendererBounds(modelRoot.gameObject);
            var rawLength = Mathf.Max(oriented.size.x, oriented.size.z);
            var normalization = rawLength > 0.001f ? 4.6f / rawLength : 1f;
            orientation.localScale = Vector3.one * normalization;
            var normalized = RendererBounds(modelRoot.gameObject);
            orientation.position += new Vector3(-normalized.center.x, -normalized.min.y, -normalized.center.z);
            var bounds = RendererBounds(modelRoot.gameObject);

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1350f;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.85f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var colliders = Child(root.transform, "Colliders");
            var bodyCollider = colliders.gameObject.AddComponent<BoxCollider>();
            bodyCollider.center = new Vector3(0f, bounds.size.y * 0.46f, 0f);
            bodyCollider.size = new Vector3(bounds.size.x * 0.88f,
                Mathf.Max(0.65f, bounds.size.y * 0.72f), bounds.size.z * 0.9f);

            var centerOfMass = Child(root.transform, "CenterOfMass");
            centerOfMass.localPosition = new Vector3(0f, Mathf.Max(0.25f, bounds.size.y * 0.28f), 0f);
            var wheelVisuals = FindWheelVisuals(modelRoot.gameObject);
            var wheels = BuildWheels(root.transform, bounds, wheelVisuals);

            var controller = root.AddComponent<VehicleController>();
            var fuel = root.AddComponent<VehicleFuel>();
            fuel.Configure(false, 100f);
            var health = root.AddComponent<VehicleHealth>();
            health.Configure(850f);
            root.AddComponent<VehicleCollisionDamage>();
            var seatManager = root.AddComponent<VehicleSeatManager>();
            var cameraController = root.AddComponent<VehicleCameraController>();
            var audio = root.AddComponent<VehicleAudio>();
            var input = root.AddComponent<VehicleInputAdapter>();
            controller.Configure(null, body, centerOfMass, wheels);

            var exits = Child(root.transform, "ExitPoints");
            var driverExit = Point(exits, "DriverExitLeft", new Vector3(-bounds.extents.x - 0.65f, 0.15f, 0.35f));
            var passengerExit = Point(exits, "PassengerExitRight", new Vector3(bounds.extents.x + 0.65f, 0.15f, 0.35f));
            var rearLeftExit = Point(exits, "RearExitLeft", new Vector3(-bounds.extents.x - 0.65f, 0.15f, -0.65f));
            var rearRightExit = Point(exits, "RearExitRight", new Vector3(bounds.extents.x + 0.65f, 0.15f, -0.65f));
            var seatsRoot = Child(root.transform, "Seats");
            var seatY = Mathf.Clamp(bounds.size.y * 0.48f, 0.65f, 1.05f);
            var seatX = bounds.extents.x * 0.32f;
            var seats = new[]
            {
                new VehicleSeat("driver", Point(seatsRoot, "DriverSeat", new Vector3(-seatX, seatY, 0.42f)),
                    new[] { driverExit, rearLeftExit }, true),
                new VehicleSeat("passenger_front", Point(seatsRoot, "PassengerFront", new Vector3(seatX, seatY, 0.42f)),
                    new[] { passengerExit, rearRightExit }, false),
                new VehicleSeat("passenger_rear_left", Point(seatsRoot, "PassengerRearLeft", new Vector3(-seatX, seatY, -0.58f)),
                    new[] { rearLeftExit, driverExit }, false),
                new VehicleSeat("passenger_rear_right", Point(seatsRoot, "PassengerRearRight", new Vector3(seatX, seatY, -0.58f)),
                    new[] { rearRightExit, passengerExit }, false)
            };
            seatManager.Configure(controller, seats);

            var cameras = Child(root.transform, "CameraPoints");
            var chase = Point(cameras, "ThirdPersonTarget", new Vector3(0f, 0.35f, -0.25f));
            var close = Point(cameras, "CloseCameraTarget", new Vector3(0f, 0.55f, 0.55f));
            cameraController.Configure(chase, close);
            input.Configure(controller, seatManager, audio, cameraController);
            Child(root.transform, "Effects");

            var renderers = model.GetComponentsInChildren<Renderer>(true).Length;
            var meshes = model.GetComponentsInChildren<MeshFilter>(true).Length;
            var animator = model.GetComponentInChildren<Animator>(true) != null;
            var lod = model.GetComponentInChildren<LODGroup>(true) != null;
            report = $"VEHICLE_ANALYSIS=MODEL={sourceModel.name};FORMAT=FBX;RAW_SIZE={rawBounds.size:F3};"
                + $"NORMALIZED_SIZE={bounds.size:F3};SCALE={normalization:F6};FORWARD=+Z;"
                + $"MESHES={meshes};RENDERERS={renderers};WHEEL_OBJECTS={wheelVisuals.Count};"
                + $"ANIMATOR={animator};LOD={lod};MATERIAL={material.name}";
            return root;
        }

        private static VehicleWheel[] BuildWheels(Transform root, Bounds bounds, List<Transform> visuals)
        {
            var wheelsRoot = Child(root, "Wheels");
            var radius = Mathf.Clamp(bounds.size.y * 0.27f, 0.32f, 0.48f);
            var x = bounds.extents.x * 0.78f;
            var frontZ = bounds.extents.z * 0.62f;
            var rearZ = -bounds.extents.z * 0.62f;
            var positions = new[]
            {
                new Vector3(-x, radius, frontZ), new Vector3(x, radius, frontZ),
                new Vector3(-x, radius, rearZ), new Vector3(x, radius, rearZ)
            };
            var names = new[] { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            var output = new VehicleWheel[4];
            for (var i = 0; i < output.Length; i++)
            {
                var wheelRoot = Child(wheelsRoot, names[i]);
                wheelRoot.localPosition = positions[i];
                var colliderObject = Child(wheelRoot, "WheelCollider");
                var collider = colliderObject.gameObject.AddComponent<WheelCollider>();
                collider.radius = radius;
                collider.mass = 28f;
                collider.suspensionDistance = 0.24f;
                collider.wheelDampingRate = 0.35f;
                collider.suspensionSpring = new JointSpring { spring = 32000f, damper = 4800f, targetPosition = 0.5f };
                collider.forwardFriction = Friction(1.55f, 1.25f);
                collider.sidewaysFriction = Friction(1.4f, 1.1f);
                var component = wheelRoot.gameObject.AddComponent<VehicleWheel>();
                component.Configure(collider, visuals.Count >= 4 ? ClosestVisual(visuals, wheelRoot.position) : null,
                    i < 2, true, i >= 2);
                output[i] = component;
            }
            return output;
        }

        private static WheelFrictionCurve Friction(float extremum, float asymptote) => new()
        {
            extremumSlip = 0.35f,
            extremumValue = extremum,
            asymptoteSlip = 0.8f,
            asymptoteValue = asymptote,
            stiffness = 1f
        };

        private static Transform ClosestVisual(List<Transform> visuals, Vector3 position) =>
            visuals.OrderBy(value => (value.position - position).sqrMagnitude).FirstOrDefault();

        private static List<Transform> FindWheelVisuals(GameObject model)
        {
            var tokens = new[] { "wheel", "tire", "tyre", "roda", "pneu" };
            return model.GetComponentsInChildren<Transform>(true)
                .Where(value => tokens.Any(token => value.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                .Where(value => value.GetComponent<Renderer>() != null || value.GetComponentInChildren<Renderer>() != null)
                .Distinct().ToList();
        }

        private static Bounds RendererBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, new Vector3(2f, 1.5f, 4.6f));
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static Material BuildMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Car URP" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Car_BaseColor.jpg"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Car_Normal.jpg"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Car_Metallic.jpg"));
            material.SetFloat("_Metallic", 0.65f);
            material.SetFloat("_Smoothness", 0.42f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTextures()
        {
            foreach (var file in new[] { "Car_BaseColor.jpg", "Car_Normal.jpg", "Car_Metallic.jpg",
                         "Car_Roughness.jpg", "Car_RM.jpg" })
            {
                var path = Source + "/" + file;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.mipmapEnabled = true;
                importer.sRGBTexture = file == "Car_BaseColor.jpg";
                importer.textureType = file == "Car_Normal.jpg"
                    ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.SaveAndReimport();
            }
        }

        private static void BuildDefinition(GameObject prefab)
        {
            var definition = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<VehicleDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("vehicleId").stringValue = "raven_runner_01";
            serialized.FindProperty("displayName").stringValue = "Raven Runner";
            serialized.FindProperty("gameplayPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("maxHealth").floatValue = 850f;
            serialized.FindProperty("mass").floatValue = 1350f;
            serialized.FindProperty("maxForwardSpeed").floatValue = 36f;
            serialized.FindProperty("maxReverseSpeed").floatValue = 12f;
            serialized.FindProperty("motorTorque").floatValue = 1750f;
            serialized.FindProperty("brakeTorque").floatValue = 2800f;
            serialized.FindProperty("maxSteerAngle").floatValue = 33f;
            serialized.FindProperty("fuelCapacity").floatValue = 100f;
            serialized.FindProperty("seatCount").intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static Transform Child(Transform parent, string name)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
        }

        private static Transform Point(Transform parent, string name, Vector3 localPosition)
        {
            var value = Child(parent, name);
            value.localPosition = localPosition;
            return value;
        }
    }
}
