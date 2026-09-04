using UnityEngine;

namespace BattleRoyale
{
    public sealed class VehicleAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource engineSource;
        [SerializeField] private AudioSource oneShotSource;
        [SerializeField] private AudioClip engineLoop;
        [SerializeField] private AudioClip hornClip;
        [SerializeField] private AudioClip collisionClip;
        [SerializeField, Range(0.5f, 2f)] private float minimumPitch = 0.72f;
        [SerializeField, Range(0.5f, 2.5f)] private float maximumPitch = 1.55f;
        private VehicleController controller;

        private void Awake()
        {
            controller = GetComponent<VehicleController>();
            if (engineSource != null)
            {
                engineSource.clip = engineLoop;
                engineSource.loop = true;
                if (engineLoop != null && !engineSource.isPlaying) engineSource.Play();
            }
        }

        private void Update()
        {
            if (engineSource == null || controller == null) return;
            engineSource.pitch = Mathf.Lerp(engineSource.pitch,
                Mathf.Lerp(minimumPitch, maximumPitch, controller.NormalizedSpeed), 5f * Time.deltaTime);
            engineSource.volume = Mathf.Lerp(0.2f, 0.72f, controller.NormalizedSpeed);
        }

        public void Horn()
        {
            if (oneShotSource != null && hornClip != null) oneShotSource.PlayOneShot(hornClip, 0.9f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (oneShotSource != null && collisionClip != null && collision.relativeVelocity.magnitude > 7f)
                oneShotSource.PlayOneShot(collisionClip, Mathf.Clamp01(collision.relativeVelocity.magnitude / 25f));
        }
    }
}
