using System;
using System.IO;
using System.Text.Json;

namespace PhoneProjection.Models
{
    public class UserPreferences
    {
        public string SelectedPresetName { get; set; } = "Balanced";
        public int CustomMaxSize { get; set; } = 1600;
        public int CustomBitrateMbps { get; set; } = 8;
        public int SelectedFps { get; set; } = 60;
        public int VideoBufferMs { get; set; } = 0;
        public bool IsFullscreen { get; set; } = false;
        public bool IsBorderless { get; set; } = false;
        public bool AlwaysOnTop { get; set; } = false;
        public bool StayAwake { get; set; } = false;
        public bool AutoHideBars { get; set; } = true; // Immersive full display
        public bool ReducedMotion { get; set; } = false;
        public bool EnableAudio { get; set; } = false;
        public string AudioSource { get; set; } = "playback";
        public string CustomScrcpyPath { get; set; } = string.Empty;
        public string CustomAdbPath { get; set; } = string.Empty;
        public string LastWifiIp { get; set; } = string.Empty;
        public int LastWifiPort { get; set; } = 5555;
    }

    public class SettingsService
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PhoneProjection"
        );

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "preferences.json");

        public UserPreferences Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var prefs = JsonSerializer.Deserialize<UserPreferences>(json);
                    if (prefs != null) return prefs;
                }
            }
            catch { }

            return new UserPreferences();
        }

        public void Save(UserPreferences prefs)
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                string json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }

        public string GetConfigPath() => SettingsFilePath;
    }
}
