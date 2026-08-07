using System.Text;
using System.Text.Json;
using PowerPlatformSolutionExporter.Models;

namespace PowerPlatformSolutionExporter.Services;

public sealed class TableDataExporter
{
    private const int ManifestFormatVersion = 1;
    private static readonly UTF8Encoding CsvEncoding = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly ITableDataExportSource _source;

    public TableDataExporter(ITableDataExportSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public async Task<TableDataExportResult> ExportAsync(
        SolutionOverview overview,
        string exportRootFolder,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportRootFolder);
        if (overview.Tables.Count == 0)
        {
            throw new InvalidOperationException("The selected solution does not contain any tables to export.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(exportRootFolder);
        var createdLocal = DateTimeOffset.Now;
        var folderStem = $"{SanitizePathSegment(overview.Solution.UniqueName)}_TableData_{createdLocal:yyyyMMdd_HHmmss_fff}";
        var outputFolder = GetAvailableDirectoryPath(exportRootFolder, folderStem);
        var workingFolder = outputFolder + $".partial_{Guid.NewGuid():N}";
        Directory.CreateDirectory(workingFolder);

        try
        {
            var definitions = overview.Tables
                .Select(_source.GetTableDataExportDefinition)
                .ToDictionary(definition => definition.Table.LogicalName, StringComparer.OrdinalIgnoreCase);
            var importPlan = BuildImportPlan(definitions);
            var exportedTables = new List<ExportedTableManifest>();
            long totalRecordCount = 0;

            for (var index = 0; index < importPlan.OrderedLogicalNames.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var logicalName = importPlan.OrderedLogicalNames[index];
                var definition = definitions[logicalName];
                var fileName = $"{SanitizePathSegment(logicalName)}.csv";
                var filePath = Path.Combine(workingFolder, fileName);
                progress?.Report(
                    $"Exporting table {index + 1:N0}/{importPlan.OrderedLogicalNames.Count:N0}: " +
                    $"{definition.Table.DisplayName} ({logicalName})...");

                var recordCount = await WriteTableCsvAsync(
                    definition,
                    filePath,
                    progress,
                    cancellationToken);
                totalRecordCount += recordCount;
                exportedTables.Add(CreateTableManifest(
                    definition,
                    fileName,
                    index + 1,
                    recordCount,
                    importPlan.Dependencies[logicalName],
                    importPlan.ExternalLookupTargets[logicalName]));
            }

            var skippedColumnCount = exportedTables.Sum(table => table.SkippedColumns.Count);
            var manifest = new ExportManifest
            {
                FormatVersion = ManifestFormatVersion,
                CreatedUtc = DateTimeOffset.UtcNow,
                SourceEnvironmentName = _source.EnvironmentName,
                SourceEnvironmentUrl = _source.EnvironmentUrl,
                SolutionFriendlyName = overview.Solution.FriendlyName,
                SolutionUniqueName = overview.Solution.UniqueName,
                SolutionVersion = overview.Solution.Version,
                TableCount = exportedTables.Count,
                RecordCount = totalRecordCount,
                SkippedColumnCount = skippedColumnCount,
                DependencyCycleDetected = importPlan.DependencyCycleDetected,
                Tables = exportedTables,
            };
            await File.WriteAllTextAsync(
                Path.Combine(workingFolder, "manifest.json"),
                JsonSerializer.Serialize(manifest, JsonOptions),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(workingFolder, "import-guide.txt"),
                BuildImportGuide(manifest),
                CsvEncoding,
                cancellationToken);

            Directory.Move(workingFolder, outputFolder);
            progress?.Report($"Table data export completed: {outputFolder}");
            return new TableDataExportResult
            {
                OutputFolder = outputFolder,
                TableCount = exportedTables.Count,
                RecordCount = totalRecordCount,
                SkippedColumnCount = skippedColumnCount,
                DependencyCycleDetected = importPlan.DependencyCycleDetected,
            };
        }
        catch
        {
            TryDeleteWorkingDirectory(workingFolder);
            throw;
        }
    }

    private async Task<long> WriteTableCsvAsync(
        TableDataExportDefinition definition,
        string filePath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(filePath, append: false, CsvEncoding);
        await writer.WriteLineAsync(string.Join(",", definition.Columns.Select(column => EscapeCsv(column.CsvHeader))));

        long recordCount = 0;
        var pageNumber = 1;
        string? pagingCookie = null;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _source.RetrieveTableDataExportPageAsync(
                definition,
                pageNumber,
                pagingCookie,
                cancellationToken);
            foreach (var row in page.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = string.Join(",", definition.Columns.Select(column =>
                    EscapeCsv(row.TryGetValue(column.LogicalName, out var value) ? value : null)));
                await writer.WriteLineAsync(line);
                recordCount++;
            }

            progress?.Report($"{definition.Table.LogicalName}: {recordCount:N0} records exported.");
            if (!page.HasMoreRecords)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(page.NextPagingCookie) ||
                string.Equals(page.NextPagingCookie, pagingCookie, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Dataverse returned an invalid paging cookie while exporting '{definition.Table.LogicalName}'.");
            }

            pagingCookie = page.NextPagingCookie;
            pageNumber++;
        }
        while (true);

        return recordCount;
    }

    private static ImportPlan BuildImportPlan(
        IReadOnlyDictionary<string, TableDataExportDefinition> definitions)
    {
        var tableNames = definitions.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dependencies = definitions.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.Columns
                .SelectMany(column => column.LookupTargets)
                .Where(target => tableNames.Contains(target) &&
                                 !string.Equals(target, pair.Key, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(target => target, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
        var externalTargets = definitions.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.Columns
                .SelectMany(column => column.LookupTargets)
                .Where(target => !tableNames.Contains(target))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(target => target, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
        var remaining = dependencies.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToHashSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>(definitions.Count);

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(pair => pair.Value.Count == 0)
                .Select(pair => pair.Key)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ready.Count == 0)
            {
                break;
            }

            foreach (var name in ready)
            {
                ordered.Add(name);
                remaining.Remove(name);
                foreach (var unresolvedDependencies in remaining.Values)
                {
                    unresolvedDependencies.Remove(name);
                }
            }
        }

        var dependencyCycleDetected = remaining.Count > 0;
        if (dependencyCycleDetected)
        {
            ordered.AddRange(remaining.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        }

        return new ImportPlan
        {
            OrderedLogicalNames = ordered,
            Dependencies = dependencies,
            ExternalLookupTargets = externalTargets,
            DependencyCycleDetected = dependencyCycleDetected,
        };
    }

    private static ExportedTableManifest CreateTableManifest(
        TableDataExportDefinition definition,
        string fileName,
        int importOrder,
        long recordCount,
        IReadOnlyList<string> dependencies,
        IReadOnlyList<string> externalLookupTargets)
    {
        return new ExportedTableManifest
        {
            ImportOrder = importOrder,
            FileName = fileName,
            DisplayName = definition.Table.DisplayName,
            LogicalName = definition.Table.LogicalName,
            PrimaryIdAttribute = definition.Table.PrimaryIdAttribute,
            RecordCount = recordCount,
            Dependencies = dependencies,
            ExternalLookupTargets = externalLookupTargets,
            Columns = definition.Columns.Select(column => new ExportedColumnManifest
            {
                CsvHeader = column.CsvHeader,
                DisplayName = column.DisplayName,
                LogicalName = column.LogicalName,
                DataType = column.DataType,
                RequiredLevel = column.RequiredLevel,
                IsPrimaryId = column.IsPrimaryId,
                IsPrimaryName = column.IsPrimaryName,
                IsCustomAttribute = column.IsCustomAttribute,
                LookupTargets = column.LookupTargets,
                DateTimeFormat = column.DateTimeFormat,
                DateTimeBehavior = column.DateTimeBehavior,
            }).ToList(),
            SkippedColumns = definition.SkippedColumns.Select(column => new SkippedColumnManifest
            {
                DisplayName = column.DisplayName,
                LogicalName = column.LogicalName,
                DataType = column.DataType,
                Reason = column.Reason,
            }).ToList(),
        };
    }

    private static string BuildImportGuide(ExportManifest manifest)
    {
        var builder = new StringBuilder();
        builder.AppendLine("POWER PLATFORM SOLUTION EXPORTER - BROWSER CSV IMPORT GUIDE");
        builder.AppendLine();
        builder.AppendLine($"Solution: {manifest.SolutionFriendlyName} ({manifest.SolutionUniqueName})");
        builder.AppendLine($"Solution version at export: {manifest.SolutionVersion}");
        builder.AppendLine($"Source environment: {manifest.SourceEnvironmentName} ({manifest.SourceEnvironmentUrl})");
        builder.AppendLine($"Created UTC: {manifest.CreatedUtc:O}");
        builder.AppendLine($"Tables: {manifest.TableCount:N0}; records: {manifest.RecordCount:N0}; skipped columns: {manifest.SkippedColumnCount:N0}");
        builder.AppendLine();
        builder.AppendLine("IMPORTANT");
        builder.AppendLine("- These CSV files contain data only. If a table or column is missing, import the unmanaged solution ZIP first.");
        builder.AppendLine("- Browser CSV import inserts or updates rows; it does not delete rows that are absent from a CSV file.");
        builder.AppendLine("- Map the Primary ID GUID when you want existing rows to be updated instead of duplicated.");
        builder.AppendLine("- Lookup values use their Dataverse display value. Verify lookup mapping and import referenced tables first.");
        builder.AppendLine("- File, Image, multi-select Choices, Timezone, generated/read-only columns, and unsupported system fields are omitted.");
        builder.AppendLine("- Only values readable by the signed-in Dataverse account can be exported.");
        builder.AppendLine("- CSV and manifest files are unencrypted and can contain sensitive data. Store the entire folder securely.");
        builder.AppendLine("- Review manifest.json for exact logical names, data types, lookup targets, and every skipped column.");
        if (manifest.DependencyCycleDetected)
        {
            builder.AppendLine("- A lookup dependency cycle was detected. Import the listed files in order, then retry rows whose lookups could not resolve.");
        }

        builder.AppendLine();
        builder.AppendLine("BROWSER IMPORT STEPS");
        builder.AppendLine("1. Open https://make.powerapps.com and select the target environment.");
        builder.AppendLine("2. Open Tables, select the matching table, then choose Import > Import data and Text/CSV.");
        builder.AppendLine("3. Upload the CSV listed below and verify every column mapping before starting the import.");
        builder.AppendLine("4. Select/map the Primary ID for upsert when restoring existing rows. If no key is selected, rows can be inserted as duplicates.");
        builder.AppendLine("5. Review the browser import result and error log before continuing with dependent tables.");
        builder.AppendLine();
        builder.AppendLine("RECOMMENDED IMPORT ORDER");
        foreach (var table in manifest.Tables.OrderBy(table => table.ImportOrder))
        {
            builder.Append($"{table.ImportOrder}. {table.DisplayName} ({table.LogicalName}) - {table.FileName} - {table.RecordCount:N0} records");
            if (table.Dependencies.Count > 0)
            {
                builder.Append($"; depends on: {string.Join(", ", table.Dependencies)}");
            }

            if (table.ExternalLookupTargets.Count > 0)
            {
                builder.Append($"; external lookup targets: {string.Join(", ", table.ExternalLookupTargets)}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    internal static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string GetAvailableDirectoryPath(string parentFolder, string folderStem)
    {
        var candidate = Path.Combine(parentFolder, folderStem);
        for (var suffix = 2; Directory.Exists(candidate) || File.Exists(candidate); suffix++)
        {
            candidate = Path.Combine(parentFolder, $"{folderStem}_{suffix}");
        }

        return candidate;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Dataverse" : sanitized;
    }

    private static void TryDeleteWorkingDirectory(string workingFolder)
    {
        try
        {
            if (Directory.Exists(workingFolder))
            {
                Directory.Delete(workingFolder, recursive: true);
            }
        }
        catch (IOException)
        {
            // Preserve the original export failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve the original export failure.
        }
    }

    private sealed class ImportPlan
    {
        public required IReadOnlyList<string> OrderedLogicalNames { get; init; }
        public required IReadOnlyDictionary<string, IReadOnlyList<string>> Dependencies { get; init; }
        public required IReadOnlyDictionary<string, IReadOnlyList<string>> ExternalLookupTargets { get; init; }
        public bool DependencyCycleDetected { get; init; }
    }

    private sealed class ExportManifest
    {
        public int FormatVersion { get; init; }
        public DateTimeOffset CreatedUtc { get; init; }
        public required string SourceEnvironmentName { get; init; }
        public required string SourceEnvironmentUrl { get; init; }
        public required string SolutionFriendlyName { get; init; }
        public required string SolutionUniqueName { get; init; }
        public required string SolutionVersion { get; init; }
        public int TableCount { get; init; }
        public long RecordCount { get; init; }
        public int SkippedColumnCount { get; init; }
        public bool DependencyCycleDetected { get; init; }
        public required IReadOnlyList<ExportedTableManifest> Tables { get; init; }
    }

    private sealed class ExportedTableManifest
    {
        public int ImportOrder { get; init; }
        public required string FileName { get; init; }
        public required string DisplayName { get; init; }
        public required string LogicalName { get; init; }
        public required string PrimaryIdAttribute { get; init; }
        public long RecordCount { get; init; }
        public required IReadOnlyList<string> Dependencies { get; init; }
        public required IReadOnlyList<string> ExternalLookupTargets { get; init; }
        public required IReadOnlyList<ExportedColumnManifest> Columns { get; init; }
        public required IReadOnlyList<SkippedColumnManifest> SkippedColumns { get; init; }
    }

    private sealed class ExportedColumnManifest
    {
        public required string CsvHeader { get; init; }
        public required string DisplayName { get; init; }
        public required string LogicalName { get; init; }
        public required string DataType { get; init; }
        public required string RequiredLevel { get; init; }
        public bool IsPrimaryId { get; init; }
        public bool IsPrimaryName { get; init; }
        public bool IsCustomAttribute { get; init; }
        public required IReadOnlyList<string> LookupTargets { get; init; }
        public string? DateTimeFormat { get; init; }
        public string? DateTimeBehavior { get; init; }
    }

    private sealed class SkippedColumnManifest
    {
        public required string DisplayName { get; init; }
        public required string LogicalName { get; init; }
        public required string DataType { get; init; }
        public required string Reason { get; init; }
    }
}
