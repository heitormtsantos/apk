using UnityEngine;

namespace BattleRoyale
{
    public sealed class VehicleWheel : MonoBehaviour
    {
        [SerializeField] private WheelCollider wheelCollider;
        [SerializeField] private Transform visual;
        [SerializeField] private bool steering;
        [SerializeField] private bool powered = true;
        [SerializeField] private bool handbrake;
        [SerializeField] private Vector3 visualRotationOffset;

        public WheelCollider Collider => wheelCollider;
        public Transform Visual => visual;
        public bool Steering => steering;
        public bool Powered => powered;
        public bool Handbrake => handbrake;
        public bool Grounded => wheelCollider != null && wheelCollider.isGrounded;

        public void Configure(WheelCollider physicsWheel, Transform visualWheel,
            bool canSteer, bool receivesPower, bool receivesHandbrake)
        {
            wheelCollider = physicsWheel;
            visual = visualWheel;
            steering = canSteer;
            powered = receivesPower;
            handbrake = receivesHandbrake;
        }

        public void Apply(float motorTorque, float brakeTorque, float steerAngle, float handbrakeTorque)
        {
            if (wheelCollider == null) return;
            wheelCollider.motorTorque = powered ? motorTorque : 0f;
            wheelCollider.brakeTorque = Mathf.Max(brakeTorque, handbrake ? handbrakeTorque : 0f);
            wheelCollider.steerAngle = steering ? steerAngle : 0f;
        }

        public void SyncVisual()
        {
            if (wheelCollider == null || visual == null) return;
            wheelCollider.GetWorldPose(out var position, out var rotation);
            visual.SetPositionAndRotation(position, rotation * Quaternion.Euler(visualRotationOffset));
        }

        public bool TryGetGroundHit(out WheelHit hit)
        {
            if (wheelCollider != null) return wheelCollider.GetGroundHit(out hit);
            hit = default;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (wheelCollider == null) return;
            Gizmos.color = steering ? Color.cyan : Color.yellow;
            Gizmos.DrawWireSphere(wheelCollider.transform.position, wheelCollider.radius);
            Gizmos.DrawLine(wheelCollider.transform.position,
                wheelCollider.transform.position - transform.up * wheelCollider.suspensionDistance);
        }
    }
}
