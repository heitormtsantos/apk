using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class CombatEffectPool : MonoBehaviour
    {
        private static readonly Stack<CombatEffectPool> Inactive = new();
        private static Material impactMaterial;
        private static Material flashMaterial;
        private Renderer effectRenderer;
        private Light effectLight;
        private MaterialPropertyBlock colorBlock;

        public static int TotalCreated { get; private set; }

        public static void SpawnImpact(Vector3 point, Vector3 normal, Color color)
        {
            var effect = Acquire("Pooled Impact Marker");
            effect.transform.position = point + normal * 0.025f;
            effect.transform.localScale = Vector3.one * 0.12f;
            effect.effectRenderer.sharedMaterial = impactMaterial ??= BRMaterialFactory.Create("Bullet Impact Flash", Color.white);
            effect.ApplyColor(color);
            effect.effectLight.enabled = false;
            effect.Invoke(nameof(Release), 0.32f);
        }

        public static void SpawnMuzzle(Vector3 point)
        {
            var effect = Acquire("Muzzle Flash");
            effect.transform.position = point;
            effect.transform.localScale = Vector3.one * 0.14f;
            effect.effectRenderer.sharedMaterial = flashMaterial ??= BRMaterialFactory.Create("Muzzle Flash", new Color(1f, 0.48f, 0.08f));
            effect.ApplyColor(new Color(1f, 0.48f, 0.08f));
            effect.effectLight.enabled = true;
            effect.effectLight.color = new Color(1f, 0.42f, 0.08f);
            effect.effectLight.range = 2.4f;
            effect.effectLight.intensity = 3.2f;
            effect.Invoke(nameof(Release), 0.11f);
        }

        private static CombatEffectPool Acquire(string objectName)
        {
            CombatEffectPool effect = null;
            while (Inactive.Count > 0 && effect == null) effect = Inactive.Pop();
            if (effect == null)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                TotalCreated++;
                var collider = obj.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying) Destroy(collider);
                    else DestroyImmediate(collider);
                }
                effect = obj.AddComponent<CombatEffectPool>();
                effect.effectRenderer = obj.GetComponent<Renderer>();
                effect.effectLight = obj.AddComponent<Light>();
            }
            effect.name = objectName;
            effect.gameObject.SetActive(true);
            return effect;
        }

        private void ApplyColor(Color color)
        {
            colorBlock ??= new MaterialPropertyBlock();
            effectRenderer.GetPropertyBlock(colorBlock);
            colorBlock.SetColor("_BaseColor", color);
            colorBlock.SetColor("_Color", color);
            effectRenderer.SetPropertyBlock(colorBlock);
        }

        private void Release()
        {
            CancelInvoke();
            gameObject.SetActive(false);
            Inactive.Push(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPool()
        {
            Inactive.Clear();
            impactMaterial = null;
            flashMaterial = null;
            TotalCreated = 0;
        }
    }
}
