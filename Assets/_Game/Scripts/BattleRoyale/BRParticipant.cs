using UnityEngine;

namespace BattleRoyale
{
    [RequireComponent(typeof(BRCharacterMotor), typeof(HealthArmorSystem), typeof(InventorySystem))]
    [RequireComponent(typeof(WeaponSystem), typeof(BackpackEquipment))]
    [RequireComponent(typeof(MatchCurrencyWallet))]
    public sealed class BRParticipant : MonoBehaviour
    {
        private static int nextLegacyTeamId = int.MinValue;

        public string DisplayName { get; private set; }
        public string PlayerId { get; private set; }
        public bool IsPlayer { get; private set; }
        public int TeamId { get; private set; }
        public ParticipantPhase Phase { get; private set; } = ParticipantPhase.WaitingPlane;
        public Vector3 SeatOffset { get; private set; }
        public BRCharacterMotor Motor { get; private set; }
        public HealthArmorSystem Health { get; private set; }
        public InventorySystem Inventory { get; private set; }
        public WeaponSystem Weapon { get; private set; }
        public MatchCurrencyWallet Wallet { get; private set; }
        public BackpackEquipment Backpack { get; private set; }
        public int Eliminations { get; set; }
        public int Assists { get; set; }
        public int DamageDealt { get; set; }
        public int Revives { get; set; }
        public MatchPlayerStats Stats { get; private set; }
        public PlayerLifeState LifeState => Health == null ? PlayerLifeState.Dead
            : Health.IsDead ? PlayerLifeState.Dead
            : Health.IsDowned ? PlayerLifeState.Knocked : PlayerLifeState.Alive;
        public bool IsDowned => Health != null && Health.IsDowned;
        public bool IsEliminated => Health != null && Health.IsDead;
        public bool IsCombatCapable => Health != null && !Health.IsDead && !Health.IsDowned;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable() => ParticipantRegistry.Register(this);

        private void OnDisable() => ParticipantRegistry.Unregister(this);

        private void CacheComponents()
        {
            Motor = GetComponent<BRCharacterMotor>();
            Health = GetComponent<HealthArmorSystem>();
            Inventory = GetComponent<InventorySystem>();
            Weapon = GetComponent<WeaponSystem>();
            Wallet = GetComponent<MatchCurrencyWallet>();
            Backpack = GetComponent<BackpackEquipment>();
            Stats = GetComponent<MatchPlayerStats>();
            BindHealthEvents();
        }

        private void BindHealthEvents()
        {
            if (Health == null) return;
            Health.Downed -= OnDowned;
            Health.Revived -= OnRevived;
            Health.Died -= OnDied;
            Health.Downed += OnDowned;
            Health.Revived += OnRevived;
            Health.Died += OnDied;
        }

        private void OnDowned(HealthArmorSystem health) => SetPhase(ParticipantPhase.Downed);

        private void OnRevived(HealthArmorSystem health) => SetPhase(ParticipantPhase.Grounded);

        private void OnDied(HealthArmorSystem health) => Phase = ParticipantPhase.Eliminated;

        private void OnDestroy()
        {
            if (Health == null) return;
            Health.Downed -= OnDowned;
            Health.Revived -= OnRevived;
            Health.Died -= OnDied;
        }

        public void Configure(string displayName, bool isPlayer, int seatIndex)
        {
            Configure(displayName, isPlayer, seatIndex, nextLegacyTeamId++);
        }

        public void Configure(string displayName, bool isPlayer, int seatIndex, int teamId)
        {
            CacheComponents();
            DisplayName = displayName;
            PlayerId = $"player-{Mathf.Max(0, seatIndex):D3}";
            IsPlayer = isPlayer;
            TeamId = teamId;
            SeatOffset = new Vector3((seatIndex % 5 - 2) * 1.15f, 0f, (seatIndex / 5 - 2) * 1.15f);
        }

        public void SetPhase(ParticipantPhase phase)
        {
            Phase = phase;
            if (phase == ParticipantPhase.Eliminated) gameObject.SetActive(false);
        }
    }
}
