using UnityEngine;

namespace BattleRoyale
{
    public sealed class VehicleInputAdapter : MonoBehaviour
    {
        [SerializeField] private VehicleController controller;
        [SerializeField] private VehicleSeatManager seats;
        [SerializeField] private VehicleAudio vehicleAudio;
        [SerializeField] private VehicleCameraController vehicleCamera;

        public void Configure(VehicleController vehicleController, VehicleSeatManager seatManager,
            VehicleAudio audioController, VehicleCameraController cameraController)
        {
            controller = vehicleController;
            seats = seatManager;
            vehicleAudio = audioController;
            vehicleCamera = cameraController;
        }

        public void Tick(BRInputFrame frame, BRParticipant participant)
        {
            if (!enabled || controller == null || seats == null || participant == null) return;
            var seat = seats.GetSeat(participant);
            if (seat == null) return;
            if (frame.Interact || Input.GetKeyDown(KeyCode.F))
            {
                seats.RequestExitVehicle(participant);
                controller.ClearInput();
                return;
            }
            if (!seat.IsDriver)
            {
                controller.ClearInput();
                if (frame.Swap || Input.GetKeyDown(KeyCode.C)) seats.TryChangeSeat(participant);
                return;
            }

            var throttle = Mathf.Max(frame.Move.y, frame.Fire ? 1f : 0f);
            var reverse = frame.Move.y < -0.05f || frame.Aim;
            controller.SetThrottle(reverse ? -1f : throttle);
            controller.SetSteering(frame.Move.x);
            controller.SetBrake(reverse && controller.SignedForwardSpeed > 1f ? 1f : 0f);
            controller.SetHandbrake(frame.Jump);
            if (frame.Reload || Input.GetKeyDown(KeyCode.H)) vehicleAudio?.Horn();
            if (frame.Swap || Input.GetKeyDown(KeyCode.C)) vehicleCamera?.CycleView();
            if (Input.GetKeyDown(KeyCode.R) && controller.IsUpsideDown) controller.TryFlip();
        }
    }
}
