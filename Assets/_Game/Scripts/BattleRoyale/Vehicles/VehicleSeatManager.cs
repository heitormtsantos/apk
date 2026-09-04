using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    [Serializable]
    public sealed class VehicleSeat
    {
        [SerializeField] private string seatId;
        [SerializeField] private Transform seatPoint;
        [SerializeField] private Transform[] exitPoints = Array.Empty<Transform>();
        [SerializeField] private bool driver;

        public string SeatId => seatId;
        public Transform SeatPoint => seatPoint;
        public Transform[] ExitPoints => exitPoints;
        public bool IsDriver => driver;
        public BRParticipant Occupant { get; internal set; }
        public bool Available => Occupant == null && seatPoint != null;

        public VehicleSeat(string id, Transform point, Transform[] exits, bool isDriver)
        {
            seatId = id;
            seatPoint = point;
            exitPoints = exits ?? Array.Empty<Transform>();
            driver = isDriver;
        }
    }

    public sealed class VehicleSeatManager : MonoBehaviour
    {
        private static readonly List<VehicleSeatManager> Active = new();
        private readonly Dictionary<BRParticipant, OccupantVisualState> occupantVisuals = new();
        [SerializeField, Min(1f)] private float interactionRange = 3.2f;
        [SerializeField] private VehicleSeat[] seats = Array.Empty<VehicleSeat>();
        [SerializeField] private VehicleController controller;

        public event Action<BRParticipant, VehicleSeat> OccupantEntered;
        public event Action<BRParticipant, VehicleSeat> OccupantExited;
        public IReadOnlyList<VehicleSeat> Seats => seats;
        public VehicleSeat DriverSeat => FindDriverSeat();
        public BRParticipant Driver => DriverSeat?.Occupant;
        public bool HasDriver => Driver != null;
        public float InteractionRange => interactionRange;

        private void Awake() => controller ??= GetComponent<VehicleController>();
        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void Update()
        {
            if (seats == null) return;
            foreach (var seat in seats)
            {
                var occupant = seat?.Occupant;
                if (occupant == null || occupant.gameObject.activeInHierarchy && !occupant.IsEliminated) continue;
                seat.Occupant = null;
                var fallback = transform.position + transform.right * (seat.IsDriver ? -2.2f : 2.2f) + Vector3.up;
                Detach(occupant, fallback);
                OccupantExited?.Invoke(occupant, seat);
            }
            controller?.SetControlsEnabled(Driver != null);
        }
        private void OnDisable()
        {
            ForceExitAll();
            Active.Remove(this);
        }

        public void Configure(VehicleController vehicleController, VehicleSeat[] vehicleSeats,
            float range = 3.2f)
        {
            controller = vehicleController;
            seats = vehicleSeats ?? Array.Empty<VehicleSeat>();
            interactionRange = Mathf.Max(1f, range);
        }

        public static VehicleSeatManager FindNearest(Vector3 position, float maximumDistance)
        {
            VehicleSeatManager nearest = null;
            var best = maximumDistance * maximumDistance;
            foreach (var vehicle in Active)
            {
                if (vehicle == null || vehicle.GetComponent<VehicleHealth>()?.IsDestroyed == true) continue;
                var distance = (vehicle.transform.position - position).sqrMagnitude;
                if (distance > best || !vehicle.HasAvailableSeat()) continue;
                nearest = vehicle;
                best = distance;
            }
            return nearest;
        }

        public static VehicleSeatManager FindForOccupant(BRParticipant participant)
        {
            if (participant == null) return null;
            foreach (var vehicle in Active)
                if (vehicle != null && vehicle.GetSeat(participant) != null) return vehicle;
            return null;
        }

        public bool RequestEnterVehicle(BRParticipant participant) => EnterVehicleAuthoritative(participant);

        public bool EnterVehicleAuthoritative(BRParticipant participant)
        {
            if (participant == null || !participant.IsCombatCapable || GetSeat(participant) != null) return false;
            if ((participant.transform.position - transform.position).sqrMagnitude > interactionRange * interactionRange)
                return false;
            var seat = FirstAvailableSeat();
            if (seat == null) return false;
            seat.Occupant = participant;
            Attach(participant, seat);
            controller?.SetControlsEnabled(Driver != null);
            OccupantEntered?.Invoke(participant, seat);
            return true;
        }

        public bool RequestExitVehicle(BRParticipant participant) => ExitVehicleAuthoritative(participant);

        public bool ExitVehicleAuthoritative(BRParticipant participant)
        {
            var seat = GetSeat(participant);
            if (seat == null || !TryFindExit(seat, participant, out var exit)) return false;
            seat.Occupant = null;
            Detach(participant, exit);
            controller?.SetControlsEnabled(Driver != null);
            OccupantExited?.Invoke(participant, seat);
            return true;
        }

        public bool TryChangeSeat(BRParticipant participant)
        {
            var current = GetSeat(participant);
            if (current == null) return false;
            VehicleSeat target = null;
            foreach (var seat in seats)
                if (seat != null && seat.Available && (target == null || seat.IsDriver)) target = seat;
            if (target == null) return false;
            current.Occupant = null;
            target.Occupant = participant;
            Attach(participant, target);
            controller?.SetControlsEnabled(Driver != null);
            return true;
        }

        public void ForceExitAll()
        {
            if (seats == null) return;
            foreach (var seat in seats)
            {
                var occupant = seat?.Occupant;
                if (occupant == null) continue;
                seat.Occupant = null;
                var fallback = transform.position + transform.right * (seat.IsDriver ? -2.2f : 2.2f) + Vector3.up;
                Detach(occupant, fallback);
                OccupantExited?.Invoke(occupant, seat);
            }
            controller?.SetControlsEnabled(false);
        }

        public VehicleSeat GetSeat(BRParticipant participant)
        {
            if (seats == null) return null;
            foreach (var seat in seats) if (seat?.Occupant == participant) return seat;
            return null;
        }

        private bool HasAvailableSeat()
        {
            if (seats == null) return false;
            foreach (var seat in seats) if (seat != null && seat.Available) return true;
            return false;
        }

        private VehicleSeat FirstAvailableSeat()
        {
            var driver = FindDriverSeat();
            if (driver?.Available == true) return driver;
            foreach (var seat in seats) if (seat != null && seat.Available) return seat;
            return null;
        }

        private VehicleSeat FindDriverSeat()
        {
            if (seats == null) return null;
            foreach (var seat in seats) if (seat?.IsDriver == true) return seat;
            return null;
        }

        private void Attach(BRParticipant participant, VehicleSeat seat)
        {
            var character = participant.GetComponent<CharacterController>();
            if (character != null) character.enabled = false;
            participant.Motor?.StopMotion();
            var renderers = participant.GetComponentsInChildren<Renderer>(true);
            var states = new bool[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                states[i] = renderers[i].enabled;
                renderers[i].enabled = false;
            }
            occupantVisuals[participant] = new OccupantVisualState(renderers, states);
            participant.transform.SetParent(seat.SeatPoint, false);
            participant.transform.localPosition = Vector3.zero;
            participant.transform.localRotation = Quaternion.identity;
        }

        private void Detach(BRParticipant participant, Vector3 position)
        {
            participant.transform.SetParent(null, true);
            participant.transform.position = position;
            participant.transform.rotation = Quaternion.Euler(0f, participant.transform.eulerAngles.y, 0f);
            var character = participant.GetComponent<CharacterController>();
            if (character != null) character.enabled = true;
            participant.Motor?.Teleport(position);
            if (!occupantVisuals.Remove(participant, out var visualState)) return;
            visualState.Restore();
        }

        private bool TryFindExit(VehicleSeat seat, BRParticipant participant, out Vector3 position)
        {
            var radius = 0.35f;
            var height = 1.45f;
            foreach (var point in seat.ExitPoints)
            {
                if (point == null) continue;
                var basePoint = point.position + Vector3.up * radius;
                var top = basePoint + Vector3.up * Mathf.Max(0f, height - radius * 2f);
                var blocked = false;
                foreach (var overlap in Physics.OverlapCapsule(basePoint, top, radius, ~0,
                             QueryTriggerInteraction.Ignore))
                {
                    if (overlap == null || overlap.transform.IsChildOf(transform)
                        || overlap.transform.IsChildOf(participant.transform)) continue;
                    blocked = true;
                    break;
                }
                if (!blocked)
                {
                    position = point.position;
                    return true;
                }
            }
            position = participant.transform.position;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
            if (seats == null) return;
            foreach (var seat in seats)
            {
                if (seat?.SeatPoint != null)
                {
                    Gizmos.color = seat.IsDriver ? Color.cyan : Color.green;
                    Gizmos.DrawWireCube(seat.SeatPoint.position, Vector3.one * 0.25f);
                }
                foreach (var exit in seat?.ExitPoints ?? Array.Empty<Transform>())
                {
                    if (exit == null) continue;
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(exit.position, 0.18f);
                }
            }
        }

        private readonly struct OccupantVisualState
        {
            private readonly Renderer[] renderers;
            private readonly bool[] enabledStates;

            public OccupantVisualState(Renderer[] values, bool[] states)
            {
                renderers = values;
                enabledStates = states;
            }

            public void Restore()
            {
                for (var i = 0; i < renderers.Length && i < enabledStates.Length; i++)
                    if (renderers[i] != null) renderers[i].enabled = enabledStates[i];
            }
        }
    }
}
