using UnityEngine;

namespace BattleRoyale
{
    public sealed class WeaponAudioEmitter : MonoBehaviour
    {
        public void PlayShot(WeaponClass weaponClass)
        {
            var path = weaponClass switch
            {
                WeaponClass.Pistol => "Audio/CC0/pistol-shot",
                WeaponClass.Shotgun => "Audio/CC0/shotgun-shot",
                WeaponClass.Sniper => "Audio/CC0/sniper-shot",
                _ => "Audio/CC0/rifle-shot"
            };
            var pitch = weaponClass == WeaponClass.Smg ? Random.Range(1.08f, 1.15f) : Random.Range(0.96f, 1.04f);
            var volume = weaponClass == WeaponClass.Sniper ? 0.92f : weaponClass == WeaponClass.Shotgun ? 0.86f : 0.7f;
            CombatWorldAudio.Play(path, transform.position + Vector3.up * 1.2f, volume, pitch, 0.92f, 3f, 72f);
        }

        public void PlayReload(WeaponClass weaponClass)
        {
            var path = weaponClass switch
            {
                WeaponClass.Pistol => "Audio/CC0/pistol-reload",
                WeaponClass.Shotgun => "Audio/CC0/shotgun-cock",
                _ => "Audio/CC0/rifle-reload"
            };
            CombatWorldAudio.Play(path, transform.position + Vector3.up, 0.72f, 1f, 0.72f, 2f, 35f);
        }
    }
}
