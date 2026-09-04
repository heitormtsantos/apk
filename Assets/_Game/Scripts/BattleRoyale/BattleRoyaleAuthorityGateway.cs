using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public enum AuthorityRequestStatus
    {
        Accepted,
        Duplicate,
        InvalidState,
        InvalidActor,
        OutOfRange,
        Rejected
    }

    public readonly struct AuthorityRequestResult
    {
        public AuthorityRequestResult(AuthorityRequestStatus status, string reason = "")
        {
            Status = status;
            Reason = reason ?? string.Empty;
        }

        public AuthorityRequestStatus Status { get; }
        public string Reason { get; }
        public bool Accepted => Status == AuthorityRequestStatus.Accepted;
    }

    [RequireComponent(typeof(MatchManager))]
    public sealed class BattleRoyaleAuthorityGateway : MonoBehaviour
    {
        private const int MaxRememberedRequests = 2048;
        private readonly HashSet<long> processedRequests = new();
        private readonly Queue<long> requestOrder = new();
        private MatchManager match;

        private void Awake() => match = GetComponent<MatchManager>();

        public AuthorityRequestResult RequestDamage(long requestId, BRParticipant attacker,
            BRParticipant target, float damage, bool headshot)
        {
            if (!BeginRequest(requestId)) return Duplicate();
            if (!MatchActive()) return InvalidState();
            if (attacker == null || target == null || !attacker.IsCombatCapable
                || !BRMatchRules.AreEnemies(attacker, target)) return InvalidActor();
            if (damage <= 0f || target.Health == null || target.Health.IsDead)
                return Rejected("Alvo ou dano invalido");
            target.Health.ApplyDamage(damage, headshot, attacker.gameObject);
            return Accepted();
        }

        public AuthorityRequestResult RequestTakeLoot(long requestId, BRParticipant actor,
            DeathLootContainer container, int entryId, float interactionRadius)
        {
            if (!BeginRequest(requestId)) return Duplicate();
            if (!MatchActive()) return InvalidState();
            if (actor == null || !actor.IsCombatCapable || container == null) return InvalidActor();
            var radius = Mathf.Max(0.5f, interactionRadius);
            if ((actor.transform.position - container.transform.position).sqrMagnitude > radius * radius)
                return new AuthorityRequestResult(AuthorityRequestStatus.OutOfRange, "Loot fora de alcance");
            var transfer = container.TakeById(entryId, actor);
            return transfer.Success ? Accepted() : Rejected(transfer.FailureReason);
        }

        public AuthorityRequestResult RequestCollectWorldLoot(long requestId, BRParticipant actor,
            LootPickup pickup, float interactionRadius)
        {
            if (!BeginRequest(requestId)) return Duplicate();
            if (!MatchActive()) return InvalidState();
            if (actor == null || !actor.IsCombatCapable || pickup == null || pickup.Collected)
                return InvalidActor();
            var radius = Mathf.Max(0.5f, interactionRadius);
            if ((actor.transform.position - pickup.transform.position).sqrMagnitude > radius * radius)
                return new AuthorityRequestResult(AuthorityRequestStatus.OutOfRange, "Loot fora de alcance");
            var transfer = pickup.CollectQuantitative(actor);
            return transfer.Success ? Accepted() : Rejected(transfer.FailureReason);
        }

        public AuthorityRequestResult RequestAirdrop(long requestId)
        {
            if (!BeginRequest(requestId)) return Duplicate();
            if (!MatchActive()) return InvalidState();
            var owner = ResolveMatch();
            return owner.Airdrops != null && owner.Airdrops.SpawnAirdrop()
                ? Accepted() : Rejected("Airdrop indisponivel");
        }

        public void ResetSession()
        {
            processedRequests.Clear();
            requestOrder.Clear();
        }

        private MatchManager ResolveMatch()
        {
            if (match == null) match = GetComponent<MatchManager>();
            return match;
        }

        private bool MatchActive()
        {
            var owner = ResolveMatch();
            return owner != null && owner.State == MatchState.Active;
        }

        private bool BeginRequest(long requestId)
        {
            if (!processedRequests.Add(requestId)) return false;
            requestOrder.Enqueue(requestId);
            while (requestOrder.Count > MaxRememberedRequests)
                processedRequests.Remove(requestOrder.Dequeue());
            return true;
        }

        private static AuthorityRequestResult Accepted() =>
            new(AuthorityRequestStatus.Accepted);
        private static AuthorityRequestResult Duplicate() =>
            new(AuthorityRequestStatus.Duplicate, "Solicitacao duplicada");
        private static AuthorityRequestResult InvalidState() =>
            new(AuthorityRequestStatus.InvalidState, "Partida nao esta ativa");
        private static AuthorityRequestResult InvalidActor() =>
            new(AuthorityRequestStatus.InvalidActor, "Participante invalido");
        private static AuthorityRequestResult Rejected(string reason) =>
            new(AuthorityRequestStatus.Rejected, reason);
    }
}
