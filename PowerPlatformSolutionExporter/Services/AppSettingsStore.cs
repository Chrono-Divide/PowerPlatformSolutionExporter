using System.Text.Json;

namespace PowerPlatformSolutionExporter.Services;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _filePath;

    public AppSettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? AppStorage.GetPath("app-settings.json");
    }

    public string LoadExportFolder(string defaultExportFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultExportFolder);
        if (!File.Exists(_filePath))
        {
            return defaultExportFolder;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<AppSettingsDocument>(json, JsonOptions);
            return string.IsNullOrWhiteSpace(settings?.ExportFolder)
                ? defaultExportFolder
                : settings.ExportFolder.Trim();
        }
        catch (IOException)
        {
            return defaultExportFolder;
        }
        catch (UnauthorizedAccessException)
        {
            return defaultExportFolder;
        }
        catch (JsonException)
        {
            return defaultExportFolder;
        }
    }

    public void SaveExportFolder(string exportFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportFolder);
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The application settings path has no parent directory.");
        Directory.CreateDirectory(directory);
        var settings = new AppSettingsDocument { ExportFolder = exportFolder.Trim() };
        var temporaryFile = _filePath + ".tmp";
        File.WriteAllText(temporaryFile, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryFile, _filePath, overwrite: true);
    }

    private sealed class AppSettingsDocument
    {
        public int FormatVersion { get; init; } = 1;
        public string ExportFolder { get; init; } = string.Empty;
    }
}
