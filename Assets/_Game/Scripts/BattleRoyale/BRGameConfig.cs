using System;
using UnityEngine;

namespace BattleRoyale
{
    [Serializable]
    public sealed class BRGameConfig
    {
        public BRMatchMode selectedMode = BRMatchMode.Solo;
        public BRPlaylist playlist = BRPlaylist.Casual;
        public int participantCount = 20;
        public int botCount = 19;
        public int lootSeed = 9142;
        public Vector2 mapSize = new(180f, 180f);
        public float playerMoveSpeed = 5.6f;
        public float botMoveSpeed = 4.6f;
        public float gravity = 22f;
        public float jumpSpeed = 7f;
        public float dropHeight = 82f;
        public float parachuteHeight = 24f;
        public float freefallSpeed = 24f;
        public float parachuteSpeed = 7f;
        public float planeSpeed = 36f;
        public float interactRadius = 2.3f;
        public float bleedoutSeconds = 24f;
        public float reviveSeconds = 4f;
        public float reviveHealth = 35f;
        public float reviveRadius = 2.2f;
        public float downedMoveSpeed = 1.35f;
        public float matchStartDelay = 1.5f;
        public float stagingSeconds = 12f;
        public Vector3 stagingCenter = new(0f, 0.08f, -126f);
        public bool startGroundedInEditor = true;
        public SafeZonePhase[] safeZonePhases = DefaultSafeZonePhases();

        public void Normalize()
        {
            participantCount = BRMatchRules.NormalizeParticipantCount(selectedMode, participantCount);
            botCount = Mathf.Max(0, participantCount - 1);
            if (mapSize.x < 60f || mapSize.y < 60f) mapSize = new Vector2(180f, 180f);
            if (safeZonePhases == null || safeZonePhases.Length == 0) safeZonePhases = DefaultSafeZonePhases();
            bleedoutSeconds = NormalizePositive(bleedoutSeconds, 24f, 1f, 300f);
            reviveSeconds = NormalizePositive(reviveSeconds, 4f, 0.25f, 30f);
            reviveHealth = NormalizePositive(reviveHealth, 35f, 1f, 100f);
            reviveRadius = NormalizePositive(reviveRadius, 2.2f, 0.5f, 8f);
            downedMoveSpeed = NormalizePositive(downedMoveSpeed, 1.35f, 0.25f, 4f);
        }

        private static float NormalizePositive(float value, float fallback, float minimum, float maximum) =>
            float.IsNaN(value) || float.IsInfinity(value) || value <= 0f
                ? fallback
                : Mathf.Clamp(value, minimum, maximum);

        public static SafeZonePhase[] DefaultSafeZonePhases() =>
            new[]
            {
                new SafeZonePhase(24f, 16f, 76f, 1.5f),
                new SafeZonePhase(18f, 16f, 52f, 3f),
                new SafeZonePhase(14f, 14f, 32f, 5f),
                new SafeZonePhase(10f, 12f, 17f, 8f),
                new SafeZonePhase(8f, 10f, 6f, 12f)
            };
    }

    [Serializable]
    public sealed class SafeZonePhase
    {
        public float waitSeconds;
        public float shrinkSeconds;
        public float radius;
        public float damagePerSecond;

        public SafeZonePhase()
        {
        }

        public SafeZonePhase(float waitSeconds, float shrinkSeconds, float radius, float damagePerSecond)
        {
            this.waitSeconds = waitSeconds;
            this.shrinkSeconds = shrinkSeconds;
            this.radius = radius;
            this.damagePerSecond = damagePerSecond;
        }
    }

    public enum MatchState
    {
        Lobby,
        Preparing,
        Staging,
        Plane,
        Active,
        Complete
    }

    public enum ParticipantPhase
    {
        WaitingPlane,
        Freefall,
        Parachute,
        Grounded,
        Downed,
        Eliminated
    }

    public enum LootKind
    {
        Weapon,
        Ammo,
        Heal,
        Vest,
        Helmet,
        Attachment,
        Currency,
        Backpack,
        GlooWall,
        Grenade
    }

    public enum AmmoKind
    {
        Light,
        Medium,
        Shell,
        Long
    }

    public enum WeaponClass
    {
        Rifle,
        Smg,
        Shotgun,
        Sniper,
        Pistol
    }
}
