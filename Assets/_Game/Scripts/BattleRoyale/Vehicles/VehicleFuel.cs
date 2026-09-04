using System;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class VehicleFuel : MonoBehaviour
    {
        [SerializeField] private bool useFuel;
        [SerializeField, Min(1f)] private float maxFuel = 100f;
        [SerializeField, Min(0f)] private float idleConsumption = 0.015f;
        [SerializeField, Min(0f)] private float drivingConsumption = 0.085f;

        public event Action<float> Changed;
        public bool UseFuel => useFuel;
        public float CurrentFuel { get; private set; }
        public float MaxFuel => Mathf.Max(1f, maxFuel);
        public float NormalizedFuel => useFuel ? CurrentFuel / MaxFuel : 1f;
        public bool CanDrive => !useFuel || CurrentFuel > 0f;

        private void Awake() => CurrentFuel = MaxFuel;

        public void Configure(bool enabled, float capacity)
        {
            useFuel = enabled;
            maxFuel = Mathf.Max(1f, capacity);
            CurrentFuel = maxFuel;
        }

        public void Consume(float throttle, float speedKmh, float deltaTime)
        {
            if (!useFuel || CurrentFuel <= 0f || deltaTime <= 0f) return;
            var rate = speedKmh > 1f || throttle > 0.05f
                ? Mathf.Lerp(idleConsumption, drivingConsumption, Mathf.Clamp01(throttle))
                : idleConsumption;
            var previous = CurrentFuel;
            CurrentFuel = Mathf.Max(0f, CurrentFuel - rate * deltaTime);
            if (!Mathf.Approximately(previous, CurrentFuel)) Changed?.Invoke(CurrentFuel);
        }

        public void Refuel(float amount)
        {
            if (!useFuel || amount <= 0f) return;
            CurrentFuel = Mathf.Min(MaxFuel, CurrentFuel + amount);
            Changed?.Invoke(CurrentFuel);
        }
    }
}
