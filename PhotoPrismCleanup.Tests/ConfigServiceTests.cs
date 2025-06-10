using NUnit.Framework;
using System.IO;

namespace PhotoPrismCleanup.Tests
{
    public class ConfigServiceTests
    {
        [Test]
        public void SaveLoad_RoundTripsConfiguration()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ppc_test_" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            var originalDir = typeof(ConfigService).GetField("ConfigDir", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var originalFile = typeof(ConfigService).GetField("ConfigFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var oldDir = originalDir.GetValue(null) as string;
            var oldFile = originalFile.GetValue(null) as string;
            originalDir.SetValue(null, tempDir);
            originalFile.SetValue(null, Path.Combine(tempDir, "config.json"));
            try
            {
                var cfg = new AppConfig { Host = "example.com", Port = 23, Username = "user", UseKey = false, PasswordOrKey = "pwd" };
                ConfigService.Save(cfg);
                var loaded = ConfigService.Load();
                Assert.That(loaded.Host, Is.EqualTo(cfg.Host));
                Assert.That(loaded.Port, Is.EqualTo(cfg.Port));
                Assert.That(loaded.Username, Is.EqualTo(cfg.Username));
                Assert.That(loaded.PasswordOrKey, Is.EqualTo(cfg.PasswordOrKey));
            }
            finally
            {
                originalDir.SetValue(null, oldDir);
                originalFile.SetValue(null, oldFile);
                Directory.Delete(tempDir, true);
            }
        }
    }
}
