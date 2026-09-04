using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class LocalAdsVisibility : MonoBehaviour
    {
        private readonly Dictionary<Renderer, bool> rendererStates = new();
        private bool hideBody;
        private bool hideWeapon;

        public bool Hidden { get; private set; }

        public void SetHidden(bool hidden)
        {
            SetHidden(hidden, hidden);
        }

        public void SetHidden(bool hideBody, bool hideWeapon)
        {
            if (hideBody || hideWeapon)
            {
                this.hideBody = hideBody;
                this.hideWeapon = hideWeapon;
                Hidden = true;
                HideCurrentRenderers();
                return;
            }
            if (!Hidden && rendererStates.Count == 0) return;
            foreach (var entry in rendererStates)
                if (entry.Key != null) entry.Key.enabled = entry.Value;
            rendererStates.Clear();
            this.hideBody = false;
            this.hideWeapon = false;
            Hidden = false;
        }

        private void LateUpdate()
        {
            if (Hidden) HideCurrentRenderers();
        }

        private void HideCurrentRenderers()
        {
            var weaponVisual = GetComponent<ParticipantWeaponVisual>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                var weaponRenderer = weaponVisual != null && weaponVisual.IsWeaponRenderer(renderer);
                if (weaponRenderer ? !hideWeapon : !hideBody) continue;
                if (!rendererStates.ContainsKey(renderer)) rendererStates.Add(renderer, renderer.enabled);
                renderer.enabled = false;
            }
        }

        private void OnDisable() => SetHidden(false);
        private void OnDestroy() => SetHidden(false);
    }
}
