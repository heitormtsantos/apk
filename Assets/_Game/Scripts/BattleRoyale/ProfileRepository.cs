using System;
using System.IO;
using UnityEngine;

namespace BattleRoyale
{
    public readonly struct ProfileLoadResult
    {
        public ProfileLoadResult(bool success, PlayerProgressionData profile, bool recoveredBackup, string error)
        {
            Success = success;
            Profile = profile;
            RecoveredBackup = recoveredBackup;
            Error = error ?? string.Empty;
        }
        public bool Success { get; }
        public PlayerProgressionData Profile { get; }
        public bool RecoveredBackup { get; }
        public string Error { get; }
    }

    public interface IProfileRepository
    {
        ProfileLoadResult Load();
        bool Save(PlayerProgressionData profile, out string error);
    }

    public sealed class LocalProfileRepository : IProfileRepository
    {
        private readonly string profilePath;
        private string BackupPath => profilePath + ".bak";
        private string TemporaryPath => profilePath + ".tmp";

        public LocalProfileRepository(string path = null)
        {
            profilePath = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Application.persistentDataPath, "raven-profile.json") : path;
        }

        public ProfileLoadResult Load()
        {
            if (!File.Exists(profilePath) && !File.Exists(BackupPath))
                return new ProfileLoadResult(true, NewProfile(), false, string.Empty);
            if (TryRead(profilePath, out var profile, out var primaryError))
                return new ProfileLoadResult(true, profile, false, string.Empty);
            if (TryRead(BackupPath, out profile, out var backupError))
                return new ProfileLoadResult(true, profile, true, primaryError);
            return new ProfileLoadResult(false, NewProfile(), false,
                $"Primary: {primaryError}; Backup: {backupError}");
        }

        public bool Save(PlayerProgressionData profile, out string error)
        {
            error = string.Empty;
            if (profile == null)
            {
                error = "Profile is null";
                return false;
            }
            try
            {
                var directory = Path.GetDirectoryName(profilePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(TemporaryPath, JsonUtility.ToJson(profile, true));
                if (File.Exists(profilePath))
                {
                    File.Copy(profilePath, BackupPath, true);
                    File.Delete(profilePath);
                }
                File.Move(TemporaryPath, profilePath);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                try { if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath); } catch { }
                return false;
            }
        }

        private static bool TryRead(string path, out PlayerProgressionData profile, out string error)
        {
            profile = null;
            error = string.Empty;
            if (!File.Exists(path))
            {
                error = "File not found";
                return false;
            }
            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
                    throw new InvalidDataException("Invalid JSON root");
                profile = JsonUtility.FromJson<PlayerProgressionData>(json);
                if (profile == null) throw new InvalidDataException("Empty profile");
                profile.Normalize();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static PlayerProgressionData NewProfile()
        {
            var value = new PlayerProgressionData();
            value.Normalize();
            return value;
        }
    }
}
