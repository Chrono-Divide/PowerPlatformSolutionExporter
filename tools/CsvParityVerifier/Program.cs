using System.Text.Json;
using Microsoft.Xrm.Sdk.Metadata;
using PowerPlatformSolutionExporter.Services;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help"))
        {
            Console.WriteLine("Read-only live CSV export using the application's own DataverseService and TableDataExporter.");
            Console.WriteLine("--environment URL --solution UNIQUE_NAME --output DIRECTORY [--user USERNAME] [--tables TABLE1,TABLE2] [--metadata-only]");
            return 0;
        }

        var exitCode = 1;
        using var context = new ApplicationContext();
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        EventHandler? start = null;
        start = async (_, _) =>
        {
            Application.Idle -= start;
            try
            {
                await RunAsync(args);
                exitCode = 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Verification export failed: {exception.Message}");
            }
            finally
            {
                context.ExitThread();
            }
        };
        Application.Idle += start;
        Application.Run(context);
        return exitCode;
    }

    private static async Task RunAsync(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var metadataOnly = args.Contains("--metadata-only");
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == "--metadata-only") continue;
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 == args.Length)
                throw new ArgumentException($"Missing value for {args[index]}.");
            options.Add(args[index], args[++index]);
        }

        string Required(string name) => options.TryGetValue(name, out var value)
            ? value : throw new ArgumentException($"{name} is required.");
        var output = Path.GetFullPath(Required("--output"));
        Directory.CreateDirectory(output);
        using var service = new DataverseService();
        await service.ConnectAsync(Required("--environment"), options.GetValueOrDefault("--user"), CancellationToken.None);
        Console.WriteLine($"Connected to {service.ConnectedEnvironmentName} ({service.ConnectedEnvironmentUrl}).");
        var solutions = await service.LoadUnmanagedSolutionsAsync(CancellationToken.None);
        var solution = solutions.Single(item => item.UniqueName == Required("--solution"));
        var overview = await service.LoadSolutionOverviewAsync(solution, new Progress<string>(Console.WriteLine), CancellationToken.None);
        var tables = options.TryGetValue("--tables", out var selected)
            ? overview.Tables.Where(table => selected.Split(',').Contains(table.LogicalName, StringComparer.OrdinalIgnoreCase)).ToList()
            : overview.Tables.ToList();
        if (tables.Count == 0) throw new ArgumentException("No matching tables.");
        var metadata = tables.Select(table => new
        {
            table.LogicalName,
            table.Metadata.EntitySetName,
            table.PrimaryIdAttribute,
            table.PrimaryNameAttribute,
            Keys = table.Metadata.Keys?.Select(key => new { key.LogicalName, key.KeyAttributes, Status = key.EntityKeyIndexStatus.ToString() }),
            Attributes = table.Metadata.Attributes.Select(attribute => new
            {
                attribute.LogicalName,
                Type = attribute.AttributeTypeName?.Value ?? attribute.AttributeType?.ToString(),
                attribute.AttributeOf,
                attribute.IsLogical,
                attribute.IsValidForRead,
                attribute.IsValidForCreate,
                attribute.IsValidForUpdate,
                attribute.SourceType,
                Targets = (attribute as LookupAttributeMetadata)?.Targets,
                DateFormat = (attribute as DateTimeAttributeMetadata)?.Format?.ToString(),
                DateBehavior = (attribute as DateTimeAttributeMetadata)?.DateTimeBehavior?.Value,
            }),
        });
        await File.WriteAllTextAsync(Path.Combine(output, "metadata.json"), JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Saved metadata for {tables.Count} tables. Solution version: {solution.Version}.");
        if (metadataOnly) return;
        var selectedOverview = new PowerPlatformSolutionExporter.Models.SolutionOverview
        {
            Solution = overview.Solution,
            Tables = tables,
            ComponentCounts = overview.ComponentCounts,
            CanvasApps = overview.CanvasApps,
        };
        var result = await new TableDataExporter(service).ExportAsync(selectedOverview, output, new Progress<string>(Console.WriteLine), CancellationToken.None);
        Console.WriteLine($"Export: {result.OutputFolder}");
        Console.WriteLine($"Tables: {result.TableCount}; records: {result.RecordCount}.");
    }
}
