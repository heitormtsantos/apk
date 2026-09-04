using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleRoyale
{
    public sealed class BulletVisual : MonoBehaviour
    {
        private static readonly Queue<BulletVisual> ActiveOrder = new();
        private static readonly HashSet<BulletVisual> Active = new();
        private static readonly Stack<BulletVisual> Inactive = new();
        private static readonly Dictionary<WeaponClass, Material> Materials = new();
        public const int MaxActive = 40;

        private LineRenderer line;
        private Transform bulletHead;
        private Vector3 origin;
        private Vector3 end;
        private Vector3 direction;
        private Vector3 impactNormal;
        private Color impactColor;
        private float distance;
        private float speed;
        private float streakLength;
        private float travelled;
        private bool spawnImpact;
        private bool characterImpact;
        private bool finished;

        public static int ActiveCount => Active.Count;
        public static int TotalCreated { get; private set; }
        public float VisibleLength { get; private set; }

        public static BulletVisual Launch(Vector3 start, Vector3 destination, WeaponClass weaponClass,
            bool createImpact, Vector3 normal, Color color, bool hitCharacter = false)
        {
            TrimQueue();
            while (Active.Count >= MaxActive && ActiveOrder.Count > 0)
            {
                var oldest = ActiveOrder.Dequeue();
                if (oldest != null && Active.Contains(oldest)) oldest.Release();
            }

            var visual = Acquire();
            visual.Configure(start, destination, weaponClass, createImpact, normal, color, hitCharacter);
            Active.Add(visual);
            ActiveOrder.Enqueue(visual);
            return visual;
        }

        private static BulletVisual Acquire()
        {
            while (Inactive.Count > 0)
            {
                var cached = Inactive.Pop();
                if (cached == null) continue;
                cached.gameObject.SetActive(true);
                return cached;
            }
            var obj = new GameObject("Bullet Visual");
            TotalCreated++;
            return obj.AddComponent<BulletVisual>();
        }

        public static float SpeedFor(WeaponClass weaponClass) => weaponClass switch
        {
            WeaponClass.Pistol => 115f,
            WeaponClass.Smg => 145f,
            WeaponClass.Rifle => 170f,
            WeaponClass.Shotgun => 105f,
            WeaponClass.Sniper => 245f,
            _ => 150f
        };

        public static float StreakLengthFor(WeaponClass weaponClass) => weaponClass switch
        {
            WeaponClass.Pistol => 0.38f,
            WeaponClass.Smg => 0.34f,
            WeaponClass.Rifle => 0.46f,
            WeaponClass.Shotgun => 0.32f,
            WeaponClass.Sniper => 0.62f,
            _ => 0.4f
        };

        private void Configure(Vector3 start, Vector3 destination, WeaponClass weaponClass,
            bool createImpact, Vector3 normal, Color color, bool hitCharacter)
        {
            EnsureVisuals();
            origin = start;
            end = destination;
            var delta = end - origin;
            distance = Mathf.Max(0.01f, delta.magnitude);
            direction = delta / distance;
            speed = SpeedFor(weaponClass);
            streakLength = StreakLengthFor(weaponClass);
            spawnImpact = createImpact;
            characterImpact = hitCharacter;
            impactNormal = normal.sqrMagnitude > 0.01f ? normal.normalized : -direction;
            impactColor = color;
            travelled = 0f;
            finished = false;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = weaponClass == WeaponClass.Sniper ? 0.022f : 0.012f;
            line.endWidth = weaponClass == WeaponClass.Sniper ? 0.008f : 0.004f;
            line.sharedMaterial = MaterialFor(weaponClass);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            bulletHead.localScale = Vector3.one * (weaponClass == WeaponClass.Sniper ? 0.038f : 0.024f);
            bulletHead.GetComponent<Renderer>().sharedMaterial = MaterialFor(weaponClass);
            SetSegment(origin, origin);
        }

        private void EnsureVisuals()
        {
            if (line == null) line = gameObject.AddComponent<LineRenderer>();
            if (bulletHead == null) CreateBulletHead();
        }

        private void Update()
        {
            if (finished) return;
            travelled = Mathf.Min(distance, travelled + speed * Time.deltaTime);
            var head = origin + direction * travelled;
            var tail = origin + direction * Mathf.Max(0f, travelled - streakLength);
            SetSegment(tail, head);
            if (bulletHead != null) bulletHead.position = head;
            if (travelled < distance) return;
            finished = true;
            if (spawnImpact)
            {
                CombatEffectPool.SpawnImpact(end, impactNormal, impactColor);
                CombatWorldAudio.PlayImpact(end, characterImpact);
            }
            Invoke(nameof(Release), 0.035f);
        }

        private void SetSegment(Vector3 tail, Vector3 head)
        {
            line.SetPosition(0, tail);
            line.SetPosition(1, head);
            VisibleLength = Vector3.Distance(tail, head);
        }

        private void CreateBulletHead()
        {
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Bullet Head";
            head.transform.SetParent(transform, false);
            head.transform.localScale = Vector3.one * 0.052f;
            var collider = head.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            bulletHead = head.transform;
        }

        private static Material MaterialFor(WeaponClass weaponClass)
        {
            if (Materials.TryGetValue(weaponClass, out var material) && material != null) return material;
            var color = weaponClass switch
            {
                WeaponClass.Sniper => new Color(1f, 0.88f, 0.42f),
                WeaponClass.Shotgun => new Color(1f, 0.58f, 0.18f),
                WeaponClass.Smg => new Color(1f, 0.72f, 0.2f),
                _ => new Color(1f, 0.8f, 0.28f)
            };
            material = BRMaterialFactory.Create($"{weaponClass} Bullet", color);
            Materials[weaponClass] = material;
            return material;
        }

        private static void TrimQueue()
        {
            while (ActiveOrder.Count > 0)
            {
                var candidate = ActiveOrder.Peek();
                if (candidate != null && Active.Contains(candidate)) break;
                ActiveOrder.Dequeue();
            }
        }

        private void Release()
        {
            CancelInvoke();
            finished = true;
            Active.Remove(this);
            gameObject.SetActive(false);
            Inactive.Push(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPool()
        {
            ActiveOrder.Clear();
            Active.Clear();
            Inactive.Clear();
            Materials.Clear();
            TotalCreated = 0;
        }
    }
}
