using System;
using UnityEngine;

namespace BattleRoyale
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleController : MonoBehaviour
    {
        [SerializeField] private VehicleDefinition definition;
        [SerializeField] private Rigidbody body;
        [SerializeField] private Transform centerOfMass;
        [SerializeField] private VehicleWheel[] wheels = Array.Empty<VehicleWheel>();
        [Header("Arcade Physics")]
        [SerializeField] private float motorTorque = 1750f;
        [SerializeField] private float reverseTorque = 1100f;
        [SerializeField] private float brakeTorque = 2800f;
        [SerializeField] private float handbrakeTorque = 4600f;
        [SerializeField] private float maxForwardSpeed = 36f;
        [SerializeField] private float maxReverseSpeed = 12f;
        [SerializeField] private float maxSteerAngle = 33f;
        [SerializeField] private float highSpeedSteerAngle = 12f;
        [SerializeField] private float downforce = 38f;
        [SerializeField] private float antiRollStrength = 5400f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.08f;

        private float throttle;
        private float steering;
        private float brake;
        private bool handbrake;
        private bool controlsEnabled = true;
        private VehicleFuel fuel;
        private VehicleHealth health;

        public float CurrentSpeedKmh => body == null ? 0f : body.linearVelocity.magnitude * 3.6f;
        public float SignedForwardSpeed => body == null ? 0f : Vector3.Dot(body.linearVelocity, transform.forward);
        public float NormalizedSpeed => Mathf.Clamp01(CurrentSpeedKmh / Mathf.Max(1f, maxForwardSpeed * 3.6f));
        public bool HasGroundContact { get; private set; }
        public bool IsUpsideDown => Vector3.Dot(transform.up, Vector3.up) < 0.15f;
        public bool ControlsEnabled => controlsEnabled && (health == null || !health.IsDestroyed);
        public Rigidbody Body => body;
        public VehicleWheel[] Wheels => wheels;

        private void Awake()
        {
            body ??= GetComponent<Rigidbody>();
            fuel = GetComponent<VehicleFuel>();
            health = GetComponent<VehicleHealth>();
            ApplyDefinition();
            if (body != null && centerOfMass != null)
                body.centerOfMass = transform.InverseTransformPoint(centerOfMass.position);
        }

        public void Configure(VehicleDefinition data, Rigidbody vehicleBody, Transform massCenter,
            VehicleWheel[] vehicleWheels)
        {
            definition = data;
            body = vehicleBody;
            centerOfMass = massCenter;
            wheels = vehicleWheels ?? Array.Empty<VehicleWheel>();
            ApplyDefinition();
        }

        private void ApplyDefinition()
        {
            if (definition == null || body == null) return;
            body.mass = definition.Mass;
            motorTorque = definition.MotorTorque;
            brakeTorque = definition.BrakeTorque;
            maxForwardSpeed = definition.MaxForwardSpeed;
            maxReverseSpeed = definition.MaxReverseSpeed;
            maxSteerAngle = definition.MaxSteerAngle;
        }

        public void SetThrottle(float value) => throttle = Mathf.Clamp(value, -1f, 1f);
        public void SetSteering(float value) => steering = Mathf.Clamp(value, -1f, 1f);
        public void SetBrake(float value) => brake = Mathf.Clamp01(value);
        public void SetHandbrake(bool value) => handbrake = value;
        public void SetControlsEnabled(bool value)
        {
            controlsEnabled = value;
            if (!value) ClearInput();
        }

        public void ClearInput()
        {
            throttle = steering = brake = 0f;
            handbrake = false;
        }

        private void FixedUpdate()
        {
            if (body == null || wheels == null || wheels.Length == 0) return;
            HasGroundContact = false;
            var grounded = 0;
            foreach (var wheel in wheels)
            {
                if (wheel != null && wheel.Grounded) grounded++;
            }
            HasGroundContact = grounded > 0;

            var canDrive = ControlsEnabled && (fuel == null || fuel.CanDrive);
            var inputThrottle = canDrive ? throttle : 0f;
            var speed = SignedForwardSpeed;
            var forwardLimit = speed >= maxForwardSpeed && inputThrottle > 0f;
            var reverseLimit = speed <= -maxReverseSpeed && inputThrottle < 0f;
            var torque = forwardLimit || reverseLimit ? 0f
                : inputThrottle >= 0f ? inputThrottle * motorTorque : inputThrottle * reverseTorque;
            var steerLimit = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, NormalizedSpeed);
            var steer = steering * steerLimit * (HasGroundContact ? 1f : airControl);
            var serviceBrake = brake * brakeTorque;
            foreach (var wheel in wheels)
                wheel?.Apply(torque, serviceBrake, steer, handbrake ? handbrakeTorque : 0f);

            if (HasGroundContact)
            {
                body.AddForce(-transform.up * downforce * body.linearVelocity.magnitude,
                    ForceMode.Force);
                ApplyAntiRollPairs();
            }
            else if (Mathf.Abs(steering) > 0.01f)
                body.AddTorque(transform.up * steering * airControl * 140f, ForceMode.Force);
            fuel?.Consume(Mathf.Abs(inputThrottle), CurrentSpeedKmh, Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            foreach (var wheel in wheels) wheel?.SyncVisual();
        }

        private void ApplyAntiRollPairs()
        {
            if (wheels.Length < 4) return;
            ApplyAntiRoll(wheels[0], wheels[1]);
            ApplyAntiRoll(wheels[2], wheels[3]);
        }

        private void ApplyAntiRoll(VehicleWheel left, VehicleWheel right)
        {
            if (left?.Collider == null || right?.Collider == null) return;
            var leftTravel = SuspensionTravel(left, out var leftGrounded);
            var rightTravel = SuspensionTravel(right, out var rightGrounded);
            var force = (leftTravel - rightTravel) * antiRollStrength;
            if (leftGrounded) body.AddForceAtPosition(left.Collider.transform.up * -force,
                left.Collider.transform.position);
            if (rightGrounded) body.AddForceAtPosition(right.Collider.transform.up * force,
                right.Collider.transform.position);
        }

        private static float SuspensionTravel(VehicleWheel wheel, out bool grounded)
        {
            grounded = wheel.TryGetGroundHit(out var hit);
            if (!grounded) return 1f;
            var local = wheel.Collider.transform.InverseTransformPoint(hit.point);
            return (-local.y - wheel.Collider.radius) / Mathf.Max(0.01f, wheel.Collider.suspensionDistance);
        }

        public bool TryFlip()
        {
            if (!IsUpsideDown || CurrentSpeedKmh > 4f || body == null) return false;
            var position = transform.position + Vector3.up * 1.2f;
            var yaw = transform.eulerAngles.y;
            body.position = position;
            body.rotation = Quaternion.Euler(0f, yaw, 0f);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            return true;
        }

        [ContextMenu("Validate Vehicle Setup")]
        private void ValidateSetup()
        {
            if (body == null) Debug.LogWarning("[VehicleController] Rigidbody nao configurado.", this);
            if (centerOfMass == null) Debug.LogWarning("[VehicleController] CenterOfMass ausente.", this);
            if (wheels == null || wheels.Length != 4)
                Debug.LogWarning("[VehicleController] Quatro rodas sao obrigatorias.", this);
        }

        private void OnDrawGizmosSelected()
        {
            if (centerOfMass == null) return;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(centerOfMass.position, 0.16f);
        }
    }
}
