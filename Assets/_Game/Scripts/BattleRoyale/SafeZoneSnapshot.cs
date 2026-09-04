using System;
using UnityEngine;

namespace BattleRoyale
{
    [Serializable]
    public struct SafeZoneSnapshot
    {
        public int phaseIndex;
        public SafeZoneState state;
        public Vector3 startCenter;
        public float startRadius;
        public Vector3 currentCenter;
        public float currentRadius;
        public Vector3 targetCenter;
        public float targetRadius;
        public double stateStartTime;
        public float stateDuration;
        public float damagePerSecond;
        public bool hasNextZone;
    }
}
