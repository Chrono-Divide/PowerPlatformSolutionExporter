namespace dataverse_solution_exporter.Services;

public static class AppStorage
{
    private const string CurrentDirectoryName = "DataverseSolutionExporter";
    private const string LegacyDirectoryName = "DataverseSnapshotManager";

    public static string GetPath(string fileName)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var currentDirectory = Path.Combine(localAppData, CurrentDirectoryName);
        Directory.CreateDirectory(currentDirectory);
        var currentPath = Path.Combine(currentDirectory, fileName);
        if (File.Exists(currentPath))
        {
            return currentPath;
        }

        var legacyPath = Path.Combine(localAppData, LegacyDirectoryName, fileName);
        if (!File.Exists(legacyPath))
        {
            return currentPath;
        }

        try
        {
            File.Copy(legacyPath, currentPath, overwrite: false);
        }
        catch (IOException)
        {
            // Another process may have completed the one-time migration.
        }
        catch (UnauthorizedAccessException)
        {
            // Fall back to the existing legacy file when migration is not permitted.
            return legacyPath;
        }

        return File.Exists(currentPath) ? currentPath : legacyPath;
    }
}
