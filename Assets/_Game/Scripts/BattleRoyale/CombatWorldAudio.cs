using UnityEngine;
using System.Collections.Generic;

namespace BattleRoyale
{
    public static class CombatWorldAudio
    {
        private const int SourceCount = 20;
        private static AudioSource[] sources;
        private static int nextSource;
        private static readonly Dictionary<string, AudioClip> Clips = new();

        public static void PlayImpact(Vector3 position, bool character)
        {
            Play(character ? "Audio/CC0/character-hit" : "Audio/CC0/bullet-impact", position,
                character ? 0.55f : 0.42f, Random.Range(0.94f, 1.07f), 0.9f, 1.5f, 38f);
        }

        public static void Play(string resourcePath, Vector3 position, float volume, float pitch,
            float spatialBlend, float minDistance, float maxDistance)
        {
            EnsurePool();
            if (!Clips.TryGetValue(resourcePath, out var clip))
            {
                clip = Resources.Load<AudioClip>(resourcePath);
                Clips[resourcePath] = clip;
            }
            if (clip == null || sources == null || sources.Length == 0) return;
            var source = sources[nextSource++ % sources.Length];
            source.transform.position = position;
            source.pitch = pitch;
            source.volume = volume;
            source.spatialBlend = spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.clip = clip;
            source.Play();
        }

        private static void EnsurePool()
        {
            if (sources != null && sources.Length == SourceCount && sources[0] != null) return;
            var root = new GameObject("Combat Audio Pool");
            Object.DontDestroyOnLoad(root);
            sources = new AudioSource[SourceCount];
            for (var i = 0; i < SourceCount; i++)
            {
                var child = new GameObject($"Impact Audio {i + 1:00}");
                child.transform.SetParent(root.transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.9f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 1.5f;
                source.maxDistance = 38f;
                source.dopplerLevel = 0f;
                sources[i] = source;
            }
        }
    }

    public sealed class FootstepAudioEmitter : MonoBehaviour
    {
        private static AudioClip[] clips;
        private BRCharacterMotor motor;
        private BRParticipant participant;
        private float nextStep;
        private int sequence;

        private void Awake()
        {
            motor = GetComponent<BRCharacterMotor>();
            participant = GetComponent<BRParticipant>();
            LoadClips();
        }

        private void Update()
        {
            if (motor == null || participant == null || participant.Health.IsDead
                || participant.Phase != ParticipantPhase.Grounded || !motor.IsGrounded) return;
            var speed = new Vector2(motor.Velocity.x, motor.Velocity.z).magnitude;
            if (speed < 0.8f || Time.time < nextStep || clips == null || clips.Length == 0) return;
            nextStep = Time.time + Mathf.Lerp(0.48f, 0.29f, Mathf.InverseLerp(1f, 6f, speed));
            var clipIndex = sequence++ % clips.Length;
            var clip = clips[clipIndex];
            if (clip == null) return;
            CombatWorldAudio.Play($"Audio/CC0/footstep-{clipIndex + 1}", transform.position,
                participant.IsPlayer ? 0.32f : 0.2f, Random.Range(0.93f, 1.07f), 0.9f, 1.2f, 22f);
        }

        private static void LoadClips()
        {
            if (clips != null) return;
            clips = new AudioClip[6];
            for (var i = 0; i < clips.Length; i++) clips[i] = Resources.Load<AudioClip>($"Audio/CC0/footstep-{i + 1}");
        }
    }
}
