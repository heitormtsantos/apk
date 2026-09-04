#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class BackpackRuntimeSmokeCapture : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var match = GetComponent<MatchManager>();
            yield return null;
            match?.StartMatch();
            var deadline = Time.realtimeSinceStartup + 90f;
            while (match != null && match.State != MatchState.Active && Time.realtimeSinceStartup < deadline)
            {
                if (match.State == MatchState.Plane && match.Player != null)
                {
                    match.Drop.TickPlane(match.Participants, match.Player, true);
                    if (match.Player.Phase is ParticipantPhase.Freefall or ParticipantPhase.Parachute)
                    {
                        if (PlayableArea.TryFindOpenGround(PlayableArea.Center, out var ground))
                            match.Player.Motor.Teleport(ground + Vector3.up * 0.12f);
                        match.Player.SetPhase(ParticipantPhase.Grounded);
                    }
                }
                yield return null;
            }
            if (match == null || match.State != MatchState.Active || match.Player == null)
            {
                Finish(false, "MATCH_NOT_ACTIVE");
                yield break;
            }

            var level1 = Resources.Load<BackpackData>("Backpacks/Data/Backpack_Lv1");
            var level2 = Resources.Load<BackpackData>("Backpacks/Data/Backpack_Lv2");
            var pickup1 = CreatePickup(match.Player.transform.position + Vector3.forward, level1);
            var first = pickup1.CollectQuantitative(match.Player);
            yield return null;
            var pickup2 = CreatePickup(match.Player.transform.position + Vector3.right, level2);
            var second = pickup2.CollectQuantitative(match.Player);
            yield return new WaitForSeconds(0.3f);

            var lower = CreatePickup(match.Player.transform.position - Vector3.right, level1);
            var downgrade = lower.CollectQuantitative(match.Player);
            var opened = match.OpenInventory();
            var unpaused = Mathf.Approximately(Time.timeScale, 1f);
            match.CloseInventory();
            var renderers = match.Player.Backpack.Socket.GetComponentsInChildren<Renderer>(true);
            var singleVisual = match.Player.Backpack.Socket.childCount == 1;
            if (Camera.main != null)
            {
                var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude > 0.001f) match.Player.transform.rotation = Quaternion.LookRotation(forward);
            }
            yield return new WaitForSeconds(0.2f);
            CaptureScreenshot();
            Finish(first.Success && second.Success && !downgrade.Success
                && match.Player.Backpack.CurrentLevel == 2 && renderers.Length > 0
                && singleVisual && opened && unpaused,
                $"LEVEL={match.Player.Backpack.CurrentLevel};RENDERERS={renderers.Length};SINGLE={singleVisual};"
                + $"DOWNGRADE_REJECTED={!downgrade.Success};UNPAUSED={unpaused}");
        }

        private static LootPickup CreatePickup(Vector3 position, BackpackData data)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = $"Smoke {data?.displayName}";
            root.transform.position = position;
            Destroy(root.GetComponent<Collider>());
            var pickup = root.AddComponent<LootPickup>();
            pickup.ConfigureBackpack(data);
            return pickup;
        }

        private static void CaptureScreenshot()
        {
            var path = CommandLineValue("-backpackScreenshot");
            var camera = Camera.main;
            if (string.IsNullOrWhiteSpace(path) || camera == null) return;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
            image.Apply(false, false);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Destroy(image);
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
        }

        private static string CommandLineValue(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return string.Empty;
        }

        private static void Finish(bool passed, string detail)
        {
            Debug.Log($"BR_BACKPACK_SMOKE={(passed ? "PASS" : "FAIL")};{detail}");
            Application.Quit(passed ? 0 : 2);
        }
    }
}
#endif
