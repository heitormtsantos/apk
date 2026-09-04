using BattleRoyale;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BattleRoyale.Editor
{
    public static class BattleRoyaleProjectBuilder
    {
        public static void ConfigureUrp()
        {
            System.IO.Directory.CreateDirectory("Assets/_Game/Settings");
            var assetPath = "Assets/_Game/Settings/MobileURP.asset";
            var rendererPath = "Assets/_Game/Settings/MobileURP_Renderer.asset";
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, rendererPath);
            }

            if (urpAsset == null)
            {
                urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urpAsset, assetPath);
            }
            else
            {
                var serialized = new SerializedObject(urpAsset);
                var rendererList = serialized.FindProperty("m_RendererDataList");
                rendererList.arraySize = 1;
                rendererList.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
                serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(urpAsset);
            }

            GraphicsSettings.defaultRenderPipeline = urpAsset;
            var previousQuality = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urpAsset;
            }
            QualitySettings.SetQualityLevel(Mathf.Clamp(previousQuality, 0, QualitySettings.names.Length - 1), false);
            AssetDatabase.SaveAssets();
            Debug.Log($"URP configured with {assetPath}.");
        }

        public static void BuildAll()
        {
            ConfigurePlayerSettings();
            ConfigureUrp();
            ImportedGameplayAssetBuilder.BuildWeaponPrefabs();
            System.IO.Directory.CreateDirectory("Assets/_Game/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("BattleRoyaleBootstrap");
            bootstrap.AddComponent<BattleRoyaleBootstrap>();
            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            camera.AddComponent<Camera>();
            camera.AddComponent<AudioListener>();
            var path = "Assets/_Game/Scenes/BattleRoyalePrototype.unity";
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Battle Royale project generated.");
        }

        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Raven Drop Studio";
            PlayerSettings.productName = "Raven Drop";
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ravendrop.prototype");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = 2;
        }

        public static void BuildWindowsDevelopment()
        {
            BuildAll();
            var output = System.IO.Path.GetFullPath("SmokeBuild/BattleRoyaleMobile.exe");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/BattleRoyalePrototype.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"Windows build failed: {report.summary.result}");
            Debug.Log($"Windows development build created at {output}");
        }

        public static void BuildAndroidDevelopment()
        {
            BuildAll();
            var output = System.IO.Path.GetFullPath("SmokeBuild/RavenDrop-Android.apk");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/BattleRoyalePrototype.unity" },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"Android build failed: {report.summary.result}");
            Debug.Log($"Android development build created at {output}");
        }

        public static void BuildAndroidRelease()
        {
            BuildAll();
            var output = System.IO.Path.GetFullPath("SmokeBuild/RavenDrop-Android.apk");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/BattleRoyalePrototype.unity" },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"Android release build failed: {report.summary.result}");
            Debug.Log($"Android release build created at {output}");
        }
    }
}
