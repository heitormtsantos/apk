using UnityEngine;

namespace BattleRoyale
{
    [RequireComponent(typeof(SafeZonePlayerTracker))]
    public sealed class SafeZonePlayerEffects : MonoBehaviour
    {
        private SafeZoneController zone;
        private SafeZoneConfig config;
        private SafeZonePlayerTracker tracker;
        private AudioSource oneShotSource;
        private AudioSource outsideLoopSource;

        public void Configure(SafeZoneController safeZone, Transform target, SafeZoneConfig settings)
        {
            Unsubscribe();
            zone = safeZone;
            config = settings;
            tracker = GetComponent<SafeZonePlayerTracker>();
            tracker.Configure(zone, target);
            oneShotSource ??= gameObject.AddComponent<AudioSource>();
            outsideLoopSource ??= gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.spatialBlend = 0f;
            outsideLoopSource.playOnAwake = false;
            outsideLoopSource.loop = true;
            outsideLoopSource.spatialBlend = 0f;
            outsideLoopSource.volume = 0.42f;
            outsideLoopSource.clip = config != null ? config.outsideZoneLoopSound : null;
            tracker.InsideStateChanged += HandleInsideChanged;
            if (zone != null)
            {
                zone.OnWarningStarted += HandleWarning;
                zone.OnStateChanged += HandleStateChanged;
            }
        }

        private void HandleInsideChanged(bool inside)
        {
            if (inside)
            {
                outsideLoopSource?.Stop();
                PlayOneShot(config?.enteredSafeZoneSound);
            }
            else
            {
                PlayOneShot(config?.exitedSafeZoneSound);
                if (outsideLoopSource != null && outsideLoopSource.clip != null) outsideLoopSource.Play();
            }
        }

        private void HandleWarning() => PlayOneShot(config?.warningSound);

        private void HandleStateChanged(SafeZoneState state)
        {
            if (state == SafeZoneState.Shrinking) PlayOneShot(config?.shrinkingStartSound);
            if (state == SafeZoneState.Inactive) outsideLoopSource?.Stop();
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip != null && oneShotSource != null) oneShotSource.PlayOneShot(clip);
        }

        private void Unsubscribe()
        {
            if (tracker != null) tracker.InsideStateChanged -= HandleInsideChanged;
            if (zone != null)
            {
                zone.OnWarningStarted -= HandleWarning;
                zone.OnStateChanged -= HandleStateChanged;
            }
        }

        private void OnDestroy() => Unsubscribe();
    }
}
