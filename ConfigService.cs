using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace PhotoPrismCleanup
{
    public class AppConfig
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "";
        public string PasswordOrKey { get; set; } = "";
        public bool UseKey { get; set; }
        public string KeyPath { get; set; } = "";
        public string RemoteFolder { get; set; } = "/opt/photoprism/originals";
        public int LastIndex { get; set; }
    }

    public static class ConfigService
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhotoPrismCleanup");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                if (File.Exists(ConfigFile))
                {
                    var json = File.ReadAllText(ConfigFile);
                    return JsonSerializer.Deserialize<AppConfig>(json)
                           ?? new AppConfig();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load config:\n{ex.Message}",
                                "Config Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return new AppConfig();
        }

        public static void Save(AppConfig cfg)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                var json = JsonSerializer.Serialize(cfg,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save config:\n{ex.Message}",
                                "Config Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
