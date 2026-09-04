using UnityEngine;

namespace BattleRoyale
{
    public static class GamePreferences
    {
        private const string SensitivityKey = "RavenDrop.LookSensitivity";
        private const string AdsSensitivityKey = "RavenDrop.AdsSensitivity";
        private const string FireDragSensitivityKey = "RavenDrop.FireDragSensitivity";
        private const string FrameRateKey = "RavenDrop.FrameRate";

        public static float LookSensitivity { get; private set; } = 1f;
        public static float AdsSensitivity { get; private set; } = 0.78f;
        public static float FireDragSensitivity { get; private set; } = 0.9f;
        public static int FrameRate { get; private set; } = 60;

        public static void LoadAndApply()
        {
            var preferredQuality = Application.isMobilePlatform ? 2 : 3;
            QualitySettings.SetQualityLevel(Mathf.Clamp(preferredQuality, 0, QualitySettings.names.Length - 1), true);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            LookSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), 0.55f, 1.65f);
            AdsSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(AdsSensitivityKey, 0.78f), 0.45f, 1.25f);
            FireDragSensitivity = NormalizeFireDragSensitivity(PlayerPrefs.GetFloat(FireDragSensitivityKey, 0.9f));
            FrameRate = NormalizeFrameRate(PlayerPrefs.GetInt(FrameRateKey, 60));
            ApplyFrameRate();
        }

        public static void SetLookSensitivity(float value)
        {
            LookSensitivity = Mathf.Clamp(Mathf.Round(value * 10f) / 10f, 0.55f, 1.65f);
            PlayerPrefs.SetFloat(SensitivityKey, LookSensitivity);
            PlayerPrefs.Save();
        }

        public static void SetAdsSensitivity(float value)
        {
            AdsSensitivity = Mathf.Clamp(Mathf.Round(value * 20f) / 20f, 0.45f, 1.25f);
            PlayerPrefs.SetFloat(AdsSensitivityKey, AdsSensitivity);
            PlayerPrefs.Save();
        }

        public static void SetFireDragSensitivity(float value)
        {
            FireDragSensitivity = NormalizeFireDragSensitivity(value);
            PlayerPrefs.SetFloat(FireDragSensitivityKey, FireDragSensitivity);
            PlayerPrefs.Save();
        }

        public static void SetFrameRate(int value)
        {
            FrameRate = NormalizeFrameRate(value);
            PlayerPrefs.SetInt(FrameRateKey, FrameRate);
            PlayerPrefs.Save();
            ApplyFrameRate();
        }

        public static RankedProfile LoadRankedProfile(BRMatchMode mode) => RankedProgression.Load(mode);

        public static void SaveRankedProfile(BRMatchMode mode, RankedProfile profile) =>
            RankedProgression.Save(mode, profile);

        private static int NormalizeFrameRate(int value) => value <= 30 ? 30 : value <= 60 ? 60 : 90;

        private static float NormalizeFireDragSensitivity(float value) =>
            Mathf.Clamp(Mathf.Round(value * 20f) / 20f, 0.45f, 1.4f);

        private static void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameRate;
        }
    }
}
