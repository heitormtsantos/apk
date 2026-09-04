using System.Collections.Generic;

namespace BattleRoyale
{
    public enum ReviveHoldStatus
    {
        Progressing,
        Completed,
        Cancelled
    }

    public enum InteractionPriority
    {
        None,
        Revive,
        Loot
    }

    public readonly struct ReviveHoldStep
    {
        public ReviveHoldStep(float progress, ReviveHoldStatus status)
        {
            Progress = progress;
            Status = status;
        }

        public float Progress { get; }
        public ReviveHoldStatus Status { get; }
    }

    public enum BRMatchMode
    {
        Solo,
        Duo,
        Trio,
        Squad
    }

    public enum BRPlaylist
    {
        Casual,
        Ranked
    }

    public static class BRMatchRules
    {
        public const int MaxParticipantCount = 100;

        public static int TeamSize(BRMatchMode mode) => mode switch
        {
            BRMatchMode.Duo => 2,
            BRMatchMode.Trio => 3,
            BRMatchMode.Squad => 4,
            _ => 1
        };

        public static int NormalizeParticipantCount(BRMatchMode mode) => mode switch
        {
            BRMatchMode.Trio => 18,
            _ => 20
        };

        public static int NormalizeParticipantCount(BRMatchMode mode, int participantCount)
        {
            if (participantCount <= 0) return NormalizeParticipantCount(mode);
            var teamSize = TeamSize(mode);
            var minimum = teamSize * 2;
            var maximum = MaxParticipantCount - MaxParticipantCount % teamSize;
            var requested = System.Math.Min(maximum, System.Math.Max(minimum, participantCount));
            var remainder = requested % teamSize;
            return remainder == 0 ? requested : System.Math.Min(maximum, requested + teamSize - remainder);
        }

        public static int NormalizeParticipantCount(int participantCount, BRMatchMode mode) =>
            NormalizeParticipantCount(mode, participantCount);

        public static int AssignTeamId(int participantIndex) => System.Math.Max(0, participantIndex);

        public static int AssignTeamId(int participantIndex, BRMatchMode mode) =>
            System.Math.Max(0, participantIndex) / TeamSize(mode);

        public static int AssignTeamId(BRMatchMode mode, int participantIndex) =>
            AssignTeamId(participantIndex, mode);

        public static bool AreEnemies(int firstTeamId, int secondTeamId) => firstTeamId != secondTeamId;

        public static bool AreEnemies(BRParticipant first, BRParticipant second) =>
            first != null && second != null && first != second && AreEnemies(first.TeamId, second.TeamId);

        public static bool HasActiveTeammate(BRParticipant participant,
            IEnumerable<BRParticipant> participants)
        {
            if (participant == null || participants == null) return false;
            foreach (var candidate in participants)
            {
                if (candidate == null || candidate == participant || candidate.TeamId != participant.TeamId) continue;
                if (candidate.Health != null && !candidate.Health.IsDead && !candidate.Health.IsDowned) return true;
            }
            return false;
        }

        public static bool ShouldDownOnLethal(BRMatchMode mode, BRParticipant participant,
            IEnumerable<BRParticipant> participants) =>
            mode != BRMatchMode.Solo && HasActiveTeammate(participant, participants);

        public static bool CanRevive(BRParticipant reviver, BRParticipant target, float maxDistance)
        {
            if (reviver == null || target == null || reviver == target || maxDistance <= 0f) return false;
            if (reviver.TeamId != target.TeamId || !reviver.IsCombatCapable || !target.IsDowned) return false;
            if (reviver.Phase != ParticipantPhase.Grounded || target.Phase != ParticipantPhase.Downed) return false;
            return UnityEngine.Vector3.Distance(reviver.transform.position, target.transform.position) <= maxDistance;
        }

        public static bool CanContinueRevive(BRParticipant reviver, BRParticipant target, float maxDistance,
            int damageSequenceAtStart) => CanRevive(reviver, target, maxDistance)
            && reviver.Health.DamageSequence == damageSequenceAtStart;

        public static ReviveHoldStep AdvanceReviveHold(float progress, float deltaTime, float duration,
            bool holding, bool eligible)
        {
            if (!holding || !eligible) return new ReviveHoldStep(0f, ReviveHoldStatus.Cancelled);
            var next = UnityEngine.Mathf.Clamp01(progress
                + UnityEngine.Mathf.Max(0f, deltaTime) / UnityEngine.Mathf.Max(0.01f, duration));
            return new ReviveHoldStep(next,
                next >= 1f ? ReviveHoldStatus.Completed : ReviveHoldStatus.Progressing);
        }

        public static InteractionPriority ResolveInteractionPriority(bool interactionRequested,
            bool hasReviveTarget, bool hasLootTarget)
        {
            if (!interactionRequested) return InteractionPriority.None;
            if (hasReviveTarget) return InteractionPriority.Revive;
            return hasLootTarget ? InteractionPriority.Loot : InteractionPriority.None;
        }

        public static bool TeamHasActiveMember(int teamId, IEnumerable<BRParticipant> participants)
        {
            if (participants == null) return false;
            foreach (var participant in participants)
                if (participant != null && participant.TeamId == teamId && participant.IsCombatCapable) return true;
            return false;
        }

        public static bool ShouldEliminateDownedTeamMember(BRParticipant participant,
            IEnumerable<BRParticipant> participants) => participant != null && participant.IsDowned
            && !TeamHasActiveMember(participant.TeamId, participants);

        public static int CountSurvivingTeams(IEnumerable<BRParticipant> participants)
        {
            if (participants == null) return 0;
            var survivingTeams = new HashSet<int>();
            foreach (var participant in participants)
            {
                if (participant == null || participant.Health == null || participant.Health.IsDead) continue;
                survivingTeams.Add(participant.TeamId);
            }
            return survivingTeams.Count;
        }

        public static int CountSurvivingTeams(IEnumerable<int> survivingTeamIds)
        {
            if (survivingTeamIds == null) return 0;
            return new HashSet<int>(survivingTeamIds).Count;
        }

        public static int CalculateTeamPlacement(BRParticipant player, IEnumerable<BRParticipant> participants)
        {
            var survivingTeams = new HashSet<int>();
            if (participants != null)
            {
                foreach (var participant in participants)
                {
                    if (participant == null || participant.Health == null || participant.Health.IsDead) continue;
                    survivingTeams.Add(participant.TeamId);
                }
            }

            if (player != null && survivingTeams.Contains(player.TeamId)) return 1;
            return System.Math.Max(2, survivingTeams.Count + 1);
        }
    }
}
