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
    public void DefinitionMatchesWebUiScopeInsteadOfFilteringByWritePrivileges()
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
            ["new_sampleid", "new_alias", "new_calculated", "new_name", "new_parentid"],
            definition.Columns.Select(column => column.LogicalName));
        Assert.Equal("new_alias", definition.Columns.Single(column => column.LogicalName == "new_alias").CsvHeader);
        Assert.Equal("new_name", definition.Columns.Single(column => column.LogicalName == "new_name").CsvHeader);
        Assert.Equal(
            ["new_parent"],
            definition.Columns.Single(column => column.LogicalName == "new_parentid").LookupTargets);
        Assert.Contains(definition.SkippedColumns, column => column.LogicalName == "createdon" && column.Reason.Contains("system field"));
        Assert.Contains(definition.SkippedColumns, column => column.LogicalName == "new_file" && column.Reason.Contains("File"));
        Assert.Contains(definition.SkippedColumns, column => column.LogicalName == "new_choices" && column.Reason.Contains("multi-select"));
        Assert.Contains(definition.SkippedColumns, column => column.LogicalName == "new_timezone" && column.Reason.Contains("Timezone"));
        Assert.Contains(definition.Columns, column => column.LogicalName == "new_calculated");
    }

    [Fact]
    public void RawValuesMatchCapturedWebUiFormatsRegardlessOfDisplayLabels()
    {
        var lookupId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        Assert.Equal("1", WebUiCsvContract.FormatValue(new OptionSetValue(1)));
        Assert.Equal("True", WebUiCsvContract.FormatValue(true));
        Assert.Equal("False", WebUiCsvContract.FormatValue(false));
        Assert.Equal(lookupId.ToString("D"), WebUiCsvContract.FormatValue(new EntityReference("new_parent", lookupId) { Name = "Parent" }));
        Assert.Equal("12.34", WebUiCsvContract.FormatValue(new Money(12.3400000000m)));
        Assert.Equal("0", WebUiCsvContract.FormatValue(new Money(0.0000000000m)));
        Assert.Equal("1", WebUiCsvContract.FormatValue(1.0000000000m));
        Assert.Equal(
            "2026-08-07 01:02:03.0000000",
            WebUiCsvContract.FormatValue(new DateTime(2026, 8, 7, 1, 2, 3, DateTimeKind.Utc)));
        Assert.Equal(
            "2026-08-07 00:00:00.0000000",
            WebUiCsvContract.FormatValue(new DateTime(2026, 8, 7)));
        Assert.Equal("2026-08-07 00:00:00.1234567", WebUiCsvContract.FormatValue(new DateTime(2026, 8, 7).AddTicks(1234567)));
        Assert.Null(WebUiCsvContract.FormatValue(null));
        Assert.Equal("line 1, \"quoted\"\r\nline 2", WebUiCsvContract.FormatValue("line 1, \"quoted\"\r\nline 2"));
    }

    [Fact]
    public void LookupUsesActiveAlternateKeyAttributesNotDisplayNameOrGuid()
    {
        var id = new UniqueIdentifierAttributeMetadata { LogicalName = "new_childid" };
        SetAttributeFlags(id, true, false, false, false);
        var lookup = new LookupAttributeMetadata { LogicalName = "new_parentid" };
        SetAttributeFlags(lookup, true, true, true, true);
        SetMetadataProperty(lookup, nameof(LookupAttributeMetadata.Targets), new[] { "new_parent" });
        var code = new StringAttributeMetadata { LogicalName = "new_code" };
        var region = new IntegerAttributeMetadata { LogicalName = "new_region" };
        SetAttributeFlags(code, true, true, true, true);
        SetAttributeFlags(region, true, true, true, true);
        var key = new EntityKeyMetadata { LogicalName = "new_business_key", KeyAttributes = ["new_code", "new_region"] };
        SetMetadataProperty(key, nameof(EntityKeyMetadata.EntityKeyIndexStatus), EntityKeyIndexStatus.Active);
        var parent = new EntityMetadata { LogicalName = "new_parent" };
        SetMetadataProperty(parent, nameof(EntityMetadata.PrimaryIdAttribute), "new_parentid");
        SetMetadataProperty(parent, nameof(EntityMetadata.Attributes), new AttributeMetadata[] { code, region });
        SetMetadataProperty(parent, nameof(EntityMetadata.Keys), new[] { key });
        var child = new EntityMetadata { LogicalName = "new_child" };
        SetMetadataProperty(child, nameof(EntityMetadata.Attributes), new AttributeMetadata[] { id, lookup });
        var table = new DataverseTableInfo
        {
            LogicalName = "new_child", SchemaName = "new_Child", DisplayName = "Child",
            PrimaryIdAttribute = "new_childid", Metadata = child,
        };
        var definition = WebUiCsvContract.CreateDefinition(table, new Dictionary<string, EntityMetadata> { ["new_parent"] = parent });
        Assert.Equal(["new_childid", "new_parentid.new_code", "new_parentid.new_region"], definition.Columns.Select(column => column.CsvHeader));
        Assert.Equal(["StringType", "IntegerType"], definition.Columns.Skip(1).Select(column => column.DataType));
        Assert.All(definition.Columns.Skip(1), column => Assert.Equal("new_parentid", column.LogicalName));

        SetMetadataProperty(key, nameof(EntityKeyMetadata.EntityKeyIndexStatus), EntityKeyIndexStatus.Failed);
        definition = WebUiCsvContract.CreateDefinition(table, new Dictionary<string, EntityMetadata> { ["new_parent"] = parent });
        Assert.Equal(["new_childid", "new_parentid"], definition.Columns.Select(column => column.CsvHeader));
    }

    [Fact]
    public void StoredReadonlyColumnsAreExportedWhileLogicalCompanionsAreExcluded()
    {
        var names = new[] { "new_sampleid", "versionnumber", "owningbusinessunit", "new_amount_base", "exchangerate", "owninguser", "new_valuename" };
        var attributes = names.Select(name => new StringAttributeMetadata { LogicalName = name }).ToArray();
        foreach (var attribute in attributes) SetAttributeFlags(attribute, true, false, false, false);
        SetMetadataProperty(attributes[5], nameof(AttributeMetadata.IsLogical), true);
        SetMetadataProperty(attributes[6], nameof(AttributeMetadata.AttributeOf), "new_value");
        var metadata = new EntityMetadata { LogicalName = "new_sample" };
        SetMetadataProperty(metadata, nameof(EntityMetadata.Attributes), attributes);
        var table = new DataverseTableInfo
        {
            LogicalName = "new_sample", SchemaName = "new_Sample", DisplayName = "Sample",
            PrimaryIdAttribute = "new_sampleid", Metadata = metadata,
        };
        var definition = WebUiCsvContract.CreateDefinition(table, new Dictionary<string, EntityMetadata>());
        Assert.Equal(["new_sampleid", "exchangerate", "new_amount_base", "owningbusinessunit", "versionnumber"], definition.Columns.Select(column => column.CsvHeader));
        Assert.Equal(["new_valuename", "owninguser"], definition.SkippedColumns.Select(column => column.LogicalName));
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
