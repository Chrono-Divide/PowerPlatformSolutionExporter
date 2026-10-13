using System.Text.Json;
using Microsoft.Xrm.Sdk.Metadata;
using PowerPlatformSolutionExporter.Models;
using PowerPlatformSolutionExporter.Services;
using Xunit;

namespace PowerPlatformSolutionExporter.Tests;

public sealed class TableDataExporterTests
{
    [Fact]
    public async Task ExportWritesUtf8CsvManifestGuideAndDependencyOrder()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "PowerPlatformSolutionExporterTests",
            Guid.NewGuid().ToString("N"));
        var exportRoot = Path.Combine(testDirectory, "Exports");
        var parentId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var parentId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var childId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var parentTable = CreateTable("new_parent", "Parent", "new_parentid");
        var childTable = CreateTable("new_child", "Child", "new_childid");
        var parentDefinition = new TableDataExportDefinition
        {
            Table = parentTable,
            Columns =
            [
                Column("new_parentid", "Parent", "new_parentid", "UniqueidentifierType", isPrimaryId: true),
                Column("new_name", "Name", "new_name", "StringType"),
            ],
            SkippedColumns =
            [
                new SkippedTableDataExportColumn
                {
                    DisplayName = "Owner",
                    LogicalName = "ownerid",
                    DataType = "OwnerType",
                    Reason = "Power Apps browser CSV import does not support this system field.",
                },
            ],
        };
        var childDefinition = new TableDataExportDefinition
        {
            Table = childTable,
            Columns =
            [
                Column("new_childid", "Child", "new_childid", "UniqueidentifierType", isPrimaryId: true),
                Column("new_parentid.new_code", "Parent", "new_parentid", "LookupType", lookupTargets: ["new_parent"]),
                Column("new_accountid", "External account", "new_accountid", "LookupType", lookupTargets: ["account"]),
                Column("new_notes", "Notes", "new_notes", "MemoType"),
            ],
            SkippedColumns = [],
        };
        var source = new FakeTableDataExportSource(
            [parentDefinition, childDefinition],
            new Dictionary<(string LogicalName, int PageNumber), TableDataExportPage>
            {
                [("new_parent", 1)] = Page(
                    [Row(("new_parentid", parentId1.ToString("D")), ("new_name", "Alpha, \"quoted\"\r\nsecond line"))],
                    hasMoreRecords: true,
                    nextPagingCookie: "parent-page-2"),
                [("new_parent", 2)] = Page(
                    [Row(("new_parentid", parentId2.ToString("D")), ("new_name", "Beta"))]),
                [("new_child", 1)] = Page(
                    [Row(
                        ("new_childid", childId.ToString("D")),
                        ("new_parentid.new_code", "Alpha"),
                        ("new_accountid", "External"),
                        ("new_notes", "Child note"))]),
            });
        var overview = CreateOverview([childTable, parentTable]);

        try
        {
            var result = await new TableDataExporter(source).ExportAsync(
                overview,
                exportRoot,
                progress: null,
                CancellationToken.None);

            Assert.Equal(2, result.TableCount);
            Assert.Equal(3, result.RecordCount);
            Assert.Equal(1, result.SkippedColumnCount);
            Assert.False(result.DependencyCycleDetected);
            Assert.Equal(["new_parent", "new_child"], source.RetrievalOrder.Distinct(StringComparer.OrdinalIgnoreCase));
            Assert.True(Directory.Exists(result.OutputFolder));
            Assert.Empty(Directory.GetDirectories(exportRoot, "*.partial_*"));

            var parentCsvPath = Path.Combine(result.OutputFolder, "new_parents.csv");
            var parentBytes = File.ReadAllBytes(parentCsvPath);
            Assert.True(parentBytes.Length >= 3);
            Assert.Equal([0xEF, 0xBB, 0xBF], parentBytes.Take(3));
            var parentCsv = File.ReadAllText(parentCsvPath);
            Assert.StartsWith("\"new_parentid\",\"new_name\"", parentCsv, StringComparison.Ordinal);
            Assert.Contains("\"Alpha, \"\"quoted\"\"\r\nsecond line\"", parentCsv, StringComparison.Ordinal);

            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputFolder, "manifest.json")));
            var root = manifest.RootElement;
            Assert.Equal(2, root.GetProperty("formatVersion").GetInt32());
            Assert.Equal(3, root.GetProperty("recordCount").GetInt64());
            var tables = root.GetProperty("tables").EnumerateArray().ToList();
            Assert.Equal("new_parent", tables[0].GetProperty("logicalName").GetString());
            Assert.Equal("new_child", tables[1].GetProperty("logicalName").GetString());
            Assert.Equal("new_parent", tables[1].GetProperty("dependencies")[0].GetString());
            Assert.Equal("account", tables[1].GetProperty("externalLookupTargets")[0].GetString());
            Assert.Equal("ownerid", tables[0].GetProperty("skippedColumns")[0].GetProperty("logicalName").GetString());

            var guide = File.ReadAllText(Path.Combine(result.OutputFolder, "import-guide.txt"));
            Assert.Contains("BROWSER IMPORT STEPS", guide, StringComparison.Ordinal);
            Assert.Contains("new_parents.csv", guide, StringComparison.Ordinal);
            Assert.Contains("new_childs.csv", guide, StringComparison.Ordinal);
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
    public async Task FailedExportRemovesItsPartialDirectory()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "PowerPlatformSolutionExporterTests",
            Guid.NewGuid().ToString("N"));
        var table = CreateTable("new_sample", "Sample", "new_sampleid");
        var definition = new TableDataExportDefinition
        {
            Table = table,
            Columns = [Column("Sample", "Sample", "new_sampleid", "UniqueidentifierType", isPrimaryId: true)],
            SkippedColumns = [],
        };
        var source = new FakeTableDataExportSource(
            [definition],
            new Dictionary<(string LogicalName, int PageNumber), TableDataExportPage>())
        {
            FailOnRetrieve = true,
        };

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new TableDataExporter(source).ExportAsync(
                CreateOverview([table]),
                testDirectory,
                progress: null,
                CancellationToken.None));
            Assert.Empty(Directory.GetDirectories(testDirectory));
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
    public async Task LookupCycleIsReportedWithoutDroppingTables()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "PowerPlatformSolutionExporterTests",
            Guid.NewGuid().ToString("N"));
        var tableA = CreateTable("new_a", "A", "new_aid");
        var tableB = CreateTable("new_b", "B", "new_bid");
        var definitionA = new TableDataExportDefinition
        {
            Table = tableA,
            Columns =
            [
                Column("A", "A", "new_aid", "UniqueidentifierType", isPrimaryId: true),
                Column("B", "B", "new_bid", "LookupType", lookupTargets: ["new_b"]),
            ],
            SkippedColumns = [],
        };
        var definitionB = new TableDataExportDefinition
        {
            Table = tableB,
            Columns =
            [
                Column("B", "B", "new_bid", "UniqueidentifierType", isPrimaryId: true),
                Column("A", "A", "new_aid", "LookupType", lookupTargets: ["new_a"]),
            ],
            SkippedColumns = [],
        };
        var source = new FakeTableDataExportSource(
            [definitionB, definitionA],
            new Dictionary<(string LogicalName, int PageNumber), TableDataExportPage>
            {
                [("new_a", 1)] = Page([]),
                [("new_b", 1)] = Page([]),
            });

        try
        {
            var result = await new TableDataExporter(source).ExportAsync(
                CreateOverview([tableB, tableA]),
                testDirectory,
                progress: null,
                CancellationToken.None);

            Assert.True(result.DependencyCycleDetected);
            Assert.Equal(2, result.TableCount);
            Assert.True(File.Exists(Path.Combine(result.OutputFolder, "new_as.csv")));
            Assert.True(File.Exists(Path.Combine(result.OutputFolder, "new_bs.csv")));
            Assert.Contains(
                "lookup dependency cycle was detected",
                File.ReadAllText(Path.Combine(result.OutputFolder, "import-guide.txt")),
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    private static TableDataExportColumn Column(
        string csvHeader,
        string displayName,
        string logicalName,
        string dataType,
        bool isPrimaryId = false,
        IReadOnlyList<string>? lookupTargets = null)
    {
        return new TableDataExportColumn
        {
            CsvHeader = csvHeader,
            DisplayName = displayName,
            LogicalName = logicalName,
            DataType = dataType,
            RequiredLevel = "None",
            IsPrimaryId = isPrimaryId,
            LookupTargets = lookupTargets ?? [],
        };
    }

    private static TableDataExportPage Page(
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        bool hasMoreRecords = false,
        string? nextPagingCookie = null)
    {
        return new TableDataExportPage
        {
            Rows = rows,
            HasMoreRecords = hasMoreRecords,
            NextPagingCookie = nextPagingCookie,
        };
    }

    private static IReadOnlyDictionary<string, string?> Row(params (string Name, string? Value)[] values)
    {
        return values.ToDictionary(value => value.Name, value => value.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static DataverseTableInfo CreateTable(string logicalName, string displayName, string primaryId)
    {
        return new DataverseTableInfo
        {
            DisplayName = displayName,
            LogicalName = logicalName,
            SchemaName = logicalName,
            PrimaryIdAttribute = primaryId,
            IsCustom = true,
            Metadata = new EntityMetadata { LogicalName = logicalName, SchemaName = logicalName, EntitySetName = logicalName + "s" },
        };
    }

    private static SolutionOverview CreateOverview(IReadOnlyList<DataverseTableInfo> tables)
    {
        return new SolutionOverview
        {
            Solution = new SolutionInfo
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                FriendlyName = "Sample solution",
                UniqueName = "SampleSolution",
                Version = "1.0.0.0",
                PublisherName = "Sample publisher",
                IsManaged = false,
            },
            Tables = tables,
            ComponentCounts = new Dictionary<int, int>(),
            CanvasApps = [],
        };
    }

    private sealed class FakeTableDataExportSource : ITableDataExportSource
    {
        private readonly IReadOnlyDictionary<string, TableDataExportDefinition> _definitions;
        private readonly IReadOnlyDictionary<(string LogicalName, int PageNumber), TableDataExportPage> _pages;

        public FakeTableDataExportSource(
            IReadOnlyList<TableDataExportDefinition> definitions,
            IReadOnlyDictionary<(string LogicalName, int PageNumber), TableDataExportPage> pages)
        {
            _definitions = definitions.ToDictionary(
                definition => definition.Table.LogicalName,
                StringComparer.OrdinalIgnoreCase);
            _pages = pages;
        }

        public string EnvironmentName => "Test environment";
        public string EnvironmentUrl => "https://example.crm.dynamics.com";
        public List<string> RetrievalOrder { get; } = [];
        public bool FailOnRetrieve { get; init; }

        public TableDataExportDefinition GetTableDataExportDefinition(DataverseTableInfo table) =>
            _definitions[table.LogicalName];

        public Task<TableDataExportPage> RetrieveTableDataExportPageAsync(
            TableDataExportDefinition definition,
            int pageNumber,
            string? pagingCookie,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RetrievalOrder.Add(definition.Table.LogicalName);
            if (FailOnRetrieve)
            {
                throw new InvalidOperationException("Simulated retrieval failure.");
            }

            if (definition.Table.LogicalName == "new_parent" && pageNumber == 2)
            {
                Assert.Equal("parent-page-2", pagingCookie);
            }

            return Task.FromResult(_pages[(definition.Table.LogicalName, pageNumber)]);
        }
    }
}
