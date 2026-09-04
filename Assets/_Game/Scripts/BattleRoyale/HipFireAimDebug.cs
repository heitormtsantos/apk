using UnityEngine;

namespace BattleRoyale
{
    [RequireComponent(typeof(Camera), typeof(ThirdPersonCamera))]
    public sealed class HipFireAimDebug : MonoBehaviour
    {
        private ThirdPersonCamera controller;
        private Camera viewCamera;

        public bool Visible { get; set; }

        private void Awake()
        {
            controller = GetComponent<ThirdPersonCamera>();
            viewCamera = GetComponent<Camera>();
        }

        private void OnGUI()
        {
            if (!Visible || controller == null || viewCamera == null) return;
            var target = controller.AimAssistTarget;
            if (target != null)
            {
                DrawPoint(AimAssistTargeting.AimPoint(target, AimPointPreference.Chest), Color.cyan, 8f);
                DrawPoint(AimAssistTargeting.AimPoint(target, AimPointPreference.Head), Color.yellow, 6f);
            }
            var text = $"TARGET: {(target != null ? target.DisplayName : "NONE")}\n"
                + $"H: {controller.HorizontalAssistStrength:0.000}  V: {controller.VerticalAssistStrength:0.000}\n"
                + $"STATE: {controller.VerticalAimState}\n"
                + $"FIRE DRAG: {controller.LastFireDragDelta.x:0.0000}, {controller.LastFireDragDelta.y:0.0000}\n"
                + $"CHEST: {controller.ChestScreenDistance:0.000}  HEAD: {controller.HeadScreenDistance:0.000}";
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(12f, 92f, 285f, 96f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(20f, 98f, 272f, 88f), text);
            GUI.color = Color.white;
        }

        private void DrawPoint(Vector3 world, Color color, float radius)
        {
            var point = viewCamera.WorldToScreenPoint(world);
            if (point.z <= 0f) return;
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(point.x - radius, Screen.height - point.y - radius,
                radius * 2f, radius * 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Visible || controller == null) return;
            var participant = controller.TargetTransform != null
                ? controller.TargetTransform.GetComponent<BRParticipant>() : null;
            var solution = participant != null && participant.Weapon != null
                ? participant.Weapon.LastAimSolution : default;
            if (!solution.IsFinite) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(solution.CameraRay.origin, solution.AimPoint);
            Gizmos.color = solution.MuzzleObstructed ? Color.red : Color.yellow;
            Gizmos.DrawLine(solution.MuzzleRay.origin, solution.EndPoint);
            Gizmos.DrawSphere(solution.AimPoint, 0.035f);
        }
    }
}
