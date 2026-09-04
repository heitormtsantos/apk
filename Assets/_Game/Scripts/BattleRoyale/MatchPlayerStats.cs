using UnityEngine;

namespace BattleRoyale
{
    [RequireComponent(typeof(BRParticipant))]
    public sealed class MatchPlayerStats : MonoBehaviour
    {
        private BRParticipant owner;
        private float startedAt;

        public int Kills => owner != null ? owner.Eliminations : 0;
        public int Assists => owner != null ? owner.Assists : 0;
        public int Damage => owner != null ? owner.DamageDealt : 0;
        public int Revives => owner != null ? owner.Revives : 0;
        public int Deaths { get; private set; }
        public float SurvivalSeconds { get; private set; }

        private void Awake()
        {
            owner = GetComponent<BRParticipant>();
            startedAt = Time.time;
        }

        public void ResetStats()
        {
            if (owner == null) owner = GetComponent<BRParticipant>();
            owner.Eliminations = 0;
            owner.Assists = 0;
            owner.DamageDealt = 0;
            owner.Revives = 0;
            Deaths = 0;
            SurvivalSeconds = 0f;
            startedAt = Time.time;
        }

        public void RecordDamage(float damage) => owner.DamageDealt += Mathf.Max(0, Mathf.RoundToInt(damage));
        public void RecordKill() => owner.Eliminations++;
        public void RecordAssist() => owner.Assists++;
        public void RecordRevive() => owner.Revives++;

        public void RecordDeath()
        {
            if (Deaths > 0) return;
            Deaths = 1;
            SurvivalSeconds = Mathf.Max(0f, Time.time - startedAt);
        }

        public void FinalizeSurvival()
        {
            if (SurvivalSeconds <= 0f) SurvivalSeconds = Mathf.Max(0f, Time.time - startedAt);
        }
    }
}
