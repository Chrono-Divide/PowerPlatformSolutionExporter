using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using dataverse_table_backup_restore.Models;

namespace dataverse_table_backup_restore.Services;

public sealed class SnapshotPackageService
{
    private const string ManifestFileName = "manifest.json";
    private const string SolutionFileName = "solution.zip";
    private readonly DataverseService _dataverseService;
    private readonly PacCliService _pacCliService;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public SnapshotPackageService(DataverseService dataverseService, PacCliService pacCliService)
    {
        _dataverseService = dataverseService;
        _pacCliService = pacCliService;
    }

    public async Task<string> CreateSnapshotAsync(
        SnapshotCreateOptions options,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (options.Tables.Count == 0)
        {
            throw new InvalidOperationException("Select at least one table.");
        }

        Directory.CreateDirectory(options.OutputFolder);
        var snapshotName = $"DataverseSnapshot_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}"[..44] + ".dvsnapshot";
        var outputPath = Path.Combine(options.OutputFolder, snapshotName);
        var temporaryDirectory = CreateTemporaryDirectory();
        var tablesDirectory = Path.Combine(temporaryDirectory, "tables");
        Directory.CreateDirectory(tablesDirectory);

        try
        {
            string? solutionSha256 = null;
            if (options.IncludeStructure)
            {
                var solutionPath = Path.Combine(temporaryDirectory, SolutionFileName);
                await _pacCliService.ExportSolutionAsync(
                    options.EnvironmentUrl,
                    options.SolutionUniqueName,
                    solutionPath,
                    progress,
                    cancellationToken);
                solutionSha256 = await ComputeSha256Async(solutionPath, cancellationToken);
            }

            var tableManifests = new List<SnapshotTableManifest>();
            var warnings = new List<string>();
            foreach (var table in options.Tables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Capturing {table.DisplayName} ({table.LogicalName})...");
                var snapshot = await _dataverseService.ExportTableAsync(
                    table,
                    options.IncludeData,
                    progress,
                    cancellationToken);

                var unsupportedValues = snapshot.Records.Sum(record => record.Attributes.Values.Count(value => value.IsUnsupported));
                if (unsupportedValues > 0)
                {
                    warnings.Add($"{table.LogicalName}: {unsupportedValues:N0} values use unsupported SDK types and will be skipped during restore.");
                }

                var specialColumns = snapshot.Schema.Attributes
                    .Where(attribute => attribute.AttributeType.Contains("File", StringComparison.OrdinalIgnoreCase) ||
                                        attribute.AttributeType.Contains("Image", StringComparison.OrdinalIgnoreCase) ||
                                        attribute.AttributeType.Contains("PartyList", StringComparison.OrdinalIgnoreCase))
                    .Select(attribute => attribute.LogicalName)
                    .ToList();
                if (specialColumns.Count > 0)
                {
                    warnings.Add($"{table.LogicalName}: special file/image/party-list content requires manual verification ({string.Join(", ", specialColumns)}). ");
                }

                var relativePath = $"tables/{table.LogicalName}.json";
                var tablePath = Path.Combine(tablesDirectory, $"{table.LogicalName}.json");
                await using (var stream = File.Create(tablePath))
                {
                    await JsonSerializer.SerializeAsync(stream, snapshot, _jsonOptions, cancellationToken);
                }

                tableManifests.Add(new SnapshotTableManifest
                {
                    DisplayName = table.DisplayName,
                    LogicalName = table.LogicalName,
                    FileName = relativePath,
                    IsCustom = table.IsCustom,
                    AttributeCount = snapshot.Schema.Attributes.Count,
                    RecordCount = snapshot.Records.Count,
                    Sha256 = await ComputeSha256Async(tablePath, cancellationToken),
                });
            }

            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            var manifest = new SnapshotManifest
            {
                ApplicationVersion = version,
                CreatedUtc = DateTimeOffset.UtcNow,
                EnvironmentUrl = NormalizeEnvironmentUrl(options.EnvironmentUrl),
                Scope = options.Scope,
                Content = options.Content,
                SolutionUniqueName = options.SolutionUniqueName,
                IncludesStructure = options.IncludeStructure,
                IncludesData = options.IncludeData,
                SolutionSha256 = solutionSha256,
                Tables = tableManifests,
                Warnings = warnings,
            };

            var manifestPath = Path.Combine(temporaryDirectory, ManifestFileName);
            await using (var stream = File.Create(manifestPath))
            {
                await JsonSerializer.SerializeAsync(stream, manifest, _jsonOptions, cancellationToken);
            }

            ZipFile.CreateFromDirectory(temporaryDirectory, outputPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            progress?.Report($"Snapshot created: {outputPath}");
            return outputPath;
        }
        catch
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            throw;
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    public async Task<(SnapshotManifest Manifest, OperationSummary Summary)> RestoreSnapshotAsync(
        RestoreOptions options,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(options.SnapshotFile))
        {
            throw new FileNotFoundException("Snapshot file was not found.", options.SnapshotFile);
        }

        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            ZipFile.ExtractToDirectory(options.SnapshotFile, temporaryDirectory);
            var manifest = await LoadManifestAsync(Path.Combine(temporaryDirectory, ManifestFileName), cancellationToken);
            ValidateManifest(manifest, options.EnvironmentUrl);

            if (options.RestoreStructure)
            {
                if (!manifest.IncludesStructure)
                {
                    throw new InvalidOperationException("This snapshot does not contain a solution package for structure restore.");
                }

                var solutionPath = Path.Combine(temporaryDirectory, SolutionFileName);
                if (!File.Exists(solutionPath))
                {
                    throw new InvalidDataException("The snapshot manifest references structure data, but solution.zip is missing.");
                }

                if (string.IsNullOrWhiteSpace(manifest.SolutionSha256) ||
                    !string.Equals(
                        await ComputeSha256Async(solutionPath, cancellationToken),
                        manifest.SolutionSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Checksum validation failed for solution.zip.");
                }

                await _pacCliService.ImportSolutionAsync(
                    options.EnvironmentUrl,
                    solutionPath,
                    progress,
                    cancellationToken);
                await _dataverseService.RefreshMetadataAsync(progress, cancellationToken);
            }

            var summary = new OperationSummary();
            if (!options.RestoreData)
            {
                return (manifest, summary);
            }

            if (!manifest.IncludesData)
            {
                throw new InvalidOperationException("This snapshot does not contain table data.");
            }

            var tableSnapshots = new List<TableSnapshot>();
            foreach (var tableManifest in manifest.Tables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!tableManifest.IsCustom)
                {
                    throw new InvalidOperationException($"Restore is limited to custom tables. Snapshot table '{tableManifest.LogicalName}' is not custom.");
                }

                var tablePath = ResolveSnapshotPath(temporaryDirectory, tableManifest.FileName);
                var actualHash = await ComputeSha256Async(tablePath, cancellationToken);
                if (!string.Equals(actualHash, tableManifest.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Checksum validation failed for '{tableManifest.LogicalName}'.");
                }

                await using var stream = File.OpenRead(tablePath);
                var snapshot = await JsonSerializer.DeserializeAsync<TableSnapshot>(stream, _jsonOptions, cancellationToken)
                               ?? throw new InvalidDataException($"Unable to read table snapshot '{tableManifest.LogicalName}'.");
                tableSnapshots.Add(snapshot);
            }

            tableSnapshots = OrderByDependencies(tableSnapshots);
            progress?.Report("Restore pass 1/2: scalar values and record creation...");
            foreach (var tableSnapshot in tableSnapshots)
            {
                Merge(summary, await _dataverseService.RestoreTableAsync(tableSnapshot, referencePass: false, progress, cancellationToken));
            }

            progress?.Report("Restore pass 2/2: lookups, owners, state, and status...");
            foreach (var tableSnapshot in tableSnapshots)
            {
                Merge(summary, await _dataverseService.RestoreTableAsync(tableSnapshot, referencePass: true, progress, cancellationToken));
            }

            if (options.DeleteRecordsNotInSnapshot)
            {
                foreach (var tableSnapshot in tableSnapshots.AsEnumerable().Reverse())
                {
                    summary.Deleted += await _dataverseService.DeleteRecordsNotInSnapshotAsync(tableSnapshot, progress, cancellationToken);
                }
            }

            return (manifest, summary);
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    public async Task<SnapshotManifest> ReadManifestAsync(string snapshotFile, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(snapshotFile);
        var entry = archive.GetEntry(ManifestFileName)
                    ?? throw new InvalidDataException("The selected file does not contain manifest.json.");
        await using var stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<SnapshotManifest>(stream, _jsonOptions, cancellationToken)
               ?? throw new InvalidDataException("Unable to read the snapshot manifest.");
    }

    private async Task<SnapshotManifest> LoadManifestAsync(string manifestPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException("The snapshot does not contain manifest.json.");
        }

        await using var stream = File.OpenRead(manifestPath);
        return await JsonSerializer.DeserializeAsync<SnapshotManifest>(stream, _jsonOptions, cancellationToken)
               ?? throw new InvalidDataException("Unable to read the snapshot manifest.");
    }

    private static void ValidateManifest(SnapshotManifest manifest, string targetEnvironmentUrl)
    {
        if (manifest.FormatVersion != SnapshotManifest.CurrentFormatVersion)
        {
            throw new InvalidDataException($"Snapshot format {manifest.FormatVersion} is not supported by this application.");
        }

        if (!string.Equals(
                NormalizeEnvironmentUrl(manifest.EnvironmentUrl),
                NormalizeEnvironmentUrl(targetEnvironmentUrl),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The snapshot belongs to '{manifest.EnvironmentUrl}', but the connected target is '{targetEnvironmentUrl}'. Cross-environment restore is blocked.");
        }
    }

    private static List<TableSnapshot> OrderByDependencies(List<TableSnapshot> snapshots)
    {
        var byName = snapshots.ToDictionary(snapshot => snapshot.Schema.LogicalName, StringComparer.OrdinalIgnoreCase);
        var pending = snapshots.ToDictionary(
            snapshot => snapshot.Schema.LogicalName,
            snapshot => snapshot.Schema.Attributes
                .SelectMany(attribute => attribute.Targets)
                .Where(target => byName.ContainsKey(target) && !string.Equals(target, snapshot.Schema.LogicalName, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        var ordered = new List<TableSnapshot>();

        while (pending.Count > 0)
        {
            var ready = pending
                .Where(item => item.Value.All(dependency => ordered.Any(snapshot => string.Equals(snapshot.Schema.LogicalName, dependency, StringComparison.OrdinalIgnoreCase))))
                .Select(item => item.Key)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ready.Count == 0)
            {
                ordered.AddRange(pending.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).Select(name => byName[name]));
                break;
            }

            foreach (var name in ready)
            {
                ordered.Add(byName[name]);
                pending.Remove(name);
            }
        }

        return ordered;
    }

    private static string CreateTemporaryDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "DataverseSnapshotManager");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DataverseSnapshotManager"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string ResolveSnapshotPath(string root, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!resolved.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(resolved))
        {
            throw new InvalidDataException($"Invalid or missing snapshot entry '{relativePath}'.");
        }

        return resolved;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static string NormalizeEnvironmentUrl(string value)
    {
        return new Uri(value).GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static void Merge(OperationSummary target, OperationSummary source)
    {
        target.Created += source.Created;
        target.Updated += source.Updated;
        target.Deleted += source.Deleted;
        target.Skipped += source.Skipped;
        target.Failed += source.Failed;
    }
}
