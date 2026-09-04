using UnityEngine;

namespace BattleRoyale
{
    public enum PlayerLifeState
    {
        Alive,
        Knocked,
        Dead
    }

    public enum DamageType
    {
        Unknown,
        Weapon,
        SafeZone,
        Explosion,
        Vehicle,
        Fall,
        BleedOut
    }

    public readonly struct EliminationInfo
    {
        public EliminationInfo(BRParticipant victim, BRParticipant killer, BRParticipant assistant,
            DamageType damageType, string weaponId, bool headshot, Vector3 deathPosition, float distance,
            bool finishedKnockedPlayer, bool wasFinisher, string knockedByPlayerId, string finishedByPlayerId,
            float timestamp)
        {
            Victim = victim;
            Killer = killer;
            Assistant = assistant;
            VictimPlayerId = victim != null ? victim.PlayerId : string.Empty;
            KillerPlayerId = killer != null ? killer.PlayerId : string.Empty;
            AssistantPlayerId = assistant != null ? assistant.PlayerId : string.Empty;
            VictimTeamId = victim != null ? victim.TeamId : -1;
            KillerTeamId = killer != null ? killer.TeamId : -1;
            Type = damageType;
            WeaponId = weaponId ?? string.Empty;
            Headshot = headshot;
            DeathPosition = deathPosition;
            Distance = Mathf.Max(0f, distance);
            FinishedKnockedPlayer = finishedKnockedPlayer;
            WasFinisher = wasFinisher;
            KnockedByPlayerId = knockedByPlayerId ?? string.Empty;
            FinishedByPlayerId = finishedByPlayerId ?? string.Empty;
            Timestamp = timestamp;
        }

        public BRParticipant Victim { get; }
        public BRParticipant Killer { get; }
        public BRParticipant Assistant { get; }
        public string VictimPlayerId { get; }
        public string KillerPlayerId { get; }
        public string AssistantPlayerId { get; }
        public int VictimTeamId { get; }
        public int KillerTeamId { get; }
        public DamageType Type { get; }
        public string WeaponId { get; }
        public bool Headshot { get; }
        public Vector3 DeathPosition { get; }
        public float Distance { get; }
        public bool FinishedKnockedPlayer { get; }
        public bool WasFinisher { get; }
        public bool KilledBySafeZone => Type == DamageType.SafeZone;
        public bool KilledByExplosion => Type == DamageType.Explosion;
        public bool KilledByVehicle => Type == DamageType.Vehicle;
        public string KnockedByPlayerId { get; }
        public string FinishedByPlayerId { get; }
        public float Timestamp { get; }
    }
}
