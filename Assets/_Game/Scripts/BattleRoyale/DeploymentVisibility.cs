using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class DeploymentVisibility : MonoBehaviour
    {
        private readonly Dictionary<Renderer, bool> renderers = new();
        private readonly Dictionary<Collider, bool> colliders = new();
        private bool captured;

        public bool Visible { get; private set; } = true;

        public void SetVisible(bool visible)
        {
            if (visible == Visible && captured) return;
            if (!visible)
            {
                Capture();
                foreach (var renderer in renderers.Keys)
                    if (renderer != null) renderer.enabled = false;
                foreach (var collider in colliders.Keys)
                    if (collider != null) collider.enabled = false;
                Visible = false;
                return;
            }

            foreach (var pair in renderers)
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in colliders)
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            renderers.Clear();
            colliders.Clear();
            captured = false;
            Visible = true;
        }

        private void Capture()
        {
            if (captured) return;
            renderers.Clear();
            colliders.Clear();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderers[renderer] = renderer.enabled;
            foreach (var collider in GetComponentsInChildren<Collider>(true)) colliders[collider] = collider.enabled;
            captured = true;
        }
    }
}
