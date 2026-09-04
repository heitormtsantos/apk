using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(menuName = "Battle Royale/Vehicles/Vehicle Definition", fileName = "VehicleDefinition")]
    public sealed class VehicleDefinition : ScriptableObject
    {
        [SerializeField] private string vehicleId = "car_01";
        [SerializeField] private string displayName = "Raven Runner";
        [SerializeField] private GameObject gameplayPrefab;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1f)] private float maxHealth = 850f;
        [SerializeField, Min(100f)] private float mass = 1350f;
        [SerializeField, Min(1f)] private float maxForwardSpeed = 36f;
        [SerializeField, Min(1f)] private float maxReverseSpeed = 12f;
        [SerializeField, Min(1f)] private float motorTorque = 1750f;
        [SerializeField, Min(1f)] private float brakeTorque = 2800f;
        [SerializeField, Range(5f, 50f)] private float maxSteerAngle = 33f;
        [SerializeField, Min(0f)] private float fuelCapacity = 100f;
        [SerializeField, Range(1, 8)] private int seatCount = 4;

        public string VehicleId => vehicleId;
        public string DisplayName => displayName;
        public GameObject GameplayPrefab => gameplayPrefab;
        public Sprite Icon => icon;
        public float MaxHealth => maxHealth;
        public float Mass => mass;
        public float MaxForwardSpeed => maxForwardSpeed;
        public float MaxReverseSpeed => maxReverseSpeed;
        public float MotorTorque => motorTorque;
        public float BrakeTorque => brakeTorque;
        public float MaxSteerAngle => maxSteerAngle;
        public float FuelCapacity => fuelCapacity;
        public int SeatCount => seatCount;
    }
}
