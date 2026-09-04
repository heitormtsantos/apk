using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class DamageFeedbackSystem : MonoBehaviour
    {
        public const int MaxDamageEvents = 12;
        private readonly List<DamageFeedbackEvent> events = new();
        private readonly List<IncomingDamageEvent> incomingEvents = new();
        private readonly List<EliminationFeedbackEvent> eliminationEvents = new();

        public static DamageFeedbackSystem Instance { get; private set; }
        public IReadOnlyList<DamageFeedbackEvent> Events => events;
        public IReadOnlyList<IncomingDamageEvent> IncomingEvents => incomingEvents;
        public IReadOnlyList<EliminationFeedbackEvent> EliminationEvents => eliminationEvents;
        public static Color BodyColor => new(1f, 0.82f, 0.08f, 1f);
        public static Color HeadshotColor => new(1f, 0.16f, 0.08f, 1f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            for (var i = events.Count - 1; i >= 0; i--)
                if (events[i].Age >= events[i].Duration) events.RemoveAt(i);
            for (var i = incomingEvents.Count - 1; i >= 0; i--)
                if (incomingEvents[i].Age >= incomingEvents[i].Duration) incomingEvents.RemoveAt(i);
            for (var i = eliminationEvents.Count - 1; i >= 0; i--)
                if (eliminationEvents[i].Age >= eliminationEvents[i].Duration) eliminationEvents.RemoveAt(i);
        }

        public static void Report(Vector3 worldPosition, float damage, bool headshot)
        {
            if (damage <= 0f) return;
            if (Instance == null)
            {
                var root = new GameObject("Damage Feedback System");
                Instance = root.AddComponent<DamageFeedbackSystem>();
            }
            while (Instance.events.Count >= MaxDamageEvents) Instance.events.RemoveAt(0);
            var stackIndex = Instance.events.Count % 4;
            Instance.events.Add(new DamageFeedbackEvent(worldPosition + Vector3.up * 0.34f,
                Mathf.RoundToInt(damage), headshot, Time.time, 0.82f, stackIndex));
        }

        public static void ClearEvents()
        {
            if (Instance == null) return;
            Instance.events.Clear();
            Instance.incomingEvents.Clear();
            Instance.eliminationEvents.Clear();
        }

        public static void ReportIncoming(Vector3 sourcePosition, float damage)
        {
            EnsureInstance();
            Instance.incomingEvents.Add(new IncomingDamageEvent(sourcePosition, Mathf.RoundToInt(damage), Time.time, 0.8f));
        }

        public static void ReportElimination(string killer, string victim, bool localPlayerKill)
        {
            EnsureInstance();
            Instance.eliminationEvents.Add(new EliminationFeedbackEvent(killer, victim, localPlayerKill, Time.time, 3.6f));
            while (Instance.eliminationEvents.Count > 4) Instance.eliminationEvents.RemoveAt(0);
        }

        public static void ReportElimination(EliminationInfo info)
        {
            EnsureInstance();
            var killerName = info.Killer != null ? info.Killer.DisplayName
                : info.KilledBySafeZone ? "ZONA" : "AMBIENTE";
            Instance.eliminationEvents.Add(new EliminationFeedbackEvent(killerName,
                info.Victim != null ? info.Victim.DisplayName : info.VictimPlayerId,
                info.Killer != null && info.Killer.IsPlayer, Time.time, 3.6f,
                info.WeaponId, info.Headshot, info.Assistant != null ? info.Assistant.DisplayName : string.Empty));
            while (Instance.eliminationEvents.Count > 4) Instance.eliminationEvents.RemoveAt(0);
        }

        private static void EnsureInstance()
        {
            if (Instance != null) return;
            var root = new GameObject("Damage Feedback System");
            Instance = root.AddComponent<DamageFeedbackSystem>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    public readonly struct DamageFeedbackEvent
    {
        public DamageFeedbackEvent(Vector3 worldPosition, int damage, bool headshot, float createdAt,
            float duration, int stackIndex = 0)
        {
            WorldPosition = worldPosition;
            Damage = damage;
            Headshot = headshot;
            CreatedAt = createdAt;
            Duration = duration;
            StackIndex = Mathf.Max(0, stackIndex);
        }

        public Vector3 WorldPosition { get; }
        public int Damage { get; }
        public bool Headshot { get; }
        public float CreatedAt { get; }
        public float Duration { get; }
        public int StackIndex { get; }
        public float Age => Mathf.Max(0f, Time.time - CreatedAt);
    }

    public sealed class IncomingDamageEvent
    {
        public IncomingDamageEvent(Vector3 sourcePosition, int damage, float createdAt, float duration)
        {
            SourcePosition = sourcePosition;
            Damage = damage;
            CreatedAt = createdAt;
            Duration = duration;
        }

        public Vector3 SourcePosition { get; }
        public int Damage { get; }
        public float CreatedAt { get; }
        public float Duration { get; }
        public float Age => Mathf.Max(0f, Time.time - CreatedAt);
    }

    public sealed class EliminationFeedbackEvent
    {
        public EliminationFeedbackEvent(string killer, string victim, bool localPlayerKill, float createdAt, float duration,
            string weaponId = "", bool headshot = false, string assistant = "")
        {
            Killer = killer;
            Victim = victim;
            LocalPlayerKill = localPlayerKill;
            CreatedAt = createdAt;
            Duration = duration;
            WeaponId = weaponId ?? string.Empty;
            Headshot = headshot;
            Assistant = assistant ?? string.Empty;
        }

        public string Killer { get; }
        public string Victim { get; }
        public bool LocalPlayerKill { get; }
        public float CreatedAt { get; }
        public float Duration { get; }
        public string WeaponId { get; }
        public bool Headshot { get; }
        public string Assistant { get; }
        public float Age => Mathf.Max(0f, Time.time - CreatedAt);
    }
}
