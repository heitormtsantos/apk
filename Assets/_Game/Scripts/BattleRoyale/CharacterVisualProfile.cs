using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class CharacterVisualProfile
    {
        public CharacterVisualProfile(string id, string prefabResource, float targetHeight,
            string[] animationResources, CharacterVisualProfile fallback = null)
        {
            Id = id;
            PrefabResource = prefabResource;
            TargetHeight = targetHeight;
            AnimationResources = animationResources ?? Array.Empty<string>();
            Fallback = fallback;
        }

        public string Id { get; }
        public string PrefabResource { get; }
        public float TargetHeight { get; }
        public string[] AnimationResources { get; }
        public CharacterVisualProfile Fallback { get; }

        public GameObject LoadPrefab() => Resources.Load<GameObject>(PrefabResource);

        public AnimationClip[] LoadClips()
        {
            var clips = new List<AnimationClip>();
            foreach (var resource in AnimationResources)
            {
                foreach (var clip in Resources.LoadAll<AnimationClip>(resource))
                {
                    if (clip != null && !clips.Contains(clip)) clips.Add(clip);
                }
            }
            return clips.ToArray();
        }
    }

    public static class CharacterVisualCatalog
    {
        private static readonly HashSet<string> WarnedProfiles = new();

        public static readonly CharacterVisualProfile Lightweight = new(
            "quaternius-soldier",
            "Quaternius/Characters/Character_Soldier",
            CharacterPresentationProfile.AnimatedNormalizationHeight,
            new[] { "Quaternius/Characters/Character_Soldier" });

        public static readonly CharacterVisualProfile Player = new(
            "mixamo-steve-player",
            "UserCharacters/Steve/Model/Steve",
            CharacterPresentationProfile.VisualHeight,
            new[]
            {
                "UserCharacters/Steve/Animations/Locomotion Pack",
                "UserCharacters/Steve/Animations/Rifle",
                "UserCharacters/Steve/Animations/Crouch",
                "UserCharacters/Steve/Animations/Reload"
            },
            Lightweight);

        public static CharacterVisualProfile Bot => Lightweight;
        public static CharacterVisualProfile Lobby => Player;

        public static CharacterVisualProfile ResolveAvailable(CharacterVisualProfile preferred)
        {
            var candidate = preferred ?? Lightweight;
            while (candidate != null)
            {
                var prefab = candidate.LoadPrefab();
                var animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
                if (prefab != null && animator != null && animator.avatar != null
                    && animator.avatar.isValid && animator.avatar.isHuman)
                    return candidate;

                if (WarnedProfiles.Add(candidate.Id))
                    Debug.LogWarning($"Character profile '{candidate.Id}' is unavailable or has an invalid humanoid Avatar; using fallback.");
                candidate = candidate.Fallback;
            }
            return Lightweight;
        }
    }
}
