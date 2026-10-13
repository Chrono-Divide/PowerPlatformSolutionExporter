using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk.Metadata;
using PowerPlatformSolutionExporter;
using PowerPlatformSolutionExporter.Models;
using PowerPlatformSolutionExporter.Services;
using Xunit;

namespace PowerPlatformSolutionExporter.Tests;

public sealed class MainFormTests
{
    [Fact]
    public void MainFormUsesRequiredNameAndSolutionExporterUi()
    {
        RunInSta(() =>
        {
            using var form = new frmMain();
            Assert.Equal("frmMain", form.Name);
            Assert.Equal(new Version(0, 0, 6, 0), typeof(frmMain).Assembly.GetName().Version);
            Assert.Equal("Power Platform Solution Exporter v0.0.6", form.Text);

            var environmentList = FindControl<ComboBox>(form, "cboEnvironmentUrl");
            var userNameList = FindControl<ComboBox>(form, "cboUserName");
            Assert.Equal(ComboBoxStyle.DropDown, environmentList.DropDownStyle);
            Assert.Equal(ComboBoxStyle.DropDown, userNameList.DropDownStyle);
            Assert.NotNull(FindControl<Button>(form, "btnSaveConnectionProfile"));
            Assert.NotNull(FindControl<Button>(form, "btnRemoveConnectionProfile"));
            Assert.NotNull(FindControl<ComboBox>(form, "cboSolutions"));
            var solutionExportButton = FindControl<Button>(form, "btnExport");
            Assert.True(solutionExportButton.Height >= 32);
            Assert.Equal("Export Table Data", FindControl<Button>(form, "btnExportTableData").Text);
            var selectAllData = FindControl<CheckBox>(form, "chkSelectAllTableData");
            Assert.Equal("Select all", selectAllData.Text);
            Assert.True(selectAllData.ThreeState);
            Assert.False(selectAllData.AutoCheck);
            var tableGrid = FindControl<DataGridView>(form, "dgvTables");
            Assert.False(tableGrid.ReadOnly);
            var exportDataColumn = Assert.IsType<DataGridViewCheckBoxColumn>(tableGrid.Columns["colExportData"]);
            Assert.Equal("IncludeInDataExport", exportDataColumn.DataPropertyName);
            Assert.False(exportDataColumn.ReadOnly);
            Assert.All(
                tableGrid.Columns.Cast<DataGridViewColumn>().Where(column => column != exportDataColumn),
                column => Assert.True(column.ReadOnly));

            var publishNotice = FindControl<Label>(form, "lblPublishNotice");
            Assert.Contains("Table checkboxes affect only Export Table Data", publishNotice.Text, StringComparison.Ordinal);
            Assert.Contains("full selected Solution", publishNotice.Text, StringComparison.Ordinal);
            Assert.Contains("segmented", publishNotice.Text, StringComparison.OrdinalIgnoreCase);

            var allText = EnumerateControls(form)
                .Select(control => control.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
            Assert.DoesNotContain(allText, text => text.Contains("Restore Snapshot", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(allText, text => text.Contains("Danger Zone", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(allText, text => Regex.IsMatch(text, "[\\u3040-\\u30ff\\u3400-\\u9fff]"));
        });
    }

    [Fact]
    public void MainFormDefaultsAndRemembersTheExportFolder()
    {
        RunInSta(() =>
        {
            var testDirectory = Path.Combine(
                Path.GetTempPath(),
                "PowerPlatformSolutionExporterTests",
                Guid.NewGuid().ToString("N"));
            var settingsFile = Path.Combine(testDirectory, "app-settings.json");
            var customExportFolder = Path.Combine(testDirectory, "RememberedExports");

            try
            {
                using (var form = new frmMain(new AppSettingsStore(settingsFile))
                       {
                           StartPosition = FormStartPosition.Manual,
                           Location = new Point(-4000, -4000),
                       })
                {
                    var exportFolder = FindControl<TextBox>(form, "txtExportFolder");
                    Assert.Equal(
                        Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "PowerPlatformSolutionExports"),
                        exportFolder.Text);

                    form.Show();
                    Application.DoEvents();
                    exportFolder.Text = customExportFolder;
                    form.Close();
                }

                using var reopenedForm = new frmMain(new AppSettingsStore(settingsFile));
                Assert.Equal(
                    customExportFolder,
                    FindControl<TextBox>(reopenedForm, "txtExportFolder").Text);
            }
            finally
            {
                if (Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, recursive: true);
                }
            }
        });
    }

    [Fact]
    public void SelectAllCheckboxTogglesEveryTableDataRow()
    {
        RunInSta(() =>
        {
            using var form = new frmMain();
            var rowType = typeof(frmMain).GetNestedType("TableGridRow", BindingFlags.NonPublic);
            var rowsField = typeof(frmMain).GetField("_tableRows", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(rowType);
            Assert.NotNull(rowsField);
            var rows = Assert.IsAssignableFrom<System.Collections.IList>(rowsField.GetValue(form));
            var selectedProperty = rowType.GetProperty("IncludeInDataExport");
            var updateTableSummary = typeof(frmMain).GetMethod(
                "UpdateTableSummary",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var toggleSelection = typeof(frmMain).GetMethod(
                "chkSelectAllTableData_Click",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(selectedProperty);
            Assert.NotNull(updateTableSummary);
            Assert.NotNull(toggleSelection);
            var selectAll = FindControl<CheckBox>(form, "chkSelectAllTableData");

            foreach (var logicalName in new[] { "sample_first", "sample_second" })
            {
                var table = new DataverseTableInfo
                {
                    DisplayName = logicalName,
                    LogicalName = logicalName,
                    SchemaName = logicalName,
                    PrimaryIdAttribute = $"{logicalName}id",
                    IsCustom = true,
                    Metadata = new EntityMetadata
                    {
                        LogicalName = logicalName,
                        SchemaName = logicalName,
                    },
                };
                var row = Activator.CreateInstance(
                    rowType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    args: [table],
                    culture: null);
                Assert.NotNull(row);
                rows.Add(row);
            }

            updateTableSummary.Invoke(form, null);
            Assert.Equal(CheckState.Checked, selectAll.CheckState);
            Assert.All(rows.Cast<object>(), row => Assert.True((bool)selectedProperty.GetValue(row)!));

            toggleSelection.Invoke(form, [selectAll, EventArgs.Empty]);
            Assert.Equal(CheckState.Unchecked, selectAll.CheckState);
            Assert.All(rows.Cast<object>(), row => Assert.False((bool)selectedProperty.GetValue(row)!));

            toggleSelection.Invoke(form, [selectAll, EventArgs.Empty]);
            Assert.Equal(CheckState.Checked, selectAll.CheckState);
            Assert.All(rows.Cast<object>(), row => Assert.True((bool)selectedProperty.GetValue(row)!));
        });
    }

    [Fact]
    public void BusyOperationFocusesCancelInsteadOfTheResizableSplitter()
    {
        RunInSta(() =>
        {
            using var form = new frmMain
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-4000, -4000),
            };
            form.Show();
            Application.DoEvents();

            var connectButton = FindControl<Button>(form, "btnConnect");
            var cancelButton = FindControl<Button>(form, "btnCancelOperation");
            var splitter = FindControl<SplitContainer>(form, "splitMain");
            Assert.False(splitter.TabStop);
            Assert.True(connectButton.Focus());

            var busyField = typeof(frmMain).GetField("_busy", BindingFlags.Instance | BindingFlags.NonPublic);
            var updateActionState = typeof(frmMain).GetMethod(
                "UpdateActionState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(busyField);
            Assert.NotNull(updateActionState);
            busyField.SetValue(form, true);
            updateActionState.Invoke(form, null);
            Application.DoEvents();

            Assert.True(cancelButton.Focused);
            Assert.False(splitter.Focused);
            form.Hide();
        });
    }

    [Fact]
    public void MainFormCanRenderAtItsDefaultSize()
    {
        RunInSta(() =>
        {
            using var form = new frmMain
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-4000, -4000),
            };
            form.Show();
            Application.DoEvents();

            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            var outputDirectory = Path.Combine(Path.GetTempPath(), "PowerPlatformSolutionExporterTests");
            Directory.CreateDirectory(outputDirectory);
            bitmap.Save(Path.Combine(outputDirectory, "frmMain.png"));
            Assert.True(bitmap.Width >= 1100);
            Assert.True(bitmap.Height >= 700);
            form.Hide();
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

    private static IEnumerable<Control> EnumerateControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateControls(child))
            {
                yield return descendant;
            }
        }
    }

    private static T FindControl<T>(Control parent, string name)
        where T : Control
    {
        return Assert.IsType<T>(parent.Controls.Find(name, searchAllChildren: true).Single());
    }
}
