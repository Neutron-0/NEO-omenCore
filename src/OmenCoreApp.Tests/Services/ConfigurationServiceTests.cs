using System;
using System.IO;
using FluentAssertions;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    [Collection("Config Isolation")]
    public class ConfigurationServiceTests : IDisposable
    {
        private readonly string _previousConfigDir;
        private readonly string _tempDir;

        public ConfigurationServiceTests()
        {
            _previousConfigDir = Environment.GetEnvironmentVariable("OMENCORE_CONFIG_DIR") ?? string.Empty;
            _tempDir = Path.Combine(Path.GetTempPath(), "OmenCoreConfigServiceTest_" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("OMENCORE_CONFIG_DIR", _tempDir);
        }

        [Fact]
        public void Save_RecreatesConfigDirectory_WhenItWasRemovedAfterConstruction()
        {
            var service = new ConfigurationService();
            Directory.Delete(_tempDir, recursive: true);

            service.Save(service.Config);

            File.Exists(Path.Combine(_tempDir, "config.json")).Should().BeTrue();
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(
                "OMENCORE_CONFIG_DIR",
                string.IsNullOrWhiteSpace(_previousConfigDir) ? null : _previousConfigDir);

            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
