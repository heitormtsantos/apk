using UnityEngine;

namespace BattleRoyale
{
    public sealed class VehicleCameraController : MonoBehaviour
    {
        [SerializeField] private Transform chaseTarget;
        [SerializeField] private Transform closeTarget;
        private ThirdPersonCamera cameraController;
        private bool closeView;

        public Transform ActiveTarget => closeView && closeTarget != null ? closeTarget : chaseTarget;

        public void Configure(Transform chase, Transform close)
        {
            chaseTarget = chase;
            closeTarget = close;
        }

        public void Activate(ThirdPersonCamera camera)
        {
            cameraController = camera;
            cameraController?.SetStagingView(false);
            cameraController?.SetTarget(ActiveTarget != null ? ActiveTarget : transform);
        }

        public void Deactivate(Transform player)
        {
            cameraController?.SetTarget(player);
            cameraController = null;
        }

        public void CycleView()
        {
            closeView = !closeView;
            if (cameraController != null) cameraController.SetTarget(ActiveTarget != null ? ActiveTarget : transform);
        }
    }
}
