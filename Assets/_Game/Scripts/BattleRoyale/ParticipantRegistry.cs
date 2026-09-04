using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public static class ParticipantRegistry
    {
        private static readonly HashSet<BRParticipant> Participants = new();

        public static IReadOnlyCollection<BRParticipant> All => Participants;
        public static int Count => Participants.Count;

        public static void Register(BRParticipant participant)
        {
            if (participant != null) Participants.Add(participant);
        }

        public static void Unregister(BRParticipant participant)
        {
            if (participant != null) Participants.Remove(participant);
        }

        public static void RemoveDestroyed()
        {
            Participants.RemoveWhere(participant => participant == null);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Participants.Clear();
    }
}
