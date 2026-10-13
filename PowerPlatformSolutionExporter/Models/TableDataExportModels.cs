namespace PowerPlatformSolutionExporter.Models;

public sealed class TableDataExportColumn
{
    public required string CsvHeader { get; init; }
    public required string DisplayName { get; init; }
    public required string LogicalName { get; init; }
    public required string DataType { get; init; }
    public required string RequiredLevel { get; init; }
    public bool IsPrimaryId { get; init; }
    public bool IsPrimaryName { get; init; }
    public bool IsCustomAttribute { get; init; }
    public IReadOnlyList<string> LookupTargets { get; init; } = [];
    public string? DateTimeFormat { get; init; }
    public string? DateTimeBehavior { get; init; }
    public string? LookupTargetEntity { get; init; }
    public string? LookupTargetPrimaryId { get; init; }
    public string? LookupKeyAttribute { get; init; }
}

public sealed class SkippedTableDataExportColumn
{
    public required string DisplayName { get; init; }
    public required string LogicalName { get; init; }
    public required string DataType { get; init; }
    public required string Reason { get; init; }
}

public sealed class TableDataExportDefinition
{
    public required DataverseTableInfo Table { get; init; }
    public required IReadOnlyList<TableDataExportColumn> Columns { get; init; }
    public required IReadOnlyList<SkippedTableDataExportColumn> SkippedColumns { get; init; }
}

public sealed class TableDataExportPage
{
    public required IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows { get; init; }
    public bool HasMoreRecords { get; init; }
    public string? NextPagingCookie { get; init; }
}

public sealed class TableDataExportResult
{
    public required string OutputFolder { get; init; }
    public required int TableCount { get; init; }
    public required long RecordCount { get; init; }
    public required int SkippedColumnCount { get; init; }
    public bool DependencyCycleDetected { get; init; }
}
