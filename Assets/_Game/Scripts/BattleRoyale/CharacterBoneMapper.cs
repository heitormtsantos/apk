using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class CharacterBoneMapper : MonoBehaviour
    {
        [SerializeField] private Transform armatureRoot;
        [SerializeField] private Transform hips;

        private readonly Dictionary<string, Transform> characterBones = new(StringComparer.OrdinalIgnoreCase);
        private bool ready;

        public Transform ArmatureRoot => armatureRoot;
        public Transform Hips => hips;
        public int BoneCount => characterBones.Count;

        public void Configure(Transform root, Transform rootHips = null)
        {
            armatureRoot = root;
            hips = rootHips;
            Rebuild();
        }

        public void Rebuild()
        {
            characterBones.Clear();
            ready = armatureRoot != null;
            if (!ready) return;
            var transforms = armatureRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var bone = transforms[i];
                AddName(bone.name, bone);
                AddName(CanonicalName(bone.name), bone);
                if (hips == null && CanonicalName(bone.name).Equals("Hips", StringComparison.OrdinalIgnoreCase))
                    hips = bone;
            }
        }

        public Transform Find(string boneName)
        {
            EnsureReady();
            if (string.IsNullOrWhiteSpace(boneName)) return null;
            if (characterBones.TryGetValue(boneName, out var exact)) return exact;
            return characterBones.TryGetValue(CanonicalName(boneName), out var canonical) ? canonical : null;
        }

        public bool TryRemap(SkinnedMeshRenderer renderer, string itemId)
        {
            if (renderer == null) return false;
            EnsureReady();
            if (!ready)
            {
                Debug.LogWarning($"Wardrobe item '{itemId}' cannot map bones because the armature is missing.", this);
                return false;
            }

            var sourceBones = renderer.bones;
            var remapped = new Transform[sourceBones.Length];
            var success = true;
            for (var i = 0; i < sourceBones.Length; i++)
            {
                var source = sourceBones[i];
                remapped[i] = source != null ? Find(source.name) : null;
                if (remapped[i] != null) continue;
                success = false;
                Debug.LogWarning($"Wardrobe item '{itemId}', renderer '{renderer.name}': bone " +
                    $"'{source?.name ?? "<null>"}' was not found in '{armatureRoot.name}'.", renderer);
            }
            if (!success) return false;

            var mappedRoot = renderer.rootBone != null ? Find(renderer.rootBone.name) : null;
            renderer.bones = remapped;
            renderer.rootBone = mappedRoot != null ? mappedRoot : hips;
            return renderer.rootBone != null;
        }

        public static string CanonicalName(string boneName)
        {
            if (string.IsNullOrWhiteSpace(boneName)) return string.Empty;
            var colon = boneName.LastIndexOf(':');
            return colon >= 0 && colon + 1 < boneName.Length ? boneName[(colon + 1)..] : boneName;
        }

        private void AddName(string key, Transform value)
        {
            if (!string.IsNullOrWhiteSpace(key) && !characterBones.ContainsKey(key)) characterBones.Add(key, value);
        }

        private void EnsureReady()
        {
            if (!ready && armatureRoot != null) Rebuild();
        }
    }
}
