using System;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class SafeZonePlayerTracker : MonoBehaviour
    {
        private SafeZoneController zone;
        private Transform target;
        private bool initialized;

        public event Action<bool> InsideStateChanged;
        public bool IsInside { get; private set; } = true;
        public float DistanceOutside { get; private set; }

        public void Configure(SafeZoneController safeZone, Transform trackedTarget)
        {
            zone = safeZone;
            target = trackedTarget;
            initialized = false;
            Refresh();
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            if (zone == null || target == null || zone.CurrentState == SafeZoneState.Inactive)
            {
                DistanceOutside = 0f;
                return;
            }
            var inside = zone.IsInsideSafeZone(target.position);
            DistanceOutside = zone.GetDistanceToSafeZone(target.position);
            if (initialized && inside == IsInside) return;
            initialized = true;
            IsInside = inside;
            InsideStateChanged?.Invoke(inside);
        }
    }
}
