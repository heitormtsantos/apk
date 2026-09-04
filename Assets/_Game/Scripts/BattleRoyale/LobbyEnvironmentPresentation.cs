using UnityEngine;

namespace BattleRoyale
{
    public sealed class LobbyEnvironmentPresentation : MonoBehaviour
    {
        private Transform platformAccent;
        private Transform weaponDisplay;
        private Transform scanLine;
        private Light accentLight;
        private Transform drone;
        private Transform hologram;
        private Vector3 weaponBasePosition;
        private Vector3 scanBasePosition;
        private Vector3 droneBasePosition;
        private Vector3 hologramBaseScale;
        private float accentBaseIntensity;

        public void Configure(Transform platform, Transform weapon, Transform scan, Light light,
            Transform companionDrone = null, Transform tacticalHologram = null)
        {
            platformAccent = platform;
            weaponDisplay = weapon;
            scanLine = scan;
            accentLight = light;
            drone = companionDrone;
            hologram = tacticalHologram;
            if (weaponDisplay != null) weaponBasePosition = weaponDisplay.localPosition;
            if (scanLine != null) scanBasePosition = scanLine.localPosition;
            if (accentLight != null) accentBaseIntensity = accentLight.intensity;
            if (drone != null) droneBasePosition = drone.localPosition;
            if (hologram != null) hologramBaseScale = hologram.localScale;
        }

        private void Update()
        {
            var time = Time.unscaledTime;
            if (platformAccent != null)
                platformAccent.localRotation = Quaternion.Euler(0f, time * 7f, 0f);
            if (weaponDisplay != null)
            {
                weaponDisplay.localPosition = weaponBasePosition + Vector3.up * (Mathf.Sin(time * 1.1f) * 0.035f);
                weaponDisplay.localRotation = Quaternion.Euler(0f, 20f + Mathf.Sin(time * 0.42f) * 8f, 90f);
            }
            if (scanLine != null)
                scanLine.localPosition = scanBasePosition + Vector3.up * (Mathf.PingPong(time * 0.48f, 1.5f) - 0.75f);
            if (accentLight != null)
                accentLight.intensity = accentBaseIntensity * (0.92f + Mathf.Sin(time * 1.7f) * 0.08f);
            if (drone != null)
            {
                drone.localPosition = droneBasePosition + new Vector3(Mathf.Sin(time * 0.55f) * 0.08f,
                    Mathf.Sin(time * 1.25f) * 0.055f, 0f);
                drone.localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 0.7f) * 12f, 0f);
            }
            if (hologram != null)
            {
                hologram.localRotation = Quaternion.Euler(0f, time * 18f, 0f);
                hologram.localScale = hologramBaseScale * (0.96f + Mathf.Sin(time * 2f) * 0.04f);
            }
        }
    }
}
