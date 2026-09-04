using UnityEngine;

namespace BattleRoyale
{
    public static class AimSolver
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[96];

        public static AimSolution Solve(Ray viewRay, Vector3 muzzlePosition, float range,
            Vector2 spreadDegrees, GameObject owner, LayerMask hitMask, BRParticipant assistedTarget = null)
        {
            var safeRange = Mathf.Max(1f, range);
            var viewDirection = ApplySpread(viewRay.direction, spreadDegrees);
            var cameraRay = new Ray(viewRay.origin, viewDirection);
            var aimPoint = cameraRay.origin + cameraRay.direction * safeRange;
            if (TryFirstValidHit(cameraRay, safeRange, owner, hitMask, out var cameraHit))
                aimPoint = cameraHit.point;

            var toAimPoint = aimPoint - muzzlePosition;
            var muzzleDirection = toAimPoint.sqrMagnitude > 0.0001f
                ? toAimPoint.normalized
                : cameraRay.direction;
            var desiredDistance = Mathf.Min(safeRange, Mathf.Max(1f, toAimPoint.magnitude + 0.5f));
            var muzzleRay = new Ray(muzzlePosition, muzzleDirection);
            var endPoint = muzzlePosition + muzzleDirection * desiredDistance;
            var obstructed = false;
            if (TryFirstValidHit(muzzleRay, desiredDistance, owner, hitMask, out var muzzleHit))
            {
                endPoint = muzzleHit.point;
                desiredDistance = muzzleHit.distance;
                obstructed = Vector3.Distance(muzzleHit.point, aimPoint) > 0.08f;
            }

            return new AimSolution(cameraRay, muzzleRay, aimPoint, endPoint,
                desiredDistance, assistedTarget, obstructed);
        }

        public static Vector3 ApplySpread(Vector3 forward, Vector2 spreadDegrees)
        {
            var direction = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            var rotation = Quaternion.LookRotation(direction, Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f
                ? Vector3.forward : Vector3.up);
            return (rotation * Quaternion.Euler(-spreadDegrees.y, spreadDegrees.x, 0f) * Vector3.forward).normalized;
        }

        public static bool TryFirstValidHit(Ray ray, float distance, GameObject owner,
            LayerMask hitMask, out RaycastHit firstHit)
        {
            var count = Physics.RaycastNonAlloc(ray, Hits, distance, hitMask, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            firstHit = default;
            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                var collider = hit.collider;
                if (collider == null || owner != null && collider.transform.IsChildOf(owner.transform)) continue;
                if (collider.GetComponentInParent<LootPickup>() != null) continue;
                if (collider.GetComponentInParent<BallisticProjectile>() != null) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                firstHit = hit;
            }
            return !float.IsPositiveInfinity(nearest);
        }
    }
}
