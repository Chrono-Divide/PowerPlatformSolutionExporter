using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;

namespace dataverse_table_backup_restore.Models;

public sealed class DataverseTableInfo
{
    public required string DisplayName { get; init; }
    public required string LogicalName { get; init; }
    public required string SchemaName { get; init; }
    public required string PrimaryIdAttribute { get; init; }
    public string? PrimaryNameAttribute { get; init; }
    public string? EntitySetName { get; init; }
    public bool IsCustom { get; init; }
    public bool IsActivity { get; init; }
    public bool IsDesignTable => DesignTableCatalog.Contains(LogicalName);

    [JsonIgnore]
    public required EntityMetadata Metadata { get; init; }
}

public sealed class SnapshotManifest
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;
    public required string ApplicationVersion { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required string EnvironmentUrl { get; init; }
    public required string Scope { get; init; }
    public required string Content { get; init; }
    public required string SolutionUniqueName { get; init; }
    public bool IncludesStructure { get; init; }
    public bool IncludesData { get; init; }
    public string? SolutionSha256 { get; init; }
    public required List<SnapshotTableManifest> Tables { get; init; }
    public required List<string> Warnings { get; init; }
}

public sealed class SnapshotTableManifest
{
    public required string DisplayName { get; init; }
    public required string LogicalName { get; init; }
    public required string FileName { get; init; }
    public bool IsCustom { get; init; }
    public int AttributeCount { get; init; }
    public int RecordCount { get; init; }
    public required string Sha256 { get; init; }
}

public sealed class TableSnapshot
{
    public required TableSchemaSnapshot Schema { get; init; }
    public required List<RecordSnapshot> Records { get; init; }
}

public sealed class TableSchemaSnapshot
{
    public required string DisplayName { get; init; }
    public required string LogicalName { get; init; }
    public required string SchemaName { get; init; }
    public required string PrimaryIdAttribute { get; init; }
    public string? PrimaryNameAttribute { get; init; }
    public string? EntitySetName { get; init; }
    public bool IsCustom { get; init; }
    public bool IsActivity { get; init; }
    public required string OwnershipType { get; init; }
    public required List<AttributeSchemaSnapshot> Attributes { get; init; }
}

public sealed class AttributeSchemaSnapshot
{
    public required string LogicalName { get; init; }
    public required string SchemaName { get; init; }
    public required string DisplayName { get; init; }
    public required string AttributeType { get; init; }
    public required string RequiredLevel { get; init; }
    public bool IsPrimaryId { get; init; }
    public bool IsPrimaryName { get; init; }
    public bool IsValidForRead { get; init; }
    public bool IsValidForCreate { get; init; }
    public bool IsValidForUpdate { get; init; }
    public bool IsSecured { get; init; }
    public bool IsRestorable { get; init; }
    public string[] Targets { get; init; } = [];
}

public sealed class RecordSnapshot
{
    public required Guid Id { get; init; }
    public required Dictionary<string, SnapshotValue> Attributes { get; init; }
}

public sealed class SnapshotValue
{
    public required string Type { get; init; }
    public string? Value { get; init; }
    public string? LogicalName { get; init; }
    public Guid? Id { get; init; }
    public string? Name { get; init; }
    public List<int>? Values { get; init; }

    public bool IsReference => Type == "EntityReference";
    public bool IsUnsupported => Type == "Unsupported";

    public static SnapshotValue FromSdkValue(object? value)
    {
        if (value is null)
        {
            return new SnapshotValue { Type = "Null" };
        }

        if (value is AliasedValue aliased)
        {
            return FromSdkValue(aliased.Value);
        }

        return value switch
        {
            string text => new SnapshotValue { Type = "String", Value = text },
            bool flag => new SnapshotValue { Type = "Boolean", Value = flag ? "true" : "false" },
            int number => new SnapshotValue { Type = "Int32", Value = number.ToString(CultureInfo.InvariantCulture) },
            long number => new SnapshotValue { Type = "Int64", Value = number.ToString(CultureInfo.InvariantCulture) },
            decimal number => new SnapshotValue { Type = "Decimal", Value = number.ToString(CultureInfo.InvariantCulture) },
            double number => new SnapshotValue { Type = "Double", Value = number.ToString("R", CultureInfo.InvariantCulture) },
            float number => new SnapshotValue { Type = "Double", Value = number.ToString("R", CultureInfo.InvariantCulture) },
            Guid guid => new SnapshotValue { Type = "Guid", Value = guid.ToString("D") },
            DateTime dateTime => new SnapshotValue { Type = "DateTime", Value = dateTime.ToString("O", CultureInfo.InvariantCulture) },
            DateTimeOffset dateTimeOffset => new SnapshotValue { Type = "DateTimeOffset", Value = dateTimeOffset.ToString("O", CultureInfo.InvariantCulture) },
            Money money => new SnapshotValue { Type = "Money", Value = money.Value.ToString(CultureInfo.InvariantCulture) },
            OptionSetValue option => new SnapshotValue { Type = "OptionSet", Value = option.Value.ToString(CultureInfo.InvariantCulture) },
            OptionSetValueCollection options => new SnapshotValue { Type = "MultiSelectOptionSet", Values = options.Select(item => item.Value).ToList() },
            EntityReference reference => new SnapshotValue
            {
                Type = "EntityReference",
                LogicalName = reference.LogicalName,
                Id = reference.Id,
                Name = reference.Name,
            },
            byte[] bytes => new SnapshotValue { Type = "ByteArray", Value = Convert.ToBase64String(bytes) },
            _ => new SnapshotValue { Type = "Unsupported", Value = value.GetType().FullName },
        };
    }

    public object? ToSdkValue()
    {
        return Type switch
        {
            "Null" => null,
            "String" => Value,
            "Boolean" => string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase),
            "Int32" => int.Parse(Value!, CultureInfo.InvariantCulture),
            "Int64" => long.Parse(Value!, CultureInfo.InvariantCulture),
            "Decimal" => decimal.Parse(Value!, CultureInfo.InvariantCulture),
            "Double" => double.Parse(Value!, CultureInfo.InvariantCulture),
            "Guid" => Guid.Parse(Value!),
            "DateTime" => DateTime.Parse(Value!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "DateTimeOffset" => DateTimeOffset.Parse(Value!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "Money" => new Money(decimal.Parse(Value!, CultureInfo.InvariantCulture)),
            "OptionSet" => new OptionSetValue(int.Parse(Value!, CultureInfo.InvariantCulture)),
            "MultiSelectOptionSet" => new OptionSetValueCollection((Values ?? []).Select(item => new OptionSetValue(item)).ToList()),
            "EntityReference" when Id.HasValue && !string.IsNullOrWhiteSpace(LogicalName) => new EntityReference(LogicalName, Id.Value) { Name = Name },
            "ByteArray" => Convert.FromBase64String(Value ?? string.Empty),
            "Unsupported" => throw new NotSupportedException($"Snapshot value type '{Value}' is not supported."),
            _ => throw new InvalidDataException($"Unknown snapshot value type '{Type}'."),
        };
    }
}

public sealed class SnapshotCreateOptions
{
    public required string EnvironmentUrl { get; init; }
    public required string OutputFolder { get; init; }
    public required string Scope { get; init; }
    public required string Content { get; init; }
    public required string SolutionUniqueName { get; init; }
    public required IReadOnlyList<DataverseTableInfo> Tables { get; init; }
    public bool IncludeStructure { get; init; }
    public bool IncludeData { get; init; }
}

public sealed class RestoreOptions
{
    public required string EnvironmentUrl { get; init; }
    public required string SnapshotFile { get; init; }
    public bool RestoreStructure { get; init; }
    public bool RestoreData { get; init; }
    public bool DeleteRecordsNotInSnapshot { get; init; }
}

public sealed class OperationSummary
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}
