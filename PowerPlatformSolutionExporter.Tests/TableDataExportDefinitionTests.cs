using System.Reflection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using PowerPlatformSolutionExporter.Models;
using PowerPlatformSolutionExporter.Services;
using Xunit;

namespace PowerPlatformSolutionExporter.Tests;

public sealed class TableDataExportDefinitionTests
{
    [Fact]
    public void DefinitionKeepsImportableColumnsAndDocumentsUnsupportedColumns()
    {
        var primaryId = new UniqueIdentifierAttributeMetadata
        {
            LogicalName = "new_sampleid",
            SchemaName = "new_SampleId",
            DisplayName = new Microsoft.Xrm.Sdk.Label("Sample", 1033),
        };
        SetAttributeFlags(primaryId, isRead: true, isCreate: false, isUpdate: false, isCustom: false);
        SetMetadataProperty(primaryId, nameof(AttributeMetadata.IsPrimaryId), true);

        var name = new StringAttributeMetadata
        {
            LogicalName = "new_name",
            SchemaName = "new_Name",
            DisplayName = new Microsoft.Xrm.Sdk.Label("Name", 1033),
        };
        SetAttributeFlags(name, isRead: true, isCreate: true, isUpdate: true, isCustom: true);
        SetMetadataProperty(name, nameof(AttributeMetadata.IsPrimaryName), true);

        var duplicateName = new StringAttributeMetadata
        {
            LogicalName = "new_alias",
            SchemaName = "new_Alias",
            DisplayName = new Microsoft.Xrm.Sdk.Label("Name", 1033),
        };
        SetAttributeFlags(duplicateName, isRead: true, isCreate: true, isUpdate: true, isCustom: true);

        var lookup = new LookupAttributeMetadata
        {
            LogicalName = "new_parentid",
            SchemaName = "new_ParentId",
            DisplayName = new Microsoft.Xrm.Sdk.Label("Parent", 1033),
        };
        SetAttributeFlags(lookup, isRead: true, isCreate: true, isUpdate: true, isCustom: true);
        SetMetadataProperty(lookup, nameof(LookupAttributeMetadata.Targets), new[] { "new_parent" });

        var createdOn = new DateTimeAttributeMetadata
        {
            LogicalName = "createdon",
            SchemaName = "CreatedOn",
            DisplayName = new Microsoft.Xrm.Sdk.Label("Created On", 1033),
        };
        SetAttributeFlags(createdOn, isRead: true, isCreate: false, isUpdate: false, isCustom: false);

        var file = new FileAttributeMetadata
        {
            LogicalName = "new_file",
            SchemaName = "new_File",
            DisplayName = new Microsoft.Xrm.Sdk.Label("File", 1033),
        };
        SetAttributeFlags(file, isRead: true, isCreate: true, isUpdate: true, isCustom: true);

        var multiSelect = new MultiSelectPicklistAttributeMetadata
        {
            LogicalName = "new_choices",
            SchemaName = "new_Choices",
            DisplayName = new Microsoft.Xrm.Sdk.Label("Choices", 1033),
        };
        SetAttributeFlags(multiSelect, isRead: true, isCreate: true, isUpdate: true, isCustom: true);

        var timezone = new IntegerAttributeMetadata
        {
            LogicalName = "new_timezone",
            SchemaName = "new_Timezone",
            DisplayName = new Microsoft.Xrm.Sdk.Label("Timezone", 1033),
            Format = IntegerFormat.TimeZone,
        };
        SetAttributeFlags(timezone, isRead: true, isCreate: true, isUpdate: true, isCustom: true);

        var calculated = new StringAttributeMetadata
        {
            LogicalName = "new_calculated",
            SchemaName = "new_Calculated",
            DisplayName = new Microsoft.Xrm.Sdk.Label("Calculated", 1033),
        };
        SetAttributeFlags(calculated, isRead: true, isCreate: false, isUpdate: false, isCustom: true);

        var metadata = new EntityMetadata
        {
            LogicalName = "new_sample",
            SchemaName = "new_Sample",
        };
        SetMetadataProperty(metadata, nameof(EntityMetadata.PrimaryIdAttribute), "new_sampleid");
        SetMetadataProperty(
            metadata,
            nameof(EntityMetadata.Attributes),
            new AttributeMetadata[]
            {
                createdOn,
                file,
                multiSelect,
                timezone,
                calculated,
                lookup,
                duplicateName,
                name,
                primaryId,
            });
        var table = new DataverseTableInfo
        {
            DisplayName = "Sample",
            LogicalName = "new_sample",
            SchemaName = "new_Sample",
            PrimaryIdAttribute = "new_sampleid",
            PrimaryNameAttribute = "new_name",
            IsCustom = true,
            Metadata = metadata,
        };

        using var service = new DataverseService();
        var definition = service.GetTableDataExportDefinition(table);

        Assert.Equal(
            ["new_sampleid", "new_name", "new_alias", "new_parentid"],
            definition.Columns.Select(column => column.LogicalName));
        Assert.Equal("Name (new_alias)", definition.Columns.Single(column => column.LogicalName == "new_alias").CsvHeader);
        Assert.Equal("Name (new_name)", definition.Columns.Single(column => column.LogicalName == "new_name").CsvHeader);
        Assert.Equal(
            ["new_parent"],
            definition.Columns.Single(column => column.LogicalName == "new_parentid").LookupTargets);
        Assert.Contains(definition.SkippedColumns, column => column.LogicalName == "createdon" && column.Reason.Contains("system field"));
        Assert.Contains(definition.SkippedColumns, column => column.LogicalName == "new_file" && column.Reason.Contains("File"));
        Assert.Contains(definition.SkippedColumns, column => column.LogicalName == "new_choices" && column.Reason.Contains("multi-select"));
        Assert.Contains(definition.SkippedColumns, column => column.LogicalName == "new_timezone" && column.Reason.Contains("Timezone"));
        Assert.Contains(definition.SkippedColumns, column => column.LogicalName == "new_calculated" && column.Reason.Contains("read-only"));
    }

    [Fact]
    public void BrowserImportValuesUseLabelsAndStableInvariantRepresentations()
    {
        var defaultColumn = Column("StringType");
        var userLocalDate = Column("DateTimeType", dateTimeBehavior: DateTimeBehavior.UserLocal.Value);
        var dateOnly = Column("DateTimeType", dateTimeFormat: DateTimeFormat.DateOnly.ToString());
        var lookupId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        Assert.Equal("Active", DataverseService.FormatBrowserImportValue(new OptionSetValue(1), "Active", defaultColumn));
        Assert.Equal("Yes", DataverseService.FormatBrowserImportValue(true, "Yes", defaultColumn));
        Assert.Equal("Parent", DataverseService.FormatBrowserImportValue(
            new EntityReference("new_parent", lookupId) { Name = "Parent" },
            "Parent",
            defaultColumn));
        Assert.Equal("12.34", DataverseService.FormatBrowserImportValue(new Money(12.34m), "$12.34", defaultColumn));
        Assert.Equal(
            "2026-08-07T01:02:03.000Z",
            DataverseService.FormatBrowserImportValue(
                new DateTime(2026, 8, 7, 1, 2, 3, DateTimeKind.Utc),
                formattedValue: null,
                userLocalDate));
        Assert.Equal(
            "2026-08-07",
            DataverseService.FormatBrowserImportValue(
                new DateTime(2026, 8, 7),
                formattedValue: null,
                dateOnly));
    }

    private static TableDataExportColumn Column(
        string dataType,
        string? dateTimeFormat = null,
        string? dateTimeBehavior = null)
    {
        return new TableDataExportColumn
        {
            CsvHeader = "Value",
            DisplayName = "Value",
            LogicalName = "new_value",
            DataType = dataType,
            RequiredLevel = "None",
            DateTimeFormat = dateTimeFormat,
            DateTimeBehavior = dateTimeBehavior,
        };
    }

    private static void SetAttributeFlags(
        AttributeMetadata attribute,
        bool isRead,
        bool isCreate,
        bool isUpdate,
        bool isCustom)
    {
        SetMetadataProperty(attribute, nameof(AttributeMetadata.IsValidForRead), isRead);
        SetMetadataProperty(attribute, nameof(AttributeMetadata.IsValidForCreate), isCreate);
        SetMetadataProperty(attribute, nameof(AttributeMetadata.IsValidForUpdate), isUpdate);
        SetMetadataProperty(attribute, nameof(AttributeMetadata.IsCustomAttribute), isCustom);
    }

    private static void SetMetadataProperty(object target, string propertyName, object value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                       ?? throw new InvalidOperationException($"Metadata property '{propertyName}' was not found.");
        property.SetValue(target, value);
    }
}
