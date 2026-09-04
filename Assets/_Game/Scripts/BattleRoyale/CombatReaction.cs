using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class CombatReaction : MonoBehaviour
    {
        private readonly List<Renderer> renderers = new();
        private MaterialPropertyBlock block;
        private HealthArmorSystem health;
        private float flashUntil;

        private void Awake()
        {
            health = GetComponent<HealthArmorSystem>();
            block = new MaterialPropertyBlock();
        }

        private void Start()
        {
            renderers.AddRange(GetComponentsInChildren<Renderer>(true));
            if (health != null) health.Damaged += OnDamaged;
        }

        private void Update()
        {
            if (flashUntil <= 0f || Time.time < flashUntil) return;
            flashUntil = 0f;
            foreach (var renderer in renderers)
                if (renderer != null) renderer.SetPropertyBlock(null);
        }

        private void OnDamaged(HealthArmorSystem target, DamageReport report)
        {
            flashUntil = Time.time + (report.Headshot ? 0.16f : 0.1f);
            var tint = report.Headshot ? new Color(1f, 0.08f, 0.03f) : new Color(1f, 0.28f, 0.16f);
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            foreach (var renderer in renderers)
                if (renderer != null) renderer.SetPropertyBlock(block);
            GetComponent<CharacterVisualAnimator>()?.PlayHitReaction();

            var participant = GetComponent<BRParticipant>();
            if (participant != null && participant.IsPlayer && report.Source != null)
                DamageFeedbackSystem.ReportIncoming(report.Source.transform.position, report.FinalDamage);
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }
    }

    public sealed class ShellCasingVisual : MonoBehaviour
    {
        private static readonly Queue<ShellCasingVisual> ActiveOrder = new();
        private static readonly HashSet<ShellCasingVisual> Active = new();
        private static readonly Stack<ShellCasingVisual> Inactive = new();
        private const int MaxActive = 28;
        private Rigidbody body;
        private Renderer casingRenderer;
        private static Material casingMaterial;

        public static int TotalCreated { get; private set; }

        public static void Eject(Vector3 origin, Vector3 right, Vector3 forward, WeaponClass weaponClass)
        {
            if (!Application.isPlaying) return;
            while (ActiveOrder.Count > 0 && (ActiveOrder.Peek() == null || !Active.Contains(ActiveOrder.Peek()))) ActiveOrder.Dequeue();
            while (Active.Count >= MaxActive && ActiveOrder.Count > 0)
            {
                var oldest = ActiveOrder.Dequeue();
                if (oldest != null && Active.Contains(oldest)) oldest.Release();
            }

            var visual = Acquire();
            visual.transform.position = origin + right * 0.07f;
            visual.transform.rotation = Random.rotation;
            visual.transform.localScale = weaponClass == WeaponClass.Shotgun
                ? new Vector3(0.025f, 0.065f, 0.025f)
                : new Vector3(0.018f, 0.045f, 0.018f);
            visual.casingRenderer.sharedMaterial = casingMaterial ??= BRMaterialFactory.Create("Casing Brass", new Color(0.72f, 0.48f, 0.12f));
            visual.body.linearVelocity = Vector3.zero;
            visual.body.angularVelocity = Vector3.zero;
            visual.body.AddForce((right * 1.15f + Vector3.up * 0.85f - forward * 0.12f) * (weaponClass == WeaponClass.Sniper ? 1.3f : 1f), ForceMode.Impulse);
            visual.body.AddTorque(Random.onUnitSphere * 0.04f, ForceMode.Impulse);
            Active.Add(visual);
            ActiveOrder.Enqueue(visual);
            visual.Invoke(nameof(Release), 2.2f);
        }

        private static ShellCasingVisual Acquire()
        {
            ShellCasingVisual visual = null;
            while (Inactive.Count > 0 && visual == null) visual = Inactive.Pop();
            if (visual != null)
            {
                visual.gameObject.SetActive(true);
                return visual;
            }
            var casing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            TotalCreated++;
            casing.name = "Spent Casing";
            visual = casing.AddComponent<ShellCasingVisual>();
            visual.casingRenderer = casing.GetComponent<Renderer>();
            visual.body = casing.AddComponent<Rigidbody>();
            visual.body.mass = 0.012f;
            visual.body.linearDamping = 0.08f;
            visual.body.angularDamping = 0.08f;
            return visual;
        }

        private void Release()
        {
            CancelInvoke();
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
            casingMaterial = null;
            TotalCreated = 0;
        }
    }
}
