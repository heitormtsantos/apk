using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    [Serializable]
    public sealed class BodyPartRenderers
    {
        [SerializeField] private BodyPart bodyPart;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();

        public BodyPart BodyPart => bodyPart;
        public Renderer[] Renderers => renderers ?? Array.Empty<Renderer>();

        public BodyPartRenderers(BodyPart part, Renderer[] bodyRenderers)
        {
            bodyPart = part;
            renderers = bodyRenderers ?? Array.Empty<Renderer>();
        }
    }

    public sealed class CharacterBodyVisibility : MonoBehaviour
    {
        [SerializeField] private BodyPartRenderers[] bodyParts = Array.Empty<BodyPartRenderers>();
        private readonly bool[] hidden = new bool[Enum.GetValues(typeof(BodyPart)).Length];

        public void Configure(BodyPartRenderers[] groups)
        {
            bodyParts = groups ?? Array.Empty<BodyPartRenderers>();
            ShowAll();
        }

        public void RefreshHiddenBodyParts(IEnumerable<ClothingItemData> equippedItems)
        {
            Array.Clear(hidden, 0, hidden.Length);
            if (equippedItems != null)
            {
                foreach (var item in equippedItems)
                {
                    if (item == null) continue;
                    var requested = item.BodyPartsToHide;
                    for (var i = 0; i < requested.Length; i++) hidden[(int)requested[i]] = true;
                }
            }
            Apply();
        }

        public bool IsHidden(BodyPart part) => hidden[(int)part];

        public void ShowAll()
        {
            Array.Clear(hidden, 0, hidden.Length);
            Apply();
        }

        private void Apply()
        {
            for (var groupIndex = 0; groupIndex < bodyParts.Length; groupIndex++)
            {
                var group = bodyParts[groupIndex];
                if (group == null) continue;
                var visible = !hidden[(int)group.BodyPart];
                var renderers = group.Renderers;
                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    if (renderers[rendererIndex] != null) renderers[rendererIndex].enabled = visible;
            }
        }
    }
}
