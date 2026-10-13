using System.Globalization;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using PowerPlatformSolutionExporter.Models;

namespace PowerPlatformSolutionExporter.Services;

/// <summary>The table CSV contract observed in Power Apps Tables > Export data.</summary>
public static class WebUiCsvContract
{
    private static readonly HashSet<string> UnsupportedSystemColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "ownerid", "createdby", "createdonbehalfby", "createdon",
        "modifiedby", "modifiedonbehalfby", "modifiedon", "overriddencreatedon",
    };

    public static TableDataExportDefinition CreateDefinition(
        DataverseTableInfo table,
        IReadOnlyDictionary<string, EntityMetadata> metadata)
    {
        ArgumentNullException.ThrowIfNull(table);
        var columns = new List<TableDataExportColumn>();
        var skipped = new List<SkippedTableDataExportColumn>();
        foreach (var attribute in table.Metadata.Attributes.Where(item => !string.IsNullOrWhiteSpace(item.LogicalName))
                     .OrderByDescending(item => item.LogicalName == table.PrimaryIdAttribute)
                     .ThenBy(item => item.LogicalName, StringComparer.Ordinal))
        {
            var reason = GetUnsupportedReason(attribute, table.PrimaryIdAttribute);
            if (reason is not null)
            {
                skipped.Add(new SkippedTableDataExportColumn
                {
                    LogicalName = attribute.LogicalName,
                    DisplayName = attribute.DisplayName?.UserLocalizedLabel?.Label ?? attribute.LogicalName,
                    DataType = GetDataType(attribute),
                    Reason = reason,
                });
                continue;
            }

            var targets = (attribute as LookupAttributeMetadata)?.Targets ?? [];
            EntityMetadata? target = null;
            if (targets.Length == 1) metadata.TryGetValue(targets[0], out target);
            var key = target?.Keys?.FirstOrDefault(item => item.EntityKeyIndexStatus == EntityKeyIndexStatus.Active && item.KeyAttributes is { Length: > 0 });
            if (key is not null)
            {
                foreach (var keyAttribute in key.KeyAttributes)
                {
                    var keyMetadata = target!.Attributes.SingleOrDefault(item => item.LogicalName == keyAttribute)
                        ?? throw new InvalidDataException($"Missing alternate-key metadata for {target.LogicalName}.{keyAttribute}.");
                    if (keyMetadata.IsValidForRead != true)
                        throw new InvalidDataException($"Alternate-key column {target.LogicalName}.{keyAttribute} cannot be read.");
                    columns.Add(CreateColumn(table, attribute, keyMetadata, targets, target));
                }
            }
            else
            {
                columns.Add(CreateColumn(table, attribute, attribute, targets, target: null));
            }
        }

        if (columns.All(column => !column.IsPrimaryId))
            throw new InvalidDataException($"Table '{table.LogicalName}' does not expose its Primary ID.");
        return new TableDataExportDefinition { Table = table, Columns = columns, SkippedColumns = skipped };
    }

    private static TableDataExportColumn CreateColumn(
        DataverseTableInfo table, AttributeMetadata source, AttributeMetadata value,
        IReadOnlyList<string> targets, EntityMetadata? target)
    {
        return new TableDataExportColumn
        {
            CsvHeader = target is null ? source.LogicalName : $"{source.LogicalName}.{value.LogicalName}",
            LogicalName = source.LogicalName,
            DisplayName = source.DisplayName?.UserLocalizedLabel?.Label ?? source.LogicalName,
            DataType = GetDataType(value),
            RequiredLevel = source.RequiredLevel?.Value.ToString() ?? "None",
            IsPrimaryId = source.LogicalName == table.PrimaryIdAttribute,
            IsPrimaryName = source.IsPrimaryName == true,
            IsCustomAttribute = source.IsCustomAttribute == true,
            LookupTargets = targets,
            DateTimeFormat = (value as DateTimeAttributeMetadata)?.Format?.ToString(),
            DateTimeBehavior = (value as DateTimeAttributeMetadata)?.DateTimeBehavior?.Value,
            LookupTargetEntity = target?.LogicalName,
            LookupTargetPrimaryId = target?.PrimaryIdAttribute,
            LookupKeyAttribute = target is null ? null : value.LogicalName,
        };
    }

    private static string? GetUnsupportedReason(AttributeMetadata attribute, string primaryId)
    {
        if (attribute.LogicalName == primaryId) return null;
        if (attribute.IsValidForRead != true) return "The column is not readable.";
        if (UnsupportedSystemColumns.Contains(attribute.LogicalName)) return "Power Apps Web UI CSV export excludes this system field.";
        if (attribute.IsLogical == true || !string.IsNullOrWhiteSpace(attribute.AttributeOf))
            return "Power Apps Web UI CSV export excludes logical/companion columns.";
        if (attribute is FileAttributeMetadata || attribute.AttributeTypeName?.Value == "FileType") return "Power Apps Web UI CSV export does not support File columns.";
        if (attribute is ImageAttributeMetadata || attribute.AttributeTypeName?.Value == "ImageType") return "Power Apps Web UI CSV export does not support Image columns.";
        if (attribute is MultiSelectPicklistAttributeMetadata) return "Power Apps Web UI CSV export does not support multi-select Choices columns.";
        if (attribute is IntegerAttributeMetadata { Format: IntegerFormat.TimeZone }) return "Power Apps Web UI CSV export does not support Timezone columns.";
        if (attribute.AttributeType is AttributeTypeCode.Virtual or AttributeTypeCode.CalendarRules or AttributeTypeCode.ManagedProperty or AttributeTypeCode.PartyList or AttributeTypeCode.EntityName)
            return "The column type is not supported by Power Apps Web UI CSV export.";
        // Read-only stored columns (versionnumber, owningbusinessunit, *_base, exchangerate)
        // ARE included by Web UI. Create/update privileges must not determine export scope.
        return null;
    }

    private static string GetDataType(AttributeMetadata attribute) =>
        attribute.AttributeTypeName?.Value ?? attribute.AttributeType?.ToString() ?? "Unknown";

    public static string? FormatValue(object? value) => value switch
    {
        null => null,
        AliasedValue aliased => FormatValue(aliased.Value),
        EntityReference reference => reference.Id.ToString("D"),
        OptionSetValue option => option.Value.ToString(CultureInfo.InvariantCulture),
        bool boolean => boolean ? "True" : "False",
        Money money => money.Value.ToString("G29", CultureInfo.InvariantCulture),
        decimal number => number.ToString("G29", CultureInfo.InvariantCulture),
        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        Guid guid => guid.ToString("D"),
        byte[] bytes => Convert.ToBase64String(bytes),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        string text => text,
        _ => throw new InvalidDataException($"Unsupported raw CSV value type: {value.GetType().Name}."),
    };
}
