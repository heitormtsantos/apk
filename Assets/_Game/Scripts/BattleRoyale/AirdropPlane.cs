using System;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class AirdropPlane : MonoBehaviour
    {
        private Vector3 start;
        private Vector3 end;
        private Vector3 dropPoint;
        private float speed;
        private float dropProgress;
        private bool dropped;
        private Action<Vector3> onDrop;

        public void Configure(Vector3 from, Vector3 to, Vector3 target, float movementSpeed,
            Action<Vector3> dropCallback)
        {
            start = from;
            end = to;
            dropPoint = target;
            speed = Mathf.Max(10f, movementSpeed);
            onDrop = dropCallback;
            var routeLength = Mathf.Max(0.01f, Vector3.Distance(start, end));
            dropProgress = Mathf.Clamp01(Vector3.Distance(start, dropPoint) / routeLength);
            transform.SetPositionAndRotation(start, Quaternion.LookRotation((end - start).normalized));
            BuildVisual();
        }

        private void Update()
        {
            var previous = transform.position;
            transform.position = Vector3.MoveTowards(previous, end, speed * Time.deltaTime);
            var routeProgress = Mathf.InverseLerp(0f, Vector3.Distance(start, end),
                Vector3.Distance(start, transform.position));
            if (!dropped && HasReachedDropPoint(routeProgress, dropProgress))
            {
                dropped = true;
                onDrop?.Invoke(new Vector3(dropPoint.x, transform.position.y - 3f, dropPoint.z));
            }
            if ((transform.position - end).sqrMagnitude <= 0.01f) Destroy(gameObject);
        }

        public static bool HasReachedDropPoint(float routeProgress, float targetProgress) =>
            Mathf.Clamp01(routeProgress) >= Mathf.Clamp01(targetProgress);

        private void BuildVisual()
        {
            var prefab = Resources.Load<GameObject>("Aircraft/CargoPlane_GameplayVisual");
            if (prefab != null)
            {
                var visual = Instantiate(prefab, transform, false);
                visual.name = "Imported Airdrop Cargo Plane";
                BRAssetVisuals.PrepareForUrp(visual);
                ImportedGameplayVisuals.NormalizeLargestDimension(visual, 14f);
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) Destroy(collider);
                return;
            }
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "Airdrop Aircraft";
            fallback.transform.SetParent(transform, false);
            fallback.transform.localScale = new Vector3(3f, 0.45f, 8f);
            Destroy(fallback.GetComponent<Collider>());
        }
    }
}
