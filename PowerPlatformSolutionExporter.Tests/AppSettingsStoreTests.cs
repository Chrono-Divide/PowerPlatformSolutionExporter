using PowerPlatformSolutionExporter.Services;
using Xunit;

namespace PowerPlatformSolutionExporter.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public void ExportFolderDefaultsThenRoundTripsWithoutConnectionInformation()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "PowerPlatformSolutionExporterTests",
            Guid.NewGuid().ToString("N"));
        var settingsFile = Path.Combine(testDirectory, "app-settings.json");
        var defaultFolder = Path.Combine(testDirectory, "DefaultExports");
        var customFolder = Path.Combine(testDirectory, "CustomExports");

        try
        {
            var store = new AppSettingsStore(settingsFile);
            Assert.Equal(defaultFolder, store.LoadExportFolder(defaultFolder));

            store.SaveExportFolder(customFolder);

            Assert.Equal(customFolder, store.LoadExportFolder(defaultFolder));
            var json = File.ReadAllText(settingsFile);
            Assert.Contains("exportFolder", json, StringComparison.Ordinal);
            Assert.DoesNotContain("environmentUrl", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("userName", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CorruptSettingsFallBackToTheDefaultFolder()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "PowerPlatformSolutionExporterTests",
            Guid.NewGuid().ToString("N"));
        var settingsFile = Path.Combine(testDirectory, "app-settings.json");
        var defaultFolder = Path.Combine(testDirectory, "DefaultExports");

        try
        {
            Directory.CreateDirectory(testDirectory);
            File.WriteAllText(settingsFile, "not json");

            Assert.Equal(
                defaultFolder,
                new AppSettingsStore(settingsFile).LoadExportFolder(defaultFolder));
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }
}
