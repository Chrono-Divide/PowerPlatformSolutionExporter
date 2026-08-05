using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
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
            var warnings = new List<string>();
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

                var solutionTableNames = ReadSolutionTableNames(solutionPath);
                var selectedTableNames = options.Tables
                    .Select(table => table.LogicalName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                ValidateSolutionCoverage(selectedTableNames, solutionTableNames, "The exported solution");
                var additionalTableCount = solutionTableNames.Count(name => !selectedTableNames.Contains(name));
                if (additionalTableCount > 0)
                {
                    warnings.Add(
                        $"solution.zip contains {additionalTableCount:N0} additional table components. Structure restore imports the entire solution, not only the tables selected for data capture.");
                }
            }

            var tableManifests = new List<SnapshotTableManifest>();
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
            var prepared = await ValidateExtractedSnapshotAsync(
                temporaryDirectory,
                manifest,
                options.RestoreStructure,
                options.RestoreData,
                cancellationToken);

            if (options.RestoreStructure)
            {
                await _pacCliService.ImportSolutionAsync(
                    options.EnvironmentUrl,
                    prepared.SolutionPath!,
                    progress,
                    cancellationToken);
                await _dataverseService.RefreshMetadataAsync(progress, cancellationToken);
            }

            var summary = new OperationSummary();
            if (!options.RestoreData)
            {
                return (manifest, summary);
            }

            if (!options.RestoreStructure)
            {
                await _dataverseService.RefreshMetadataAsync(progress, cancellationToken);
            }

            _dataverseService.ValidateRestoreTargets(prepared.TableSnapshots);
            var tableSnapshots = OrderByDependencies(prepared.TableSnapshots);
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
                    Merge(summary, await _dataverseService.DeleteRecordsNotInSnapshotAsync(tableSnapshot, progress, cancellationToken));
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

    public async Task<SnapshotValidationResult> ValidateSnapshotAsync(
        string snapshotFile,
        string targetEnvironmentUrl,
        bool requireStructure,
        bool requireData,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(snapshotFile))
        {
            throw new FileNotFoundException("Snapshot file was not found.", snapshotFile);
        }

        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            ZipFile.ExtractToDirectory(snapshotFile, temporaryDirectory);
            var manifest = await LoadManifestAsync(Path.Combine(temporaryDirectory, ManifestFileName), cancellationToken);
            ValidateManifest(manifest, targetEnvironmentUrl);
            var prepared = await ValidateExtractedSnapshotAsync(
                temporaryDirectory,
                manifest,
                requireStructure,
                requireData,
                cancellationToken);
            var manifestTableNames = manifest.Tables
                .Select(table => table.LogicalName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return new SnapshotValidationResult
            {
                Manifest = manifest,
                SolutionTableCount = prepared.SolutionTableNames.Count,
                AdditionalSolutionTableCount = prepared.SolutionTableNames.Count(name => !manifestTableNames.Contains(name)),
                TotalRecordCount = prepared.TableSnapshots.Sum(table => table.Records.Count),
            };
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
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

    private async Task<PreparedSnapshot> ValidateExtractedSnapshotAsync(
        string temporaryDirectory,
        SnapshotManifest manifest,
        bool requireStructure,
        bool requireData,
        CancellationToken cancellationToken)
    {
        ValidateManifestTableList(manifest);

        string? solutionPath = null;
        IReadOnlySet<string> solutionTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (requireStructure)
        {
            if (!manifest.IncludesStructure)
            {
                throw new InvalidOperationException("This snapshot does not contain a solution package for structure restore.");
            }

            solutionPath = Path.Combine(temporaryDirectory, SolutionFileName);
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

            solutionTableNames = ReadSolutionTableNames(solutionPath);
            ValidateSolutionCoverage(
                manifest.Tables.Select(table => table.LogicalName),
                solutionTableNames,
                "The snapshot solution");
        }

        var tableSnapshots = new List<TableSnapshot>();
        if (requireData)
        {
            if (!manifest.IncludesData)
            {
                throw new InvalidOperationException("This snapshot does not contain table data.");
            }

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
                ValidateTableSnapshot(tableManifest, snapshot);
                tableSnapshots.Add(snapshot);
            }
        }

        return new PreparedSnapshot(solutionPath, solutionTableNames, tableSnapshots);
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

    private static void ValidateManifestTableList(SnapshotManifest manifest)
    {
        if (manifest.Tables.Count == 0)
        {
            throw new InvalidDataException("The snapshot manifest does not contain any tables.");
        }

        var duplicateLogicalName = manifest.Tables
            .GroupBy(table => table.LogicalName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        if (duplicateLogicalName is not null)
        {
            throw new InvalidDataException($"The snapshot contains duplicate table '{duplicateLogicalName}'.");
        }

        var duplicateFileName = manifest.Tables
            .GroupBy(table => table.FileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        if (duplicateFileName is not null)
        {
            throw new InvalidDataException($"The snapshot contains duplicate table file '{duplicateFileName}'.");
        }
    }

    private static void ValidateTableSnapshot(SnapshotTableManifest tableManifest, TableSnapshot snapshot)
    {
        if (snapshot.Schema is null || snapshot.Schema.Attributes is null || snapshot.Records is null)
        {
            throw new InvalidDataException($"Table snapshot '{tableManifest.LogicalName}' is missing schema or record collections.");
        }

        if (!string.Equals(tableManifest.LogicalName, snapshot.Schema.LogicalName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Table manifest '{tableManifest.LogicalName}' does not match schema '{snapshot.Schema.LogicalName}'.");
        }

        if (!snapshot.Schema.IsCustom)
        {
            throw new InvalidDataException($"Snapshot table '{snapshot.Schema.LogicalName}' is not marked as custom.");
        }

        if (tableManifest.AttributeCount != snapshot.Schema.Attributes.Count)
        {
            throw new InvalidDataException(
                $"Attribute count mismatch for '{tableManifest.LogicalName}': manifest {tableManifest.AttributeCount:N0}, file {snapshot.Schema.Attributes.Count:N0}.");
        }

        if (tableManifest.RecordCount != snapshot.Records.Count)
        {
            throw new InvalidDataException(
                $"Record count mismatch for '{tableManifest.LogicalName}': manifest {tableManifest.RecordCount:N0}, file {snapshot.Records.Count:N0}.");
        }

        var attributeNames = snapshot.Schema.Attributes
            .Select(attribute => attribute.LogicalName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (attributeNames.Count != snapshot.Schema.Attributes.Count)
        {
            throw new InvalidDataException($"Snapshot schema '{tableManifest.LogicalName}' contains duplicate attributes.");
        }

        var recordIds = new HashSet<Guid>();
        foreach (var record in snapshot.Records)
        {
            if (record is null || record.Attributes is null)
            {
                throw new InvalidDataException($"Snapshot table '{tableManifest.LogicalName}' contains an invalid record object.");
            }

            if (record.Id == Guid.Empty || !recordIds.Add(record.Id))
            {
                throw new InvalidDataException($"Snapshot table '{tableManifest.LogicalName}' contains an empty or duplicate record ID.");
            }

            foreach (var attribute in record.Attributes)
            {
                if (!attributeNames.Contains(attribute.Key))
                {
                    throw new InvalidDataException(
                        $"Record {record.Id:D} in '{tableManifest.LogicalName}' contains unknown attribute '{attribute.Key}'.");
                }

                if (attribute.Value is null)
                {
                    throw new InvalidDataException(
                        $"Record {record.Id:D} in '{tableManifest.LogicalName}' contains a null snapshot value object for '{attribute.Key}'.");
                }

                if (attribute.Value.IsUnsupported)
                {
                    continue;
                }

                try
                {
                    _ = attribute.Value.ToSdkValue();
                }
                catch (Exception exception) when (exception is FormatException or InvalidDataException or NotSupportedException)
                {
                    throw new InvalidDataException(
                        $"Record {record.Id:D} in '{tableManifest.LogicalName}' contains invalid snapshot value for '{attribute.Key}'.",
                        exception);
                }
            }
        }
    }

    private static IReadOnlySet<string> ReadSolutionTableNames(string solutionPath)
    {
        using var archive = ZipFile.OpenRead(solutionPath);
        var customizationsEntry = archive.GetEntry("customizations.xml")
                                  ?? throw new InvalidDataException("solution.zip does not contain customizations.xml.");
        using var stream = customizationsEntry.Open();
        var document = XDocument.Load(stream, LoadOptions.None);
        var entities = document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "Entities", StringComparison.OrdinalIgnoreCase))
            .SelectMany(element => element.Elements())
            .Where(element => string.Equals(element.Name.LocalName, "Entity", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Elements().FirstOrDefault(child =>
                string.Equals(child.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase))?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (entities.Count == 0)
        {
            throw new InvalidDataException("solution.zip does not contain any table components.");
        }

        return entities;
    }

    private static void ValidateSolutionCoverage(
        IEnumerable<string> requiredTableNames,
        IReadOnlySet<string> solutionTableNames,
        string sourceDescription)
    {
        var missing = requiredTableNames
            .Where(name => !solutionTableNames.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missing.Count > 0)
        {
            throw new InvalidDataException(
                $"{sourceDescription} does not contain structure for {missing.Count:N0} selected table(s): {string.Join(", ", missing)}.");
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

    private sealed record PreparedSnapshot(
        string? SolutionPath,
        IReadOnlySet<string> SolutionTableNames,
        List<TableSnapshot> TableSnapshots);
}
