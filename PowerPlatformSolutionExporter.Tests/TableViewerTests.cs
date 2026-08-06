using System.Reflection;
using PowerPlatformSolutionExporter.Models;
using PowerPlatformSolutionExporter.Services;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Xunit;

namespace PowerPlatformSolutionExporter.Tests;

public sealed class TableViewerTests
{
    [Fact]
    public void InspectorOrdersColumnsHighlightsBuiltInsAndKeepsRecordGridReadOnly()
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
            var customColumn = new StringAttributeMetadata
            {
                LogicalName = "new_customvalue",
                SchemaName = "new_CustomValue",
                DisplayName = new Microsoft.Xrm.Sdk.Label("Custom value", 1033),
                MaxLength = 200,
            };
            var generatedColumn = new StringAttributeMetadata
            {
                LogicalName = "new_customvaluename",
                SchemaName = "new_CustomValueName",
                DisplayName = new Microsoft.Xrm.Sdk.Label("Generated custom value name", 1033),
                MaxLength = 200,
            };
            var builtInColumn = new StringAttributeMetadata
            {
                LogicalName = "utcconversiontimezonecode",
                SchemaName = "UTCConversionTimeZoneCode",
                DisplayName = new Microsoft.Xrm.Sdk.Label(
                    "UTC conversion time zone code with a deliberately long display name",
                    1033),
                MaxLength = 100,
            };
            SetMetadataProperty(metadata, nameof(EntityMetadata.PrimaryIdAttribute), "new_sampleid");
            SetMetadataProperty(metadata, nameof(EntityMetadata.PrimaryNameAttribute), "new_name");
            SetMetadataProperty(
                metadata,
                nameof(EntityMetadata.Attributes),
                new AttributeMetadata[] { builtInColumn, generatedColumn, customColumn, primaryName, primaryId });
            SetMetadataProperty(primaryId, nameof(AttributeMetadata.IsPrimaryId), true);
            SetMetadataProperty(primaryId, nameof(AttributeMetadata.IsValidForRead), true);
            SetMetadataProperty(primaryId, nameof(AttributeMetadata.IsCustomAttribute), false);
            SetMetadataProperty(primaryName, nameof(AttributeMetadata.IsPrimaryName), true);
            SetMetadataProperty(primaryName, nameof(AttributeMetadata.IsCustomAttribute), true);
            SetMetadataProperty(primaryName, nameof(AttributeMetadata.IsValidForRead), true);
            SetMetadataProperty(primaryName, nameof(AttributeMetadata.IsValidForCreate), true);
            SetMetadataProperty(primaryName, nameof(AttributeMetadata.IsValidForUpdate), true);
            SetMetadataProperty(customColumn, nameof(AttributeMetadata.IsCustomAttribute), true);
            SetMetadataProperty(customColumn, nameof(AttributeMetadata.IsValidForRead), true);
            SetMetadataProperty(generatedColumn, nameof(AttributeMetadata.AttributeOf), "new_customvalue");
            SetMetadataProperty(generatedColumn, nameof(AttributeMetadata.IsCustomAttribute), false);
            SetMetadataProperty(generatedColumn, nameof(AttributeMetadata.IsValidForRead), true);
            SetMetadataProperty(builtInColumn, nameof(AttributeMetadata.IsCustomAttribute), false);
            SetMetadataProperty(builtInColumn, nameof(AttributeMetadata.IsValidForRead), true);
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
            var columns = service.GetTableColumns(table);
            using var form = new frmTableViewer(service, table);
            var columnGrid = FindControl<DataGridView>(form, "dgvColumns");
            var dataGrid = FindControl<DataGridView>(form, "dgvData");
            var tabs = FindControl<TabControl>(form, "tabViewer");

            Assert.Equal("Sample - Table Inspector", form.Text);
            Assert.Equal(
                ["new_sampleid", "new_name", "new_customvalue", "new_customvaluename", "utcconversiontimezonecode"],
                columns.Select(column => column.LogicalName));
            Assert.Equal(DataverseColumnOrigin.DataverseGenerated, columns[0].Origin);
            Assert.Equal(DataverseColumnOrigin.Custom, columns[1].Origin);
            Assert.Equal(DataverseColumnOrigin.Custom, columns[2].Origin);
            Assert.Equal(DataverseColumnOrigin.DataverseGenerated, columns[3].Origin);
            Assert.Equal(DataverseColumnOrigin.DataverseSystem, columns[4].Origin);
            Assert.Equal(5, columnGrid.Rows.Count);
            Assert.Equal(
                ["new_sampleid", "new_name", "new_customvalue", "new_customvaluename", "utcconversiontimezonecode"],
                columnGrid.Rows.Cast<DataGridViewRow>().Select(row => row.Cells[1].Value));
            Assert.True(columnGrid.Rows[0].DefaultCellStyle.BackColor != Color.Empty);
            Assert.True(columnGrid.Rows[2].DefaultCellStyle.BackColor.IsEmpty);
            Assert.True(columnGrid.Rows[3].DefaultCellStyle.BackColor != Color.Empty);
            Assert.True(columnGrid.Rows[4].DefaultCellStyle.BackColor != Color.Empty);
            Assert.NotEqual(
                columnGrid.Rows[3].DefaultCellStyle.BackColor,
                columnGrid.Rows[4].DefaultCellStyle.BackColor);
            Assert.Equal("Dataverse generated", columnGrid.Rows[3].Cells[3].Value);
            Assert.Equal("Dataverse system", columnGrid.Rows[4].Cells[3].Value);
            Assert.True(columnGrid.Columns["DisplayName"]!.Width > 190);
            Assert.Equal(
                columns[4].DisplayName,
                columnGrid.Rows[4].Cells[0].ToolTipText);
            Assert.True(columnGrid.ReadOnly);
            Assert.True(dataGrid.ReadOnly);
            Assert.Equal("Columns and data types", tabs.TabPages[0].Text);
            Assert.Equal("Record data (read-only)", tabs.TabPages[1].Text);

            var page = new TableDataPage
            {
                PageNumber = 1,
                Columns = columns,
                Rows =
                [
                    new Dictionary<string, string?>
                    {
                        ["new_sampleid"] = Guid.Empty.ToString(),
                        ["new_name"] = "Sample",
                        ["new_customvalue"] = "Custom",
                        ["new_customvaluename"] = "Generated",
                        ["utcconversiontimezonecode"] = "4",
                    },
                ],
                HasMoreRecords = false,
            };
            var populateData = typeof(frmTableViewer).GetMethod(
                "PopulateData",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(populateData);
            populateData.Invoke(form, [page]);

            Assert.Equal(
                DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
                dataGrid.ClipboardCopyMode);
            dataGrid.ClearSelection();
            var guidCell = dataGrid.Rows[0].Cells[dataGrid.Columns["new_sampleid"]!.Index];
            guidCell.Selected = true;

            var clipboardData = dataGrid.GetClipboardContent();
            Assert.NotNull(clipboardData);
            Assert.True(clipboardData.TryGetData<string>(DataFormats.UnicodeText, out var copiedText));
            Assert.NotNull(copiedText);
            Assert.Equal(Guid.Empty.ToString(), copiedText.TrimEnd('\r', '\n'));
            Assert.DoesNotContain("new_sampleid", copiedText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UniqueidentifierType", copiedText, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(
                ["new_sampleid", "new_name", "new_customvalue", "new_customvaluename", "utcconversiontimezonecode"],
                dataGrid.Columns.Cast<DataGridViewColumn>().Select(column => column.Name));
            Assert.Equal(DataGridViewColumnHeadersHeightSizeMode.AutoSize, dataGrid.ColumnHeadersHeightSizeMode);
            Assert.Equal(DataGridViewTriState.True, dataGrid.ColumnHeadersDefaultCellStyle.WrapMode);
            Assert.True(dataGrid.Columns["new_customvalue"]!.DefaultCellStyle.BackColor.IsEmpty);
            var generatedDataColumn = dataGrid.Columns["new_customvaluename"]!;
            var builtInDataColumn = dataGrid.Columns["utcconversiontimezonecode"]!;
            Assert.True(generatedDataColumn.DefaultCellStyle.BackColor != Color.Empty);
            Assert.True(builtInDataColumn.DefaultCellStyle.BackColor != Color.Empty);
            Assert.NotEqual(
                generatedDataColumn.DefaultCellStyle.BackColor,
                builtInDataColumn.DefaultCellStyle.BackColor);
            Assert.True(builtInDataColumn.HeaderCell.Style.BackColor != Color.Empty);
            Assert.Contains("deliberately long display name", builtInDataColumn.HeaderText);
            Assert.StartsWith(builtInDataColumn.HeaderText, builtInDataColumn.HeaderCell.ToolTipText);
            Assert.Contains("Origin: Dataverse system", builtInDataColumn.HeaderCell.ToolTipText);
            Assert.True(builtInDataColumn.Width > 190);
            Assert.All(
                dataGrid.Columns.Cast<DataGridViewColumn>(),
                column => Assert.Equal(DataGridViewColumnSortMode.Programmatic, column.SortMode));
            Assert.Equal(
                SortOrder.Ascending,
                dataGrid.Columns["new_sampleid"]!.HeaderCell.SortGlyphDirection);

            var toggleDataSort = typeof(frmTableViewer).GetMethod(
                "ToggleDataSort",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(toggleDataSort);
            toggleDataSort.Invoke(form, [columns[2]]);
            populateData.Invoke(form, [page]);
            Assert.Equal(
                SortOrder.Ascending,
                dataGrid.Columns["new_customvalue"]!.HeaderCell.SortGlyphDirection);
            toggleDataSort.Invoke(form, [columns[2]]);
            populateData.Invoke(form, [page]);
            Assert.Equal(
                SortOrder.Descending,
                dataGrid.Columns["new_customvalue"]!.HeaderCell.SortGlyphDirection);

            var applyOrdering = typeof(DataverseService).GetMethod(
                "ApplyTableDataOrdering",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(applyOrdering);
            var query = new QueryExpression(table.LogicalName);
            applyOrdering.Invoke(
                null,
                [query, columns, table.PrimaryIdAttribute, "new_customvalue", false]);
            Assert.Collection(
                query.Orders,
                order =>
                {
                    Assert.Equal("new_customvalue", order.AttributeName);
                    Assert.Equal(OrderType.Descending, order.OrderType);
                },
                order =>
                {
                    Assert.Equal("new_sampleid", order.AttributeName);
                    Assert.Equal(OrderType.Ascending, order.OrderType);
                });

            var busyField = typeof(frmTableViewer).GetField("_busy", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(busyField);
            busyField.SetValue(form, true);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-4000, -4000);
            form.Show();
            Application.DoEvents();
            var outputDirectory = Path.Combine(Path.GetTempPath(), "PowerPlatformSolutionExporterTests");
            Directory.CreateDirectory(outputDirectory);
            tabs.SelectedIndex = 0;
            Application.DoEvents();
            SaveFormScreenshot(form, Path.Combine(outputDirectory, "frmTableViewer-columns.png"));
            tabs.SelectedIndex = 1;
            Application.DoEvents();
            SaveFormScreenshot(form, Path.Combine(outputDirectory, "frmTableViewer-data.png"));
            form.Hide();

            var metadataHeaderClick = typeof(frmTableViewer).GetMethod(
                "dgvColumns_ColumnHeaderMouseClick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(metadataHeaderClick);
            var clickArguments = new DataGridViewCellMouseEventArgs(
                1,
                -1,
                0,
                0,
                new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
            metadataHeaderClick.Invoke(form, [columnGrid, clickArguments]);
            Assert.Equal(
                ["new_sampleid", "new_customvalue", "new_customvaluename", "new_name", "utcconversiontimezonecode"],
                columnGrid.Rows.Cast<DataGridViewRow>().Select(row => row.Cells[1].Value));
            Assert.Equal(SortOrder.Ascending, columnGrid.Columns[1].HeaderCell.SortGlyphDirection);
            metadataHeaderClick.Invoke(form, [columnGrid, clickArguments]);
            Assert.Equal(
                ["new_sampleid", "utcconversiontimezonecode", "new_name", "new_customvaluename", "new_customvalue"],
                columnGrid.Rows.Cast<DataGridViewRow>().Select(row => row.Cells[1].Value));
            Assert.Equal(SortOrder.Descending, columnGrid.Columns[1].HeaderCell.SortGlyphDirection);
        });
    }

    private static void SaveFormScreenshot(Form form, string outputPath)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(outputPath);
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
