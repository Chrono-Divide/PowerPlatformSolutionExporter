using PowerPlatformSolutionExporter.Models;

namespace PowerPlatformSolutionExporter.Services;

public interface ITableDataExportSource
{
    string EnvironmentName { get; }
    string EnvironmentUrl { get; }

    TableDataExportDefinition GetTableDataExportDefinition(DataverseTableInfo table);

    Task<TableDataExportPage> RetrieveTableDataExportPageAsync(
        TableDataExportDefinition definition,
        int pageNumber,
        string? pagingCookie,
        CancellationToken cancellationToken);
}
