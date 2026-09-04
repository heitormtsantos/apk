using System;
using UnityEngine;

namespace BattleRoyale
{
    [Serializable]
    public sealed class SafeZonePhaseData
    {
        public string phaseName = "Phase";
        [Min(0f)] public float waitingDuration = 30f;
        [Min(0.1f)] public float shrinkingDuration = 20f;
        [Min(0f)] public float targetRadius = 50f;
        [Min(0f)] public float damagePerSecond = 1f;
        [Min(0f)] public float warningTime = 10f;
        [Range(0f, 1f)] public float maxCenterOffsetMultiplier = 0.72f;
        public bool generateNewCenter = true;
        public bool showNextZoneDuringWaiting = true;

        public SafeZonePhaseData()
        {
        }

        public SafeZonePhaseData(string name, float waiting, float shrinking, float radius, float damage)
        {
            phaseName = name;
            waitingDuration = waiting;
            shrinkingDuration = shrinking;
            targetRadius = radius;
            damagePerSecond = damage;
            warningTime = Mathf.Min(10f, waiting);
        }

        public void Normalize(float maximumRadius)
        {
            phaseName = string.IsNullOrWhiteSpace(phaseName) ? "Phase" : phaseName;
            waitingDuration = Mathf.Max(0f, waitingDuration);
            shrinkingDuration = Mathf.Max(0.1f, shrinkingDuration);
            targetRadius = Mathf.Clamp(targetRadius, 0f, Mathf.Max(0f, maximumRadius));
            damagePerSecond = Mathf.Max(0f, damagePerSecond);
            warningTime = Mathf.Clamp(warningTime, 0f, waitingDuration);
            maxCenterOffsetMultiplier = Mathf.Clamp01(maxCenterOffsetMultiplier);
        }
    }
}
