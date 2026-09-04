using System.Collections;
using System.Linq;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class RuntimeSoakCapture : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return new WaitForSeconds(0.5f);
            var match = FindAnyObjectByType<MatchManager>();
            if (match == null)
            {
                Debug.LogError("BR_SOAK_FAIL=NO_MATCH_MANAGER");
                Application.Quit(2);
                yield break;
            }

            match.StartMatch();
            var planeDeadline = Time.time + 20f;
            while (match.State != MatchState.Plane && Time.time < planeDeadline) yield return null;
            if (match.State != MatchState.Plane)
            {
                Debug.LogError($"BR_SOAK_FAIL=PLANE_TIMEOUT;STATE={match.State}");
                Application.Quit(3);
                yield break;
            }

            match.Drop.TickPlane(match.Participants, match.Player, true);
            Time.timeScale = 3f;
            var activeDeadline = Time.time + 35f;
            while (match.State != MatchState.Active && Time.time < activeDeadline) yield return null;
            if (match.Player != null)
            {
                match.Player.Motor.Teleport(new Vector3(0f, 3f, 0f));
                match.Player.Health.SetInvulnerable(190f);
            }
            var nextReport = Time.time + 15f;
            var deadline = Time.time + 210f;
            while (match.State != MatchState.Complete && Time.time < deadline)
            {
                if (Time.time >= nextReport)
                {
                    nextReport += 15f;
                    Report(match);
                }
                yield return null;
            }

            Report(match);
            var healths = FindObjectsByType<HealthArmorSystem>(FindObjectsInactive.Include);
            var invalidHealth = healths.Count(health => float.IsNaN(health.Health) || health.Health < -0.01f
                || health.Health > health.MaxHealth + 0.01f);
            var invalidPositions = match.Participants.Count(participant => participant != null
                && (!float.IsFinite(participant.transform.position.x) || !float.IsFinite(participant.transform.position.y)
                    || !float.IsFinite(participant.transform.position.z)));
            var completed = match.State == MatchState.Complete;
            Debug.Log($"BR_SOAK_FINAL=COMPLETED={completed};STATE={match.State};ALIVE={match.AliveCount};INVALID_HEALTH={invalidHealth};INVALID_POSITIONS={invalidPositions};SHOTS={WeaponSystem.TotalShotsFired};BULLETS_CREATED={BulletVisual.TotalCreated};CASINGS_CREATED={ShellCasingVisual.TotalCreated};EFFECTS_CREATED={CombatEffectPool.TotalCreated};REGISTRY={ParticipantRegistry.Count}");
            Time.timeScale = 1f;
            Application.Quit(completed && invalidHealth == 0 && invalidPositions == 0 ? 0 : 4);
        }

        private static void Report(MatchManager match)
        {
            var alive = match.Participants.Where(participant => participant != null && !participant.Health.IsDead).ToArray();
            var grounded = alive.Count(participant => participant.Phase == ParticipantPhase.Grounded);
            var armed = alive.Count(participant => participant.Inventory.ActiveWeapon != null);
            var kills = match.Participants.Where(participant => participant != null).Sum(participant => participant.Eliminations);
            var combatDamage = match.Participants.Where(participant => participant != null).Sum(participant => participant.DamageDealt);
            var states = alive.Where(participant => !participant.IsPlayer)
                .Select(participant => participant.GetComponent<BotController>()?.State.ToString())
                .Where(state => state != null).GroupBy(state => state)
                .Select(group => $"{group.Key}:{group.Count()}");
            Debug.Log($"BR_SOAK_TICK=T={Time.time:0};STATE={match.State};ALIVE={match.AliveCount};GROUNDED={grounded};ARMED={armed};KILLS={kills};COMBAT_DAMAGE={combatDamage};SHOTS={WeaponSystem.TotalShotsFired};PLAYER={match.Player?.Phase}:{match.Player?.Health.Health:0};LOOT={FindObjectsByType<LootPickup>().Length};DEATH_BOXES={FindObjectsByType<DeathLootContainer>().Length};BOT_STATES={string.Join(",", states)}");
        }
    }
}
