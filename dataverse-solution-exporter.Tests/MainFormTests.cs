using System.Text.RegularExpressions;
using dataverse_solution_exporter;
using Xunit;

namespace dataverse_solution_exporter.Tests;

public sealed class MainFormTests
{
    [Fact]
    public void MainFormUsesRequiredNameAndSolutionExporterUi()
    {
        RunInSta(() =>
        {
            using var form = new frmMain();
            Assert.Equal("frmMain", form.Name);
            Assert.Equal("Dataverse Solution Exporter", form.Text);

            var environmentList = FindControl<ComboBox>(form, "cboEnvironmentUrl");
            var userNameList = FindControl<ComboBox>(form, "cboUserName");
            Assert.Equal(ComboBoxStyle.DropDown, environmentList.DropDownStyle);
            Assert.Equal(ComboBoxStyle.DropDown, userNameList.DropDownStyle);
            Assert.NotNull(FindControl<Button>(form, "btnSaveConnectionProfile"));
            Assert.NotNull(FindControl<Button>(form, "btnRemoveConnectionProfile"));
            Assert.NotNull(FindControl<ComboBox>(form, "cboSolutions"));
            Assert.NotNull(FindControl<Button>(form, "btnExport"));
            Assert.NotNull(FindControl<DataGridView>(form, "dgvTables"));

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
            var outputDirectory = Path.Combine(Path.GetTempPath(), "DataverseSolutionExporterTests");
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
