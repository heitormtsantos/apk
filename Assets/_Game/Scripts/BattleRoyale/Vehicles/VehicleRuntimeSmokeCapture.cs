#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class VehicleRuntimeSmokeCapture : MonoBehaviour
    {
        private MatchManager match;

        private IEnumerator Start()
        {
            match = GetComponent<MatchManager>();
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
                        var center = PlayableArea.Center;
                        if (PlayableArea.TryFindOpenGround(center, out var ground))
                            match.Player.Motor.Teleport(ground + Vector3.up * 0.12f);
                        match.Player.SetPhase(ParticipantPhase.Grounded);
                    }
                }
                yield return null;
            }
            if (match == null || match.State != MatchState.Active || match.Player == null)
            {
                Finish(false, "MATCH_NOT_ACTIVE", null);
                yield break;
            }

            VehicleSeatManager vehicle = null;
            deadline = Time.realtimeSinceStartup + 12f;
            while (vehicle == null && Time.realtimeSinceStartup < deadline)
            {
                var vehicles = FindObjectsByType<VehicleSeatManager>(FindObjectsInactive.Exclude);
                if (vehicles.Length > 0) vehicle = vehicles[0];
                yield return null;
            }
            if (vehicle == null)
            {
                Finish(false, "VEHICLE_NOT_SPAWNED", null);
                yield break;
            }

            match.Player.Motor.Teleport(vehicle.transform.position + vehicle.transform.forward * 2.8f);
            yield return null;
            match.TryInteractPlayer();
            yield return new WaitForSeconds(0.5f);
            if (match.ActiveVehicle != vehicle)
            {
                Finish(false, "ENTER_FAILED", vehicle);
                yield break;
            }

            var controller = vehicle.GetComponent<VehicleController>();
            vehicle.GetComponent<VehicleInputAdapter>().enabled = false;
            controller.SetThrottle(1f);
            controller.SetSteering(0.18f);
            yield return new WaitForSeconds(4f);
            controller.ClearInput();
            var speed = controller.CurrentSpeedKmh;
            var grounded = 0;
            foreach (var wheel in controller.Wheels) if (wheel != null && wheel.Grounded) grounded++;
            CaptureScreenshot();
            yield return new WaitForSeconds(0.5f);
            var exited = vehicle.RequestExitVehicle(match.Player);
            Finish(speed > 3f && grounded >= 2 && exited,
                $"SPEED={speed:F1};GROUNDED={grounded};EXITED={exited}", vehicle);
        }

        private static void CaptureScreenshot()
        {
            var path = CommandLineValue("-vehicleScreenshot");
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

        private static void Finish(bool passed, string detail, VehicleSeatManager vehicle)
        {
            var health = vehicle != null ? vehicle.GetComponent<VehicleHealth>() : null;
            Debug.Log($"BR_VEHICLE_SMOKE={(passed ? "PASS" : "FAIL")};{detail};HEALTH={health?.CurrentHealth ?? 0f:F0}");
            Application.Quit(passed ? 0 : 2);
        }
    }
}
#endif
