using System.Text.RegularExpressions;
using Xunit;

namespace dataverse_table_backup_restore.Tests;

public sealed class MainFormTests
{
    [Fact]
    public void MainFormUsesTheRequiredNameAndEnglishUiText()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new frmMain();
                Assert.Equal("frmMain", form.Name);
                Assert.Equal("Dataverse Snapshot Manager", form.Text);

                var allText = EnumerateControls(form)
                    .Select(control => control.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text));
                Assert.DoesNotContain(allText, text => Regex.IsMatch(text, "[\\u3040-\\u30ff\\u3400-\\u9fff]"));
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

    [Fact]
    public void MainFormCanRenderAtItsDefaultSize()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
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
                var outputDirectory = Path.Combine(Path.GetTempPath(), "DataverseSnapshotManagerTests");
                Directory.CreateDirectory(outputDirectory);
                bitmap.Save(Path.Combine(outputDirectory, "frmMain.png"));
                Assert.True(bitmap.Width >= 1100);
                Assert.True(bitmap.Height >= 700);
                form.Hide();
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
}
