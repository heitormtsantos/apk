using System;
using UnityEngine;

namespace BattleRoyale
{
    [Serializable]
    public struct VehicleSnapshot
    {
        public string vehicleId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public float steering;
        public float speedKmh;
        public float health;
        public float fuel;
        public string driverPlayerId;
        public VehicleDamageState state;
    }

    public sealed class VehicleSpawnPoint : MonoBehaviour
    {
        [SerializeField] private VehicleDefinition definition;
        [SerializeField] private bool spawnOnStart = true;
        public GameObject SpawnedVehicle { get; private set; }

        private void Start()
        {
            if (spawnOnStart) Spawn();
        }

        public GameObject Spawn()
        {
            if (SpawnedVehicle != null || definition == null || definition.GameplayPrefab == null) return SpawnedVehicle;
            SpawnedVehicle = Instantiate(definition.GameplayPrefab, transform.position, transform.rotation);
            return SpawnedVehicle;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.8f, new Vector3(2f, 1.6f, 4.5f));
            Gizmos.DrawRay(transform.position, transform.forward * 3f);
        }
    }
}
