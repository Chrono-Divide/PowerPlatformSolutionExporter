using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using dataverse_table_backup_restore.Models;
using dataverse_table_backup_restore.Services;
using Xunit;

namespace dataverse_table_backup_restore.Tests;

public sealed class SnapshotPackageValidationTests
{
    private const string EnvironmentUrl = "https://sample.crm.dynamics.com";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ValidPackagePassesCompleteOfflineValidation()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var snapshotFile = CreatePackage(testDirectory, solutionTableName: "sample_table", tamperTableAfterHash: false);
            using var dataverse = new DataverseService();
            var service = new SnapshotPackageService(dataverse, new PacCliService("not-used"));

            var result = await service.ValidateSnapshotAsync(
                snapshotFile,
                EnvironmentUrl,
                requireStructure: true,
                requireData: true,
                CancellationToken.None);

            Assert.Single(result.Manifest.Tables);
            Assert.Equal(1, result.SolutionTableCount);
            Assert.Equal(0, result.AdditionalSolutionTableCount);
            Assert.Equal(1, result.TotalRecordCount);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task TamperedTableIsRejectedBeforeRestore()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var snapshotFile = CreatePackage(testDirectory, solutionTableName: "sample_table", tamperTableAfterHash: true);
            using var dataverse = new DataverseService();
            var service = new SnapshotPackageService(dataverse, new PacCliService("not-used"));

            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.ValidateSnapshotAsync(
                snapshotFile,
                EnvironmentUrl,
                requireStructure: true,
                requireData: true,
                CancellationToken.None));

            Assert.Contains("Checksum validation failed", exception.Message);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task SolutionMissingSelectedTableIsRejected()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var snapshotFile = CreatePackage(testDirectory, solutionTableName: "different_table", tamperTableAfterHash: false);
            using var dataverse = new DataverseService();
            var service = new SnapshotPackageService(dataverse, new PacCliService("not-used"));

            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.ValidateSnapshotAsync(
                snapshotFile,
                EnvironmentUrl,
                requireStructure: true,
                requireData: true,
                CancellationToken.None));

            Assert.Contains("does not contain structure", exception.Message);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    private static string CreatePackage(string testDirectory, string solutionTableName, bool tamperTableAfterHash)
    {
        var sourceDirectory = Path.Combine(testDirectory, "source");
        var tablesDirectory = Path.Combine(sourceDirectory, "tables");
        Directory.CreateDirectory(tablesDirectory);

        var solutionFile = Path.Combine(sourceDirectory, "solution.zip");
        using (var solutionArchive = ZipFile.Open(solutionFile, ZipArchiveMode.Create))
        {
            var entry = solutionArchive.CreateEntry("customizations.xml");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(
                $"<ImportExportXml><Entities><Entity><Name LocalizedName=\"Sample\">{solutionTableName}</Name><EntityInfo /></Entity></Entities></ImportExportXml>");
        }

        var tableSnapshot = new TableSnapshot
        {
            Schema = new TableSchemaSnapshot
            {
                DisplayName = "Sample table",
                LogicalName = "sample_table",
                SchemaName = "sample_Table",
                PrimaryIdAttribute = "sample_tableid",
                IsCustom = true,
                IsActivity = false,
                OwnershipType = "UserOwned",
                Attributes =
                [
                    new AttributeSchemaSnapshot
                    {
                        LogicalName = "sample_name",
                        SchemaName = "sample_Name",
                        DisplayName = "Name",
                        AttributeType = "String",
                        RequiredLevel = "None",
                        IsValidForRead = true,
                        IsValidForCreate = true,
                        IsValidForUpdate = true,
                        IsRestorable = true,
                    },
                ],
            },
            Records =
            [
                new RecordSnapshot
                {
                    Id = Guid.NewGuid(),
                    Attributes = new Dictionary<string, SnapshotValue>
                    {
                        ["sample_name"] = new SnapshotValue { Type = "String", Value = "Test" },
                    },
                },
            ],
        };

        var tableFile = Path.Combine(tablesDirectory, "sample_table.json");
        File.WriteAllText(tableFile, JsonSerializer.Serialize(tableSnapshot, JsonOptions));
        var tableHash = ComputeSha256(tableFile);
        if (tamperTableAfterHash)
        {
            File.AppendAllText(tableFile, " ");
        }

        var manifest = new SnapshotManifest
        {
            ApplicationVersion = "1.0.0",
            CreatedUtc = DateTimeOffset.UtcNow,
            EnvironmentUrl = EnvironmentUrl,
            Scope = "Test",
            Content = "Structure and data",
            SolutionUniqueName = "SampleSolution",
            IncludesStructure = true,
            IncludesData = true,
            SolutionSha256 = ComputeSha256(solutionFile),
            Tables =
            [
                new SnapshotTableManifest
                {
                    DisplayName = "Sample table",
                    LogicalName = "sample_table",
                    FileName = "tables/sample_table.json",
                    IsCustom = true,
                    AttributeCount = 1,
                    RecordCount = 1,
                    Sha256 = tableHash,
                },
            ],
            Warnings = [],
        };
        File.WriteAllText(Path.Combine(sourceDirectory, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions));

        var snapshotFile = Path.Combine(testDirectory, "sample.dvsnapshot");
        ZipFile.CreateFromDirectory(sourceDirectory, snapshotFile, CompressionLevel.Optimal, includeBaseDirectory: false);
        return snapshotFile;
    }

    private static string ComputeSha256(string filePath)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath)));
    }

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "DataverseSnapshotManagerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTestDirectory(string testDirectory)
    {
        var safeRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DataverseSnapshotManagerTests"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(testDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }
}
