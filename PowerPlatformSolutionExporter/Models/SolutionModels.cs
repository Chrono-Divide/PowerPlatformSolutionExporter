using Microsoft.Xrm.Sdk.Metadata;

namespace PowerPlatformSolutionExporter.Models;

public sealed class SolutionInfo
{
    public required Guid Id { get; init; }
    public required string FriendlyName { get; init; }
    public required string UniqueName { get; init; }
    public required string Version { get; init; }
    public required string PublisherName { get; init; }
    public bool IsManaged { get; init; }

    public override string ToString() => $"{FriendlyName} ({UniqueName})";
}

public sealed class DataverseTableInfo
{
    public required string DisplayName { get; init; }
    public required string LogicalName { get; init; }
    public required string SchemaName { get; init; }
    public required string PrimaryIdAttribute { get; init; }
    public string? PrimaryNameAttribute { get; init; }
    public bool IsCustom { get; init; }
    public required EntityMetadata Metadata { get; init; }
}

public sealed class SolutionOverview
{
    public required SolutionInfo Solution { get; init; }
    public required IReadOnlyList<DataverseTableInfo> Tables { get; init; }
    public required IReadOnlyDictionary<int, int> ComponentCounts { get; init; }
    public required IReadOnlyList<CanvasAppPublishStatus> CanvasApps { get; init; }

    public int GetComponentCount(int componentType) =>
        ComponentCounts.TryGetValue(componentType, out var count) ? count : 0;
}

public sealed class CanvasAppPublishStatus
{
    public required Guid Id { get; init; }
    public required string DisplayName { get; init; }
    public DateTime? LastModifiedTime { get; init; }
    public DateTime? LastPublishTime { get; init; }

    public bool IsPublished => LastPublishTime.HasValue &&
                               (!LastModifiedTime.HasValue || LastPublishTime.Value >= LastModifiedTime.Value);
}

public enum DataverseColumnOrigin
{
    Custom,
    DataverseGenerated,
    DataverseSystem,
}

public sealed class TableColumnInfo
{
    public required string DisplayName { get; init; }
    public required string LogicalName { get; init; }
    public required string SchemaName { get; init; }
    public required string DataType { get; init; }
    public required string Details { get; init; }
    public required string RequiredLevel { get; init; }
    public bool IsPrimaryId { get; init; }
    public bool IsPrimaryName { get; init; }
    public bool IsCustomAttribute { get; init; }
    public required DataverseColumnOrigin Origin { get; init; }
    public bool IsValidForRead { get; init; }
    public bool IsValidForCreate { get; init; }
    public bool IsValidForUpdate { get; init; }

    public string OriginDisplayName => Origin switch
    {
        DataverseColumnOrigin.Custom => "Custom",
        DataverseColumnOrigin.DataverseGenerated => "Dataverse generated",
        DataverseColumnOrigin.DataverseSystem => "Dataverse system",
        _ => Origin.ToString(),
    };
}

public sealed class TableDataPage
{
    public required int PageNumber { get; init; }
    public required IReadOnlyList<TableColumnInfo> Columns { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows { get; init; }
    public bool HasMoreRecords { get; init; }
    public string? NextPagingCookie { get; init; }
}

public sealed class SolutionExportResult
{
    public required string FilePath { get; init; }
    public required string PreviousVersion { get; init; }
    public required string ExportedVersion { get; init; }
    public required string Sha256 { get; init; }
    public required int TableCount { get; init; }
    public required int ComponentCount { get; init; }
}

public static class SolutionVersion
{
    public static string IncrementRevision(string currentVersion)
    {
        if (!Version.TryParse(currentVersion, out var version))
        {
            throw new FormatException($"Solution version '{currentVersion}' is not a valid major.minor.build.revision value.");
        }

        var build = Math.Max(0, version.Build);
        var revision = checked(Math.Max(0, version.Revision) + 1);
        return new Version(version.Major, version.Minor, build, revision).ToString(4);
    }
}
