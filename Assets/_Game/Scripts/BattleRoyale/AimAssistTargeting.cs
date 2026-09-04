using UnityEngine;

namespace BattleRoyale
{
    public static class AimAssistTargeting
    {
        public static BRParticipant FindBest(GameObject owner, Vector3 origin, Vector3 forward,
            BRParticipant retainedTarget = null, float maxDistance = 65f, float coneDegrees = 11f,
            AimPointPreference preference = AimPointPreference.Chest, float retainedConeMultiplier = 1.45f)
        {
            BRParticipant best = null;
            var bestScore = float.PositiveInfinity;
            foreach (var candidate in ParticipantRegistry.All)
            {
                if (!IsEligible(candidate, owner)) continue;
                var toTarget = AimPoint(candidate, preference) - origin;
                var distance = toTarget.magnitude;
                if (distance < 0.1f || distance > maxDistance) continue;
                var angle = Vector3.Angle(forward, toTarget);
                var allowedCone = candidate == retainedTarget
                    ? RetainedConeDegrees(coneDegrees, retainedConeMultiplier) : coneDegrees;
                if (angle > allowedCone || !HasLineOfSight(origin, toTarget, owner, candidate)) continue;
                var score = CandidateScore(angle, distance, allowedCone, maxDistance, candidate == retainedTarget);
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }

        public static float RetainedConeDegrees(float coneDegrees, float multiplier) =>
            Mathf.Max(0f, coneDegrees) * Mathf.Max(1f, multiplier);

        public static Vector3 AimPoint(BRParticipant participant, AimPointPreference preference = AimPointPreference.Chest)
        {
            if (participant == null) return Vector3.zero;
            var height = preference switch
            {
                AimPointPreference.Head => CharacterPresentationProfile.HeadHeight,
                AimPointPreference.Pelvis => CharacterPresentationProfile.VisualHeight * 0.42f,
                _ => CharacterPresentationProfile.VisualHeight * 0.68f
            };
            return participant.transform.position + Vector3.up * height;
        }

        public static float CandidateScore(float angle, float distance, float coneDegrees, float maxDistance,
            bool retained)
        {
            var angular = Mathf.Clamp01(angle / Mathf.Max(0.01f, coneDegrees));
            var range = Mathf.Clamp01(distance / Mathf.Max(0.01f, maxDistance));
            return angular * 0.76f + range * 0.24f - (retained ? 0.16f : 0f);
        }

        public static float MagnetismStrength(float manualLookMagnitude) =>
            Mathf.Lerp(0.88f, 0.22f, Mathf.InverseLerp(0.15f, 4.5f, manualLookMagnitude));

        private static bool IsEligible(BRParticipant candidate, GameObject owner) =>
            candidate != null && candidate.gameObject != owner && !candidate.Health.IsDead
            && candidate.Phase == ParticipantPhase.Grounded && IsEnemyOrLegacyOwner(candidate, owner);

        private static bool IsEnemyOrLegacyOwner(BRParticipant candidate, GameObject owner)
        {
            var ownerParticipant = owner != null ? owner.GetComponent<BRParticipant>() : null;
            return ownerParticipant == null || BRMatchRules.AreEnemies(ownerParticipant, candidate);
        }

        private static bool HasLineOfSight(Vector3 origin, Vector3 toTarget, GameObject owner,
            BRParticipant candidate)
        {
            var distance = toTarget.magnitude;
            var sightHits = Physics.RaycastAll(origin, toTarget / distance, distance + 0.15f,
                ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(sightHits, RaycastHitDistanceComparer.Instance);
            foreach (var hit in sightHits)
            {
                var collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(owner.transform)) continue;
                if (collider.GetComponentInParent<LootPickup>() != null) continue;
                return collider.GetComponentInParent<BRParticipant>() == candidate;
            }
            return true;
        }
    }
}
