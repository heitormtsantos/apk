#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class RuntimeMapOpeningDiscovery : MonoBehaviour
    {
        private const string MapName = "User Supplied Clock Tower Map";
        private const float Radius = 0.24f;
        private const float Height = CharacterPresentationProfile.ControllerHeight;
        private const float Skin = 0.035f;
        private readonly Collider[] overlapBuffer = new Collider[64];
        private Transform mapRoot;
        private Collider[] mapColliders;

        private IEnumerator Start()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            yield return new WaitForFixedUpdate();
            yield return null;
            var map = GameObject.Find(MapName);
            if (map == null)
            {
                Debug.LogError("BR_OPENING_SCAN RESULT=FAIL REASON=MAP_MISSING");
                Application.Quit(21);
                yield break;
            }

            mapRoot = map.transform;
            mapColliders = map.GetComponentsInChildren<Collider>(true)
                .Where(collider => collider != null && collider.enabled).ToArray();
            Physics.queriesHitBackfaces = true;
            Physics.SyncTransforms();
            var bounds = CombinedBounds(mapColliders);
            Debug.Log($"BR_OPENING_SCAN START MAP={MapName} COLLIDERS={mapColliders.Length} " +
                      $"BOUNDS_CENTER={V(bounds.center)} BOUNDS_SIZE={V(bounds.size)}");
            foreach (var collider in mapColliders)
                Debug.Log($"BR_MAP_COLLIDER PATH={Path(collider.transform)} TYPE={collider.GetType().Name} " +
                          $"CENTER={V(collider.bounds.center)} SIZE={V(collider.bounds.size)}");

            var doors = ScanDoors(bounds);
            var windows = ScanWindows(bounds);
            Debug.Log($"BR_OPENING_SCAN RESULT={(doors > 1 && windows > 0 ? "PASS" : "FAIL")} " +
                      $"DOORS={doors} WINDOWS={windows}");
            Application.Quit(doors > 1 && windows > 0 ? 0 : 22);
        }

        private int ScanDoors(Bounds bounds)
        {
            var candidates = new List<Candidate>();
            var groundCount = 0;
            var endpointCount = 0;
            var clearCount = 0;
            var pathCount = 0;
            var sidesCount = 0;
            var topCount = 0;
            var dimensionCount = 0;
            const float grid = 0.12f;
            var scanMin = Vector3.Max(bounds.min, new Vector3(-34f, bounds.min.y, -34f));
            var scanMax = Vector3.Min(bounds.max, new Vector3(34f, bounds.max.y, 34f));
            for (var x = scanMin.x; x <= scanMax.x; x += grid)
            for (var z = scanMin.z; z <= scanMax.z; z += grid)
            {
                if (!TryGround(new Vector3(x, bounds.max.y + 2f, z), out var ground, out var groundCollider)) continue;
                groundCount++;
                foreach (var forward in new[] { Vector3.right, Vector3.forward })
                {
                    var midpoint = ground;
                    var outside = midpoint - forward * 0.55f;
                    var inside = midpoint + forward * 0.55f;
                    if (!TryGround(outside + Vector3.up * 3f, out outside, out var outsideGround) ||
                        !TryGround(inside + Vector3.up * 3f, out inside, out var insideGround) ||
                        Mathf.Abs(outside.y - inside.y) > 0.16f) continue;
                    endpointCount++;
                    if (!CapsuleClear(outside) || !CapsuleClear(inside)) continue;
                    clearCount++;
                    if (!CapsulePathClear(outside, inside)) continue;
                    pathCount++;

                    midpoint.y = (outside.y + inside.y) * 0.5f;
                    var right = Vector3.Cross(Vector3.up, forward);
                    if (!MapRay(midpoint + Vector3.up * 0.9f, right, 1.15f, out var rightHit) ||
                        !MapRay(midpoint + Vector3.up * 0.9f, -right, 1.15f, out var leftHit)) continue;
                    sidesCount++;
                    var width = rightHit.distance + leftHit.distance;
                    if (width < 0.58f || width > 1.65f) continue;
                    if (!MapRay(midpoint + Vector3.up * 0.25f, Vector3.up, 2.7f, out var topHit)) continue;
                    topCount++;
                    var clearHeight = topHit.point.y - midpoint.y;
                    var ratio = CharacterPresentationProfile.VisualHeight / clearHeight;
                    if (ratio < 0.80f || ratio > 0.85f) continue;
                    dimensionCount++;

                    var candidate = new Candidate(midpoint, outside, inside, width, clearHeight,
                        groundCollider, leftHit.collider, rightHit.collider, topHit.collider,
                        outsideGround, insideGround);
                    if (candidates.Any(existing => Vector3.Distance(existing.Midpoint, midpoint) < 0.55f)) continue;
                    candidates.Add(candidate);
                    Debug.Log($"BR_DOOR_CANDIDATE INDEX={candidates.Count} FIXTURE=False MID={V(midpoint)} " +
                              $"OUTSIDE={V(outside)} INSIDE={V(inside)} WIDTH={width:F3} CLEAR_HEIGHT={clearHeight:F3} " +
                              $"RATIO={ratio:F3} GROUND={Path(groundCollider.transform)} " +
                              $"LEFT={Path(leftHit.collider.transform)} RIGHT={Path(rightHit.collider.transform)} " +
                              $"TOP={Path(topHit.collider.transform)} OUT_GROUND={Path(outsideGround.transform)} " +
                              $"IN_GROUND={Path(insideGround.transform)}");
                }
            }
            Debug.Log($"BR_DOOR_SCAN_STATS GROUND={groundCount} ENDPOINTS={endpointCount} CLEAR={clearCount} " +
                      $"PATH={pathCount} SIDES={sidesCount} TOP={topCount} DIMENSIONS={dimensionCount}");
            return candidates.Count;
        }

        private int ScanWindows(Bounds bounds)
        {
            var candidates = new List<Candidate>();
            var outsideCount = 0;
            var endpointsCount = 0;
            var apertureCount = 0;
            var frameCount = 0;
            var arcCount = 0;
            const float grid = 0.12f;
            var scanMin = Vector3.Max(bounds.min, new Vector3(-34f, bounds.min.y, -34f));
            var scanMax = Vector3.Min(bounds.max, new Vector3(34f, bounds.max.y, 34f));
            foreach (var forward in new[]
                     {
                         Vector3.right, new Vector3(1f, 0f, 1f).normalized, Vector3.forward,
                         new Vector3(-1f, 0f, 1f).normalized, Vector3.left,
                         new Vector3(-1f, 0f, -1f).normalized, Vector3.back,
                         new Vector3(1f, 0f, -1f).normalized
                     })
            for (var x = scanMin.x; x <= scanMax.x; x += grid)
            for (var z = scanMin.z; z <= scanMax.z; z += grid)
            {
                if (!TryGround(new Vector3(x, bounds.max.y + 2f, z), out var outside, out var outsideGround) ||
                    !CapsuleClear(outside)) continue;
                outsideCount++;
                var insideProbe = outside + forward * 1.33f + Vector3.up * 3f;
                if (!TryGround(insideProbe, out var inside, out var insideGround) || !CapsuleClear(inside) ||
                    Mathf.Abs(outside.y - inside.y) > 0.18f) continue;
                endpointsCount++;
                var distance = Vector3.Distance(outside, inside);
                if (!MapRay(outside + Vector3.up * BRCharacterMotor.LowVaultProbeHeight, forward,
                        BRCharacterMotor.VaultForwardProbeDistance, out var obstacleHit)) continue;
                if (MapRay(outside + Vector3.up * BRCharacterMotor.HighVaultProbeHeight, forward,
                        BRCharacterMotor.VaultForwardProbeDistance, out _)) continue;
                apertureCount++;
                var aperture = obstacleHit.point + forward * 0.04f;
                aperture.y = outside.y + 1.05f;
                if (!MapRay(aperture, Vector3.down, 1.0f, out var sillHit) ||
                    !MapRay(aperture, Vector3.up, 1.3f, out var topHit)) continue;
                var right = Vector3.Cross(Vector3.up, forward);
                if (!MapRay(aperture, right, 2.5f, out var rightHit) ||
                    !MapRay(aperture, -right, 2.5f, out var leftHit)) continue;
                frameCount++;
                var arcOutsideProbe = outside - forward * 0.25f + Vector3.up * 3f;
                if (!TryGround(arcOutsideProbe, out var arcOutside, out outsideGround) ||
                    !TryGround(arcOutside + forward * 1.58f + Vector3.up * 3f, out var arcInside,
                        out insideGround) || !CapsuleClear(arcOutside) || !CapsuleClear(arcInside)) continue;
                var width = rightHit.distance + leftHit.distance;
                var clearBottom = sillHit.point.y - arcOutside.y;
                var clearTop = topHit.point.y - arcOutside.y;
                if (width < 0.48f || width > 3.5f || !VaultArcClear(arcOutside, arcInside)) continue;
                arcCount++;
                if (clearBottom < 0.20f || clearBottom > 0.95f || clearTop < 1.2f || clearTop > 2.3f) continue;

                var candidate = new Candidate(aperture, arcOutside, arcInside, width, clearTop - clearBottom,
                    sillHit.collider, leftHit.collider, rightHit.collider, topHit.collider,
                    outsideGround, insideGround);
                if (candidates.Any(existing => Vector3.Distance(existing.Midpoint, aperture) < 0.6f)) continue;
                candidates.Add(candidate);
                Debug.Log($"BR_WINDOW_CANDIDATE INDEX={candidates.Count} FIXTURE=False MID={V(aperture)} " +
                          $"OUTSIDE={V(arcOutside)} INSIDE={V(arcInside)} WIDTH={width:F3} SILL={clearBottom:F3} " +
                          $"TOP={clearTop:F3} CLEAR_HEIGHT={clearTop - clearBottom:F3} " +
                          $"SILL_PATH={Path(sillHit.collider.transform)} LEFT={Path(leftHit.collider.transform)} " +
                          $"RIGHT={Path(rightHit.collider.transform)} TOP_PATH={Path(topHit.collider.transform)} " +
                          $"OUT_GROUND={Path(outsideGround.transform)} IN_GROUND={Path(insideGround.transform)}");
            }
            Debug.Log($"BR_WINDOW_SCAN_STATS OUTSIDE={outsideCount} ENDPOINTS={endpointsCount} " +
                      $"APERTURE={apertureCount} FRAME={frameCount} ARC={arcCount}");
            return candidates.Count;
        }

        private bool TryGround(Vector3 origin, out Vector3 ground, out Collider groundCollider)
        {
            var hits = Physics.RaycastAll(origin, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits.Where(hit => hit.point.y <= 6.5f).OrderBy(hit => hit.distance))
            {
                if (!IsMap(hit.collider) || hit.normal.y < 0.45f) continue;
                ground = hit.point;
                groundCollider = hit.collider;
                return true;
            }
            ground = default;
            groundCollider = null;
            return false;
        }

        private bool CapsuleClear(Vector3 feet)
        {
            Capsule(feet, out var bottom, out var top);
            var count = Physics.OverlapCapsuleNonAlloc(bottom, top, Radius - Skin, overlapBuffer, ~0,
                QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
                if (IsMap(overlapBuffer[i])) return false;
            return true;
        }

        private bool CapsulePathClear(Vector3 from, Vector3 to)
        {
            Capsule(from, out var bottom, out var top);
            var delta = to - from;
            return !Physics.CapsuleCast(bottom, top, Radius - Skin, delta.normalized, out var hit, delta.magnitude,
                ~0, QueryTriggerInteraction.Ignore) || !IsMap(hit.collider);
        }

        private bool VaultArcClear(Vector3 from, Vector3 to)
        {
            const int steps = 24;
            var previous = from;
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var point = Vector3.Lerp(from, to, t) + Vector3.up *
                    (Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.65f) * BRCharacterMotor.CompactVaultArcHeight);
                if (!CompactCapsuleClear(point)) return false;
                if (i > 0)
                {
                    CompactCapsule(previous, out var bottom, out var top, out var radius);
                    var delta = point - previous;
                    var hits = Physics.CapsuleCastAll(bottom, top, radius, delta.normalized, delta.magnitude,
                        ~0, QueryTriggerInteraction.Ignore);
                    if (hits.Any(hit => IsMap(hit.collider))) return false;
                }
                previous = point;
            }
            return true;
        }

        private bool CompactCapsuleClear(Vector3 feet)
        {
            CompactCapsule(feet, out var bottom, out var top, out var radius);
            var count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlapBuffer, ~0,
                QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
                if (IsMap(overlapBuffer[i])) return false;
            return true;
        }

        private static void CompactCapsule(Vector3 feet, out Vector3 bottom, out Vector3 top, out float radius)
        {
            radius = BRCharacterMotor.CompactVaultRadius;
            var halfHeight = BRCharacterMotor.CompactVaultHeight * 0.5f - 0.01f;
            var segment = halfHeight - radius;
            var center = feet + Vector3.up * (BRCharacterMotor.CompactVaultHeight * 0.5f
                + BRCharacterMotor.CompactVaultGroundClearance);
            bottom = center - Vector3.up * segment;
            top = center + Vector3.up * segment;
        }

        private bool MapRay(Vector3 origin, Vector3 direction, float distance, out RaycastHit mapHit)
        {
            foreach (var hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore)
                         .OrderBy(hit => hit.distance))
            {
                if (!IsMap(hit.collider)) continue;
                mapHit = hit;
                return true;
            }
            mapHit = default;
            return false;
        }

        private void Capsule(Vector3 feet, out Vector3 bottom, out Vector3 top)
        {
            bottom = feet + Vector3.up * (Radius + Skin);
            top = feet + Vector3.up * (Height - Radius);
        }

        private bool IsMap(Collider collider) => collider != null && collider.transform.IsChildOf(mapRoot);

        private string Path(Transform transform)
        {
            var names = new Stack<string>();
            while (transform != null)
            {
                names.Push(transform.name);
                if (transform == mapRoot) break;
                transform = transform.parent;
            }
            return string.Join("/", names);
        }

        private static Bounds CombinedBounds(IReadOnlyList<Collider> colliders)
        {
            var bounds = colliders[0].bounds;
            for (var i = 1; i < colliders.Count; i++) bounds.Encapsulate(colliders[i].bounds);
            return bounds;
        }

        private static string V(Vector3 value) => FormattableString.Invariant($"({value.x:F3},{value.y:F3},{value.z:F3})");

        private readonly struct Candidate
        {
            public readonly Vector3 Midpoint;
            public Candidate(Vector3 midpoint, Vector3 outside, Vector3 inside, float width, float height,
                params Collider[] colliders) => Midpoint = midpoint;
        }
    }
}
#endif
