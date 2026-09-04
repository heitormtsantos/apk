using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class DropSystem : MonoBehaviour
    {
        private readonly Dictionary<BRParticipant, Vector3> drift = new();
        private readonly Dictionary<BRParticipant, GameObject> parachutes = new();
        private BRGameConfig config;
        private Vector3 planeStart;
        private Vector3 planeEnd;
        private float planeT;
        private Transform planeVisual;
        private readonly RaycastHit[] recoveryHits = new RaycastHit[24];

        public bool PlaneFinished => planeT >= 1f;
        public float PlaneProgress => planeT;
        public Vector3 PlanePosition => Vector3.Lerp(planeStart, planeEnd, planeT);
        public Vector3 PlaneStart => planeStart;
        public Vector3 PlaneEnd => planeEnd;
        public Vector3 RouteDirection => PlaneDirection();
        public float PlaneForwardAlignment => planeVisual != null ? Vector3.Dot(planeVisual.forward, PlaneDirection()) : 0f;

        public int VisibleWaitingPassengerCount(IEnumerable<BRParticipant> participants)
        {
            var count = 0;
            foreach (var participant in participants)
            {
                if (participant == null || participant.Phase != ParticipantPhase.WaitingPlane) continue;
                var visibility = participant.GetComponent<DeploymentVisibility>();
                if (visibility == null || visibility.Visible) count++;
            }
            return count;
        }

        public void Configure(BRGameConfig newConfig)
        {
            config = newConfig;
            var area = PlayableArea.Bounds;
            var routeMargin = Mathf.Max(8f, Mathf.Max(area.size.x, area.size.z) * 0.015f);
            var terrainClearance = Mathf.Max(90f, Mathf.Max(area.size.x, area.size.z) * 0.055f);
            var routeHeight = Mathf.Max(config.dropHeight, area.max.y + terrainClearance);
            planeStart = new Vector3(area.min.x - routeMargin, routeHeight, area.min.z + area.size.z * 0.18f);
            planeEnd = new Vector3(area.max.x + routeMargin, routeHeight, area.max.z - area.size.z * 0.18f);
            planeT = 0f;
            drift.Clear();
            ClearParachutes();
            EnsurePlaneVisual();
            if (planeVisual != null) planeVisual.gameObject.SetActive(false);
        }

        public void BeginRoute()
        {
            planeT = 0f;
            if (planeVisual != null)
            {
                planeVisual.gameObject.SetActive(true);
                planeVisual.SetPositionAndRotation(planeStart, Quaternion.LookRotation((planeEnd - planeStart).normalized));
            }
        }

        public void PreparePassengers(IEnumerable<BRParticipant> participants)
        {
            foreach (var participant in participants)
            {
                if (participant == null) continue;
                participant.GetComponent<DeploymentVisibility>()?.SetVisible(false);
            }
        }

        public int CountPassengers(IEnumerable<BRParticipant> participants)
        {
            var count = 0;
            foreach (var participant in participants)
                if (participant != null && participant.Phase == ParticipantPhase.WaitingPlane) count++;
            return count;
        }

        public void EjectRemaining(IEnumerable<BRParticipant> participants)
        {
            foreach (var participant in participants)
            {
                if (participant == null || participant.Phase != ParticipantPhase.WaitingPlane) continue;
                participant.transform.position = PlanePosition + participant.SeatOffset;
                var side = participant.IsPlayer ? 0f : (participant.SeatOffset.x >= 0f ? 42f : -42f);
                var direction = Quaternion.Euler(0f, side, 0f) * PlaneDirection();
                BeginDrop(participant, direction);
            }
            if (planeVisual != null) planeVisual.gameObject.SetActive(false);
        }

        public void TickPlane(IEnumerable<BRParticipant> participants, BRParticipant player, bool playerDrop)
        {
            planeT = Mathf.MoveTowards(planeT, 1f, config.planeSpeed * Time.deltaTime / Vector3.Distance(planeStart, planeEnd));
            var planePosition = PlanePosition;
            if (planeVisual != null)
                planeVisual.SetPositionAndRotation(planePosition, Quaternion.LookRotation((planeEnd - planeStart).normalized));

            foreach (var participant in participants)
            {
                if (participant == null || participant.Phase != ParticipantPhase.WaitingPlane) continue;
                participant.GetComponent<DeploymentVisibility>()?.SetVisible(false);
                participant.transform.position = planePosition + participant.SeatOffset;
                participant.transform.rotation = Quaternion.LookRotation(PlaneDirection());
                if (participant == player)
                {
                    if (playerDrop || PlaneFinished) BeginDrop(participant, PlaneDirection());
                }
                else
                {
                    var jumpProbability = planeT > 0.12f && planeT < 0.9f ? Time.deltaTime * 0.72f : 0f;
                    if (PlaneFinished || Random.value < jumpProbability)
                    {
                        var side = Random.value < 0.5f ? -1f : 1f;
                        var direction = Quaternion.Euler(0f, side * Random.Range(25f, 70f), 0f) * PlaneDirection();
                        BeginDrop(participant, direction);
                    }
                }
            }

            if (PlaneFinished && planeVisual != null) planeVisual.gameObject.SetActive(false);
        }

        public void TickFalling(BRParticipant participant, Vector2 moveInput, bool requestParachute) =>
            TickFalling(participant, moveInput, requestParachute, Vector3.forward, Vector3.right);

        public void TickFalling(BRParticipant participant, Vector2 moveInput, bool requestParachute,
            Vector3 referenceForward, Vector3 referenceRight)
        {
            if (participant.Phase is not (ParticipantPhase.Freefall or ParticipantPhase.Parachute)) return;
            var position = participant.transform.position;
            var activeSteering = DeploymentSteering.HasInput(moveInput);
            var planar = DeploymentSteering.CameraRelativeTravel(moveInput, referenceForward,
                referenceRight, participant.transform.forward);
            if (activeSteering && planar.sqrMagnitude > 0.000001f)
                drift[participant] = planar.normalized;
            else if (drift.TryGetValue(participant, out var previousDirection))
                planar = DeploymentSteering.IdleTravel(previousDirection);

            var shouldAutoDeploy = TryFindGroundBelow(position, out var ground)
                && position.y - ground.y <= config.parachuteHeight;
            if (participant.Phase == ParticipantPhase.Freefall && (requestParachute || shouldAutoDeploy))
                OpenParachute(participant);

            var parachuting = participant.Phase == ParticipantPhase.Parachute;
            var fallSpeed = parachuting ? config.parachuteSpeed : config.freefallSpeed;
            var steeringSpeed = parachuting ? 9.5f : 16f;
            var turnSpeed = parachuting ? 150f : 260f;
            position += Vector3.ClampMagnitude(planar, 1f) * steeringSpeed * Time.deltaTime;
            position = ClampToIsland(position);
            position += Vector3.down * fallSpeed * Time.deltaTime;
            if (position.y < -6f)
            {
                if (TryFindRecoveryLanding(out var recoveryLanding))
                {
                    participant.Motor.Teleport(recoveryLanding + Vector3.up * 0.08f);
                    participant.SetPhase(ParticipantPhase.Grounded);
                    CloseParachute(participant);
                    drift.Remove(participant);
                    return;
                }
                var center = PlayableArea.Center;
                position = new Vector3(center.x,
                    PlayableArea.Bounds.max.y + Mathf.Max(14f, config.parachuteHeight), center.z);
                OpenParachute(participant);
            }
            participant.Motor.Teleport(position);
            participant.transform.rotation = DeploymentSteering.SmoothFacing(participant.transform.rotation,
                planar, turnSpeed, Time.deltaTime);
            UpdateParachuteBank(participant, parachuting && activeSteering ? moveInput.x : 0f);

            if (TryFindLanding(position, out var landing) && position.y <= landing.y + 1.35f)
            {
                participant.Motor.Teleport(landing + Vector3.up * 0.08f);
                participant.SetPhase(ParticipantPhase.Grounded);
                CloseParachute(participant);
                drift.Remove(participant);
            }
        }

        private void UpdateParachuteBank(BRParticipant participant, float lateralInput)
        {
            if (!parachutes.TryGetValue(participant, out var visual) || visual == null) return;
            var target = Quaternion.Euler(0f, 0f, -Mathf.Clamp(lateralInput, -1f, 1f) * 8f);
            visual.transform.localRotation = Quaternion.Slerp(visual.transform.localRotation, target,
                7f * Time.deltaTime);
        }

        private Vector3 PlaneDirection() => (planeEnd - planeStart).normalized;

        private Vector3 ClampToIsland(Vector3 position)
        {
            return PlayableArea.Clamp(position, 1f);
        }

        private bool TryFindRecoveryLanding(out Vector3 landing)
        {
            landing = default;
            var center = PlayableArea.Center;
            var area = PlayableArea.Bounds;
            for (var ring = 0; ring <= 3; ring++)
            {
                var radius = ring * 10f;
                var samples = ring == 0 ? 1 : 8;
                for (var sample = 0; sample < samples; sample++)
                {
                    var angle = sample * Mathf.PI * 2f / samples + ring * 0.37f;
                    var planar = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    var origin = center + planar;
                    origin.y = area.max.y + 40f;
                    var count = Physics.RaycastNonAlloc(origin, Vector3.down, recoveryHits,
                        area.size.y + 100f, ~0, QueryTriggerInteraction.Ignore);
                    var found = false;
                    var highest = float.NegativeInfinity;
                    for (var i = 0; i < count; i++)
                    {
                        var hit = recoveryHits[i];
                        if (hit.collider == null || IsWater(hit.collider) || hit.normal.y < 0.58f) continue;
                        if (hit.collider.GetComponentInParent<BRParticipant>() != null
                            || hit.point.y < area.min.y - 2f || hit.point.y > area.max.y + 2f) continue;
                        if (hit.point.y <= highest) continue;
                        highest = hit.point.y;
                        landing = hit.point;
                        found = true;
                    }
                    if (found) return true;
                }
            }
            return false;
        }

        private void BeginDrop(BRParticipant participant, Vector3 direction)
        {
            var ejectPosition = participant.transform.position - PlaneDirection() * 2.4f + Vector3.down * 2.2f;
            participant.GetComponent<DeploymentVisibility>()?.SetVisible(true);
            participant.Motor.Teleport(ejectPosition);
            participant.SetPhase(ParticipantPhase.Freefall);
            drift[participant] = new Vector3(direction.x, 0f, direction.z).normalized;
            participant.transform.rotation = Quaternion.LookRotation(drift[participant]);
        }

        private void OpenParachute(BRParticipant participant)
        {
            if (participant.Phase != ParticipantPhase.Freefall) return;
            participant.SetPhase(ParticipantPhase.Parachute);
            if (parachutes.ContainsKey(participant)) return;

            var root = new GameObject($"Parachute - {participant.DisplayName}");
            root.transform.SetParent(participant.transform, false);
            root.transform.localPosition = Vector3.up * 2.9f;
            var canopy = ImportedGameplayVisuals.InstantiateTextured(
                ImportedGameplayVisuals.ParachuteModel, ImportedGameplayVisuals.ParachuteTextureRoot,
                root.transform, "Imported Parachute Canopy", 0.08f, 0.30f);
            if (canopy != null)
            {
                NormalizeModel(canopy.transform, 4.3f);
            }
            else CreateFallbackCanopy(root.transform);
            CreateSuspensionLines(root.transform);
            parachutes[participant] = root;
        }

        private bool TryFindLanding(Vector3 position, out Vector3 landing)
        {
            var origin = position + Vector3.up * 1.5f;
            var hits = Physics.SphereCastAll(origin, 0.28f, Vector3.down, 5.5f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null || IsWater(hit.collider)) continue;
                if (hit.collider.GetComponentInParent<BRParticipant>() != null) continue;
                landing = hit.point;
                return true;
            }
            landing = default;
            return false;
        }

        private static bool TryFindGroundBelow(Vector3 position, out Vector3 ground)
        {
            var area = PlayableArea.Bounds;
            var distance = Mathf.Max(8f, position.y - area.min.y + 4f);
            var hits = Physics.RaycastAll(position + Vector3.up * 0.5f, Vector3.down, distance, ~0,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null || IsWater(hit.collider)
                    || hit.collider.GetComponentInParent<BRParticipant>() != null || hit.normal.y < 0.45f) continue;
                ground = hit.point;
                return true;
            }
            ground = default;
            return false;
        }

        private static bool IsWater(Collider collider) => collider != null && collider.gameObject.name.Contains("Water");

        private void EnsurePlaneVisual()
        {
            if (planeVisual != null) return;
            var root = new GameObject("Deployment Aircraft").transform;
            root.SetParent(transform);
            var prefab = Resources.Load<GameObject>("Aircraft/CargoPlane_GameplayVisual");
            if (prefab != null)
            {
                var aircraft = Instantiate(prefab, root, false);
                aircraft.name = "Imported Deployment Cargo Plane";
                BRAssetVisuals.PrepareForUrp(aircraft);
                aircraft.transform.localRotation = Quaternion.identity;
                NormalizeModel(aircraft.transform, 16f);
                foreach (var collider in aircraft.GetComponentsInChildren<Collider>()) Destroy(collider);
            }
            else CreateFallbackPlane(root);
            planeVisual = root;
        }

        private static void CreateFallbackPlane(Transform root)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Aircraft Body";
            body.transform.SetParent(root, false);
            body.transform.localScale = new Vector3(2.4f, 1.1f, 9f);
            body.GetComponent<Renderer>().sharedMaterial = BRMaterialFactory.Create("Aircraft Dark", new Color(0.13f, 0.15f, 0.18f));
            Destroy(body.GetComponent<Collider>());
            var wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = "Aircraft Wing";
            wing.transform.SetParent(root, false);
            wing.transform.localScale = new Vector3(13f, 0.25f, 2.1f);
            wing.GetComponent<Renderer>().sharedMaterial = BRMaterialFactory.Create("Aircraft Wing", new Color(0.18f, 0.21f, 0.24f));
            Destroy(wing.GetComponent<Collider>());
        }

        private static void CreateFallbackCanopy(Transform root)
        {
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Fallback Parachute Canopy";
            canopy.transform.SetParent(root, false);
            canopy.transform.localPosition = Vector3.up * 0.25f;
            canopy.transform.localScale = new Vector3(4.4f, 0.85f, 2.6f);
            canopy.GetComponent<Renderer>().sharedMaterial = BRMaterialFactory.Create("Parachute Fabric", new Color(0.08f, 0.42f, 0.52f));
            Destroy(canopy.GetComponent<Collider>());
        }

        private static void CreateSuspensionLines(Transform root)
        {
            var material = BRMaterialFactory.Create("Parachute Lines", new Color(0.86f, 0.88f, 0.82f));
            for (var i = 0; i < 4; i++)
            {
                var line = new GameObject($"Suspension Line {i + 1}").AddComponent<LineRenderer>();
                line.transform.SetParent(root, false);
                line.useWorldSpace = false;
                line.positionCount = 2;
                var x = i < 2 ? -1.55f : 1.55f;
                var z = i % 2 == 0 ? -0.65f : 0.65f;
                line.SetPosition(0, new Vector3(x, 0f, z));
                line.SetPosition(1, new Vector3(i < 2 ? -0.22f : 0.22f, -2.35f, 0f));
                line.startWidth = 0.018f;
                line.endWidth = 0.018f;
                line.sharedMaterial = material;
            }
        }

        private static void NormalizeModel(Transform visual, float targetSize)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest > 0.001f) visual.localScale *= targetSize / largest;

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (visual.parent != null) visual.position += visual.parent.position - bounds.center;
        }

        private void CloseParachute(BRParticipant participant)
        {
            if (!parachutes.TryGetValue(participant, out var visual)) return;
            if (visual != null) Destroy(visual);
            parachutes.Remove(participant);
        }

        private void ClearParachutes()
        {
            foreach (var visual in parachutes.Values)
                if (visual != null) Destroy(visual);
            parachutes.Clear();
        }
    }
}
