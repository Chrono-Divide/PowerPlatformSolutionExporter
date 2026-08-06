using dataverse_solution_exporter.Models;
using dataverse_solution_exporter.Services;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Xunit;

namespace dataverse_solution_exporter.Tests;

public sealed class TableViewerTests
{
    [Fact]
    public void InspectorShowsColumnTypesAndKeepsRecordGridReadOnly()
    {
        RunInSta(() =>
        {
            var metadata = new EntityMetadata
            {
                LogicalName = "new_sample",
                SchemaName = "new_Sample",
            };
            var primaryId = new UniqueIdentifierAttributeMetadata
            {
                LogicalName = "new_sampleid",
                SchemaName = "new_SampleId",
                DisplayName = new Microsoft.Xrm.Sdk.Label("Sample", 1033),
            };
            var primaryName = new StringAttributeMetadata
            {
                LogicalName = "new_name",
                SchemaName = "new_Name",
                DisplayName = new Microsoft.Xrm.Sdk.Label("Sample name", 1033),
                MaxLength = 100,
            };
            SetMetadataProperty(metadata, nameof(EntityMetadata.PrimaryIdAttribute), "new_sampleid");
            SetMetadataProperty(metadata, nameof(EntityMetadata.PrimaryNameAttribute), "new_name");
            SetMetadataProperty(metadata, nameof(EntityMetadata.Attributes), new AttributeMetadata[] { primaryId, primaryName });
            SetMetadataProperty(primaryId, nameof(AttributeMetadata.IsPrimaryId), true);
            SetMetadataProperty(primaryId, nameof(AttributeMetadata.IsValidForRead), true);
            SetMetadataProperty(primaryName, nameof(AttributeMetadata.IsPrimaryName), true);
            SetMetadataProperty(primaryName, nameof(AttributeMetadata.IsValidForRead), true);
            SetMetadataProperty(primaryName, nameof(AttributeMetadata.IsValidForCreate), true);
            SetMetadataProperty(primaryName, nameof(AttributeMetadata.IsValidForUpdate), true);
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
            using var form = new frmTableViewer(service, table);
            var columnGrid = FindControl<DataGridView>(form, "dgvColumns");
            var dataGrid = FindControl<DataGridView>(form, "dgvData");
            var tabs = FindControl<TabControl>(form, "tabViewer");

            Assert.Equal("Sample - Table Inspector", form.Text);
            Assert.Equal(2, columnGrid.Rows.Count);
            Assert.True(columnGrid.ReadOnly);
            Assert.True(dataGrid.ReadOnly);
            Assert.Equal("Columns and data types", tabs.TabPages[0].Text);
            Assert.Equal("Record data (read-only)", tabs.TabPages[1].Text);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw failure;
        }
    }

    private static T FindControl<T>(Control parent, string name)
        where T : Control
    {
        return Assert.IsType<T>(parent.Controls.Find(name, searchAllChildren: true).Single());
    }

    private static void SetMetadataProperty(object target, string propertyName, object value)
    {
        var property = target.GetType().GetProperty(propertyName)
                       ?? throw new InvalidOperationException($"Metadata property '{propertyName}' was not found.");
        property.SetValue(target, value);
    }
}
