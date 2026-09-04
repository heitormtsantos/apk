using UnityEngine;
using System.Collections.Generic;

namespace BattleRoyale
{
    public sealed class VehicleRuntimeSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject vehiclePrefab;
        [SerializeField, Min(1)] private int vehicleCount = 3;
        private MatchManager match;
        private bool spawned;
        private readonly List<GameObject> instances = new();

        public void Configure(MatchManager manager, GameObject prefab = null)
        {
            match = manager;
            vehiclePrefab = prefab != null ? prefab : Resources.Load<GameObject>("Vehicles/Car/Vehicle_Car_Gameplay");
        }

        private void Update()
        {
            if (match != null && match.State == MatchState.Lobby && spawned)
            {
                foreach (var instance in instances) if (instance != null) Destroy(instance);
                instances.Clear();
                spawned = false;
            }
            if (spawned || match == null || match.State != MatchState.Active || match.Player == null
                || match.Player.Phase != ParticipantPhase.Grounded) return;
            SpawnNearPlayer();
        }

        private void SpawnNearPlayer()
        {
            if (vehiclePrefab == null)
            {
                Debug.LogWarning("[VehicleRuntimeSpawner] Vehicle prefab not found.", this);
                spawned = true;
                return;
            }
            var origin = match.Player.transform.position;
            var directions = new[] { match.Player.transform.right, -match.Player.transform.right,
                match.Player.transform.forward, -match.Player.transform.forward };
            var created = 0;
            for (var i = 0; i < directions.Length && created < vehicleCount; i++)
            {
                var probe = origin + directions[i] * (8f + created * 5f) + Vector3.up * 18f;
                if (!Physics.Raycast(probe, Vector3.down, out var hit, 45f, ~0, QueryTriggerInteraction.Ignore)
                    || hit.normal.y < 0.72f) continue;
                var forward = Vector3.ProjectOnPlane(match.Player.transform.forward, hit.normal);
                if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
                instances.Add(Instantiate(vehiclePrefab, hit.point + Vector3.up * 0.55f,
                    Quaternion.LookRotation(forward, hit.normal)));
                created++;
            }
            spawned = true;
        }
    }
}
