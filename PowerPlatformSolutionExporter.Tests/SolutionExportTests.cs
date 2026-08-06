using System.IO.Compression;
using PowerPlatformSolutionExporter.Models;
using PowerPlatformSolutionExporter.Services;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace PowerPlatformSolutionExporter.Tests;

public sealed class SolutionExportTests
{
    [Theory]
    [InlineData("0.1.0.23", "0.1.0.24")]
    [InlineData("1.2", "1.2.0.1")]
    [InlineData("10.20.30.0", "10.20.30.1")]
    public void RevisionIsIncrementedDeterministically(string current, string expected)
    {
        Assert.Equal(expected, SolutionVersion.IncrementRevision(current));
    }

    [Fact]
    public void InvalidVersionIsRejected()
    {
        Assert.Throws<FormatException>(() => SolutionVersion.IncrementRevision("not-a-version"));
    }

    [Fact]
    public void InspectorReadsAndVerifiesUnmanagedPackageIdentity()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "PowerPlatformSolutionExporterTests", Guid.NewGuid().ToString("N"));
        var packagePath = Path.Combine(testDirectory, "solution.zip");
        Directory.CreateDirectory(testDirectory);
        try
        {
            using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("solution.xml");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("<ImportExportXml><SolutionManifest><UniqueName>SampleSolution</UniqueName><Version>1.2.3.4</Version><Managed>0</Managed><RootComponents><RootComponent type=\"1\" /><RootComponent type=\"300\" /></RootComponents></SolutionManifest></ImportExportXml>");
            }

            var inspection = new SolutionPackageInspector().Inspect(packagePath);
            Assert.Equal("SampleSolution", inspection.UniqueName);
            Assert.Equal("1.2.3.4", inspection.Version);
            Assert.False(inspection.IsManaged);
            Assert.Equal(2, inspection.RootComponentCount);
            Assert.Equal(64, inspection.Sha256.Length);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void DataverseValuesAreFormattedForReadOnlyInspection()
    {
        var id = Guid.NewGuid();
        Assert.Equal("Example (account:" + id.ToString("D") + ")", DataverseService.FormatSdkValue(new EntityReference("account", id) { Name = "Example" }));
        Assert.Equal("7", DataverseService.FormatSdkValue(new OptionSetValue(7)));
        Assert.Equal("12.34", DataverseService.FormatSdkValue(new Money(12.34m)));
        Assert.Equal("[Binary: 3 bytes]", DataverseService.FormatSdkValue(new byte[] { 1, 2, 3 }));
    }
}
