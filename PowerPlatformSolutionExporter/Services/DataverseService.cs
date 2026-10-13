using System.Collections.ObjectModel;
using System.Globalization;
using System.ServiceModel;
using PowerPlatformSolutionExporter.Models;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using SolutionInfo = PowerPlatformSolutionExporter.Models.SolutionInfo;

namespace PowerPlatformSolutionExporter.Services;

public sealed class DataverseService : IDisposable, ITableDataExportSource
{
    public const int EntityComponentType = 1;
    public const int OptionSetComponentType = 9;
    public const int SecurityRoleComponentType = 20;
    public const int WorkflowComponentType = 29;
    public const int WebResourceComponentType = 61;
    public const int AppModuleComponentType = 80;
    public const int PluginAssemblyComponentType = 91;
    public const int CanvasAppComponentType = 300;
    public const int ConnectorComponentType = 371;
    public const int EnvironmentVariableDefinitionComponentType = 380;
    public const int EnvironmentVariableValueComponentType = 381;

    private const string PrototypeClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
    private const string PrototypeRedirectUri = "http://localhost";
    private readonly SolutionPackageInspector _packageInspector = new();
    private ServiceClient? _client;
    private List<DataverseTableInfo> _allTables = [];

    public bool IsConnected => _client?.IsReady == true;
    public string ConnectedEnvironmentName => _client?.ConnectedOrgFriendlyName ?? string.Empty;
    public string ConnectedEnvironmentUrl => _client?.ConnectedOrgUriActual?.GetLeftPart(UriPartial.Authority) ?? string.Empty;
    string ITableDataExportSource.EnvironmentName => ConnectedEnvironmentName;
    string ITableDataExportSource.EnvironmentUrl => ConnectedEnvironmentUrl;

    public async Task ConnectAsync(string environmentUrl, string? userName, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(environmentUrl, UriKind.Absolute, out var environmentUri) || environmentUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Enter a valid HTTPS Dataverse environment URL.", nameof(environmentUrl));
        }

        var tokenCachePath = AppStorage.GetPath("msal_cache.data");
        var userPart = string.IsNullOrWhiteSpace(userName)
            ? string.Empty
            : $"Username={userName.Trim()};";
        var connectionString = string.Join(
            string.Empty,
            "AuthType=OAuth;",
            $"Url={environmentUri.GetLeftPart(UriPartial.Authority)};",
            userPart,
            $"AppId={PrototypeClientId};",
            $"RedirectUri={PrototypeRedirectUri};",
            $"TokenCacheStorePath={tokenCachePath};",
            "LoginPrompt=Auto;",
            "RequireNewInstance=true;");

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException(
                "Interactive Dataverse sign-in must be started from the WinForms UI thread (STA). No sign-in was attempted.");
        }

        ServiceClient client;
        try
        {
            client = new ServiceClient(connectionString);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw CreateConnectionException(environmentUri, client: null, exception);
        }

        if (!client.IsReady)
        {
            var connectionException = CreateConnectionException(environmentUri, client, client.LastException);
            client.Dispose();
            throw connectionException;
        }

        _client?.Dispose();
        _client = client;
        _allTables = [];
    }

    public async Task<IReadOnlyList<SolutionInfo>> LoadUnmanagedSolutionsAsync(CancellationToken cancellationToken)
    {
        EnsureConnected();
        var query = new QueryExpression("solution")
        {
            ColumnSet = new ColumnSet("solutionid", "friendlyname", "uniquename", "version", "ismanaged", "publisherid", "isvisible", "solutiontype"),
            NoLock = true,
        };
        query.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);
        query.Criteria.AddCondition("isvisible", ConditionOperator.Equal, true);
        query.Criteria.AddCondition("solutiontype", ConditionOperator.Equal, 0);
        query.Orders.Add(new OrderExpression("friendlyname", OrderType.Ascending));

        var response = await Task.Run(() => _client!.RetrieveMultiple(query), cancellationToken);
        return response.Entities
            .Select(CreateSolutionInfo)
            .Where(solution => !IsSystemSolution(solution.UniqueName))
            .OrderBy(solution => solution.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(solution => solution.UniqueName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<SolutionOverview> LoadSolutionOverviewAsync(
        SolutionInfo solution,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        bool forceMetadataRefresh = false)
    {
        EnsureConnected();
        progress?.Report($"Loading components for '{solution.UniqueName}'...");
        var components = await RetrieveSolutionComponentsAsync(solution.Id, cancellationToken);
        var componentCounts = new ReadOnlyDictionary<int, int>(components
            .GroupBy(component => component.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? -1)
            .Where(group => group.Key >= 0)
            .ToDictionary(group => group.Key, group => group.Count()));

        await EnsureTableMetadataAsync(progress, cancellationToken, forceMetadataRefresh);
        var entityMetadataIds = components
            .Where(component => component.GetAttributeValue<OptionSetValue>("componenttype")?.Value == EntityComponentType)
            .Select(component => component.GetAttributeValue<Guid>("objectid"))
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var tables = _allTables
            .Where(table => table.Metadata.MetadataId.HasValue && entityMetadataIds.Contains(table.Metadata.MetadataId.Value))
            .OrderBy(table => table.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(table => table.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var canvasComponentIds = components
            .Where(component => component.GetAttributeValue<OptionSetValue>("componenttype")?.Value == CanvasAppComponentType)
            .Select(component => component.GetAttributeValue<Guid>("objectid"))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var canvasApps = await RetrieveCanvasAppStatusesAsync(canvasComponentIds, cancellationToken);

        progress?.Report($"Loaded {tables.Count:N0} tables and {components.Count:N0} solution component records.");
        return new SolutionOverview
        {
            Solution = solution,
            Tables = tables,
            ComponentCounts = componentCounts,
            CanvasApps = canvasApps,
        };
    }

    public async Task<SolutionExportResult> PublishAndExportUnmanagedAsync(
        SolutionInfo selectedSolution,
        string outputFolder,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        EnsureConnected();
        if (selectedSolution.IsManaged)
        {
            throw new InvalidOperationException("Only unmanaged solutions can be exported by this tool.");
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            throw new ArgumentException("Choose an export folder.", nameof(outputFolder));
        }

        progress?.Report("Running a read-only Canvas App publish-state preflight...");
        var components = await RetrieveSolutionComponentsAsync(selectedSolution.Id, cancellationToken);
        var canvasComponentIds = GetCanvasComponentIds(components);
        var canvasApps = await RetrieveCanvasAppStatusesAsync(canvasComponentIds, cancellationToken);
        EnsureCanvasAppsPublished(canvasComponentIds, canvasApps, "Preflight");
        progress?.Report($"Preflight passed: {canvasApps.Count:N0} Canvas App(s) are published.");

        progress?.Report("Publishing all saved customizations in the connected environment...");
        await Task.Run(() => _client!.Execute(new PublishAllXmlRequest()), cancellationToken);
        progress?.Report("Publish All Customizations completed.");

        components = await RetrieveSolutionComponentsAsync(selectedSolution.Id, cancellationToken);
        canvasComponentIds = GetCanvasComponentIds(components);
        canvasApps = await RetrieveCanvasAppStatusesAsync(canvasComponentIds, cancellationToken);
        EnsureCanvasAppsPublished(canvasComponentIds, canvasApps, "Post-publish verification");

        progress?.Report($"Verified {canvasApps.Count:N0} Canvas App(s): no saved unpublished version was detected.");
        var currentSolution = await RetrieveSolutionAsync(selectedSolution.Id, cancellationToken);
        var nextVersion = SolutionVersion.IncrementRevision(currentSolution.Version);
        progress?.Report($"Incrementing solution version: {currentSolution.Version} -> {nextVersion}...");
        var versionUpdate = new Entity("solution", currentSolution.Id)
        {
            ["version"] = nextVersion,
        };
        await Task.Run(() => _client!.Update(versionUpdate), cancellationToken);

        Directory.CreateDirectory(outputFolder);
        var safeUniqueName = string.Concat(currentSolution.UniqueName.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var versionForFile = nextVersion.Replace('.', '_');
        var fileName = $"{safeUniqueName}_{versionForFile}_unmanaged_{DateTime.Now:yyyyMMdd_HHmmss_fff}.zip";
        var outputPath = Path.Combine(outputFolder, fileName);
        var temporaryPath = outputPath + $".{Guid.NewGuid():N}.partial";
        var outputCreated = false;

        try
        {
            progress?.Report($"Exporting unmanaged solution '{currentSolution.UniqueName}' version {nextVersion}...");
            var response = await Task.Run(
                () => (ExportSolutionResponse)_client!.Execute(new ExportSolutionRequest
                {
                    SolutionName = currentSolution.UniqueName,
                    Managed = false,
                }),
                cancellationToken);
            if (response.ExportSolutionFile is not { Length: > 0 } packageBytes)
            {
                throw new InvalidDataException("Dataverse returned an empty solution package.");
            }

            await File.WriteAllBytesAsync(temporaryPath, packageBytes, cancellationToken);
            File.Move(temporaryPath, outputPath);
            outputCreated = true;
            var inspection = _packageInspector.Inspect(outputPath);
            if (inspection.IsManaged ||
                !string.Equals(inspection.UniqueName, currentSolution.UniqueName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(inspection.Version, nextVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Export verification failed. Package reports name '{inspection.UniqueName}', version '{inspection.Version}', managed={inspection.IsManaged}.");
            }

            progress?.Report($"Verified unmanaged ZIP. SHA-256: {inspection.Sha256}");
            progress?.Report($"Export completed: {outputPath}");
            return new SolutionExportResult
            {
                FilePath = outputPath,
                PreviousVersion = currentSolution.Version,
                ExportedVersion = nextVersion,
                Sha256 = inspection.Sha256,
                TableCount = components.Count(component => component.GetAttributeValue<OptionSetValue>("componenttype")?.Value == EntityComponentType),
                ComponentCount = inspection.RootComponentCount,
            };
        }
        catch
        {
            TryDeleteFile(temporaryPath);
            if (outputCreated)
            {
                TryDeleteFile(outputPath);
            }

            throw;
        }
    }

    public IReadOnlyList<TableColumnInfo> GetTableColumns(DataverseTableInfo table)
    {
        return table.Metadata.Attributes
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.LogicalName))
            .Select(attribute => new TableColumnInfo
            {
                DisplayName = GetLabel(attribute.DisplayName, attribute.LogicalName),
                LogicalName = attribute.LogicalName,
                SchemaName = attribute.SchemaName ?? attribute.LogicalName,
                DataType = attribute.AttributeTypeName?.Value ?? attribute.AttributeType?.ToString() ?? "Unknown",
                Details = GetAttributeDetails(attribute),
                RequiredLevel = attribute.RequiredLevel?.Value.ToString() ?? "None",
                IsPrimaryId = attribute.IsPrimaryId == true,
                IsPrimaryName = attribute.IsPrimaryName == true,
                IsCustomAttribute = attribute.IsCustomAttribute == true,
                Origin = GetColumnOrigin(attribute),
                IsValidForRead = attribute.IsValidForRead == true,
                IsValidForCreate = attribute.IsValidForCreate == true,
                IsValidForUpdate = attribute.IsValidForUpdate == true,
            })
            .OrderByDescending(column => column.IsPrimaryId)
            .ThenBy(column => column.Origin)
            .ThenByDescending(column => column.IsPrimaryName)
            .ThenBy(column => column.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(column => column.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public TableDataExportDefinition GetTableDataExportDefinition(DataverseTableInfo table)
    {
        var metadata = _allTables.ToDictionary(item => item.LogicalName, item => item.Metadata, StringComparer.OrdinalIgnoreCase);
        metadata[table.LogicalName] = table.Metadata;
        return WebUiCsvContract.CreateDefinition(table, metadata);
    }

    public async Task<TableDataExportPage> RetrieveTableDataExportPageAsync(
        TableDataExportDefinition definition,
        int pageNumber,
        string? pagingCookie,
        CancellationToken cancellationToken)
    {
        EnsureConnected();
        ArgumentNullException.ThrowIfNull(definition);
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        var query = new QueryExpression(definition.Table.LogicalName)
        {
            ColumnSet = new ColumnSet(definition.Columns.Select(column => column.LogicalName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()),
            NoLock = true,
            PageInfo = new PagingInfo { Count = 5000, PageNumber = pageNumber, PagingCookie = pagingCookie },
        };
        query.Orders.Add(new OrderExpression(definition.Table.PrimaryIdAttribute, OrderType.Ascending));
        var valueAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var projections = definition.Columns.Where(column => column.LookupKeyAttribute is not null)
            .GroupBy(column => column.LogicalName, StringComparer.OrdinalIgnoreCase).ToList();
        for (var index = 0; index < projections.Count; index++)
        {
            var group = projections[index];
            var first = group.First();
            var alias = $"csvlookup{index}";
            var link = query.AddLink(first.LookupTargetEntity!, first.LogicalName, first.LookupTargetPrimaryId!, JoinOperator.LeftOuter);
            link.EntityAlias = alias;
            link.Columns = new ColumnSet(group.Select(column => column.LookupKeyAttribute!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            foreach (var column in group) valueAttributes.Add(column.CsvHeader, $"{alias}.{column.LookupKeyAttribute}");
        }

        var page = await Task.Run(() => _client!.RetrieveMultiple(query), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var rows = page.Entities.Select(entity =>
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in definition.Columns)
            {
                var valueAttribute = valueAttributes.GetValueOrDefault(column.CsvHeader, column.LogicalName);
                entity.Attributes.TryGetValue(valueAttribute, out var rawValue);
                if (rawValue is null && column.IsPrimaryId) rawValue = entity.Id;
                row[column.CsvHeader] = WebUiCsvContract.FormatValue(rawValue);
            }
            return (IReadOnlyDictionary<string, string?>)row;
        }).ToList();
        return new TableDataExportPage { Rows = rows, HasMoreRecords = page.MoreRecords, NextPagingCookie = page.PagingCookie };
    }

    public async Task<TableDataPage> RetrieveTableDataPageAsync(
        DataverseTableInfo table,
        int pageNumber,
        int pageSize,
        string? pagingCookie,
        CancellationToken cancellationToken,
        string? sortLogicalName = null,
        bool sortAscending = true)
    {
        EnsureConnected();
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        if (pageSize is < 10 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 10 and 500.");
        }

        var allColumns = GetTableColumns(table);
        var metadataByLogicalName = table.Metadata.Attributes
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.LogicalName))
            .ToDictionary(attribute => attribute.LogicalName, StringComparer.OrdinalIgnoreCase);
        var previewColumns = allColumns
            .Where(column => metadataByLogicalName.TryGetValue(column.LogicalName, out var attribute) && IsPreviewReadable(attribute))
            .ToList();
        if (previewColumns.All(column => !column.IsPrimaryId))
        {
            var primaryId = allColumns.FirstOrDefault(column => column.IsPrimaryId);
            if (primaryId is not null)
            {
                previewColumns.Insert(0, primaryId);
            }
        }

        var query = new QueryExpression(table.LogicalName)
        {
            ColumnSet = new ColumnSet(previewColumns.Select(column => column.LogicalName).ToArray()),
            NoLock = true,
            PageInfo = new PagingInfo
            {
                Count = pageSize,
                PageNumber = pageNumber,
                PagingCookie = pagingCookie,
                ReturnTotalRecordCount = false,
            },
        };
        ApplyTableDataOrdering(query, previewColumns, table.PrimaryIdAttribute, sortLogicalName, sortAscending);
        var page = await Task.Run(() => _client!.RetrieveMultiple(query), cancellationToken);
        var rows = page.Entities.Select(entity =>
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in previewColumns)
            {
                if (entity.FormattedValues.TryGetValue(column.LogicalName, out var formattedValue))
                {
                    var rawValue = entity.Attributes.TryGetValue(column.LogicalName, out var value) ? value : null;
                    row[column.LogicalName] = rawValue is OptionSetValue or Money or bool
                        ? $"{formattedValue} ({FormatSdkValue(rawValue)})"
                        : formattedValue;
                }
                else if (entity.Attributes.TryGetValue(column.LogicalName, out var rawValue))
                {
                    row[column.LogicalName] = FormatSdkValue(rawValue);
                }
                else
                {
                    row[column.LogicalName] = null;
                }
            }

            return (IReadOnlyDictionary<string, string?>)row;
        }).ToList();

        return new TableDataPage
        {
            PageNumber = pageNumber,
            Columns = previewColumns,
            Rows = rows,
            HasMoreRecords = page.MoreRecords,
            NextPagingCookie = page.PagingCookie,
        };
    }

    private static void ApplyTableDataOrdering(
        QueryExpression query,
        IReadOnlyList<TableColumnInfo> previewColumns,
        string primaryIdAttribute,
        string? sortLogicalName,
        bool sortAscending)
    {
        var sortColumn = previewColumns.FirstOrDefault(column =>
                             string.Equals(column.LogicalName, sortLogicalName, StringComparison.OrdinalIgnoreCase))
                         ?? previewColumns.FirstOrDefault(column => column.IsPrimaryId);
        if (sortColumn is not null)
        {
            query.Orders.Add(new OrderExpression(
                sortColumn.LogicalName,
                sortAscending ? OrderType.Ascending : OrderType.Descending));
        }

        if (sortColumn is null || !string.Equals(
                sortColumn.LogicalName,
                primaryIdAttribute,
                StringComparison.OrdinalIgnoreCase))
        {
            query.Orders.Add(new OrderExpression(primaryIdAttribute, OrderType.Ascending));
        }
    }

    public static string? FormatSdkValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is AliasedValue aliased)
        {
            return FormatSdkValue(aliased.Value);
        }

        return value switch
        {
            EntityReference reference => string.IsNullOrWhiteSpace(reference.Name)
                ? $"{reference.LogicalName}:{reference.Id:D}"
                : $"{reference.Name} ({reference.LogicalName}:{reference.Id:D})",
            OptionSetValue option => option.Value.ToString(CultureInfo.InvariantCulture),
            OptionSetValueCollection options => string.Join(", ", options.Select(option => option.Value.ToString(CultureInfo.InvariantCulture))),
            Money money => money.Value.ToString(CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff K", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture),
            byte[] bytes => $"[Binary: {bytes.Length:N0} bytes]",
            Guid guid => guid.ToString("D"),
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
        _allTables = [];
    }

    private async Task EnsureTableMetadataAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        bool forceRefresh)
    {
        if (!forceRefresh && _allTables.Count > 0)
        {
            return;
        }

        progress?.Report("Loading Dataverse table and column metadata...");
        var response = await Task.Run(
            () => (RetrieveAllEntitiesResponse)_client!.Execute(new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity | EntityFilters.Attributes,
                RetrieveAsIfPublished = true,
            }),
            cancellationToken);
        _allTables = response.EntityMetadata
            .Where(metadata => !string.IsNullOrWhiteSpace(metadata.LogicalName) &&
                               !string.IsNullOrWhiteSpace(metadata.PrimaryIdAttribute) &&
                               metadata.MetadataId.HasValue)
            .Select(CreateTableInfo)
            .ToList();
    }

    private async Task<List<Entity>> RetrieveSolutionComponentsAsync(Guid solutionId, CancellationToken cancellationToken)
    {
        var query = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("objectid", "componenttype", "rootsolutioncomponentid"),
            NoLock = true,
        };
        query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
        var result = await Task.Run(() => _client!.RetrieveMultiple(query), cancellationToken);
        return result.Entities.ToList();
    }

    private async Task<IReadOnlyList<CanvasAppPublishStatus>> RetrieveCanvasAppStatusesAsync(
        IReadOnlyCollection<Guid> canvasAppIds,
        CancellationToken cancellationToken)
    {
        if (canvasAppIds.Count == 0)
        {
            return [];
        }

        var apps = new List<CanvasAppPublishStatus>();
        foreach (var batch in canvasAppIds.Chunk(200))
        {
            var query = new QueryExpression("canvasapp")
            {
                ColumnSet = new ColumnSet("canvasappid", "displayname", "name", "lastmodifiedtime", "lastpublishtime"),
                NoLock = true,
            };
            query.Criteria.AddCondition("canvasappid", ConditionOperator.In, batch.Cast<object>().ToArray());
            var response = await Task.Run(() => _client!.RetrieveMultiple(query), cancellationToken);
            apps.AddRange(response.Entities.Select(entity => new CanvasAppPublishStatus
            {
                Id = entity.Id,
                DisplayName = entity.GetAttributeValue<string>("displayname")
                              ?? entity.GetAttributeValue<string>("name")
                              ?? entity.Id.ToString("D"),
                LastModifiedTime = entity.GetAttributeValue<DateTime?>("lastmodifiedtime"),
                LastPublishTime = entity.GetAttributeValue<DateTime?>("lastpublishtime"),
            }));
        }

        return apps.OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static List<Guid> GetCanvasComponentIds(IEnumerable<Entity> components)
    {
        return components
            .Where(component => component.GetAttributeValue<OptionSetValue>("componenttype")?.Value == CanvasAppComponentType)
            .Select(component => component.GetAttributeValue<Guid>("objectid"))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static void EnsureCanvasAppsPublished(
        IReadOnlyCollection<Guid> canvasComponentIds,
        IReadOnlyCollection<CanvasAppPublishStatus> canvasApps,
        string stage)
    {
        if (canvasApps.Count != canvasComponentIds.Count)
        {
            throw new InvalidOperationException(
                $"{stage} failed: the solution contains {canvasComponentIds.Count:N0} Canvas App component(s), but only {canvasApps.Count:N0} could be verified. No solution version change or export was attempted.");
        }

        var unpublishedApps = canvasApps.Where(app => !app.IsPublished).ToList();
        if (unpublishedApps.Count == 0)
        {
            return;
        }

        var details = string.Join(
            Environment.NewLine,
            unpublishedApps.Select(app =>
                $"- {app.DisplayName}: modified {FormatTimestamp(app.LastModifiedTime)}, published {FormatTimestamp(app.LastPublishTime)}"));
        throw new InvalidOperationException(
            $"{stage} failed because {unpublishedApps.Count:N0} Canvas App(s) have saved changes that are not published:{Environment.NewLine}{details}{Environment.NewLine}{Environment.NewLine}Publish these apps in Power Apps, reload the solution, and retry. No solution version change or export was attempted.");
    }

    private async Task<SolutionInfo> RetrieveSolutionAsync(Guid solutionId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _client!.Retrieve(
                "solution",
                solutionId,
                new ColumnSet("solutionid", "friendlyname", "uniquename", "version", "ismanaged", "publisherid")),
            cancellationToken);
        return CreateSolutionInfo(entity);
    }

    private static SolutionInfo CreateSolutionInfo(Entity entity)
    {
        return new SolutionInfo
        {
            Id = entity.Id,
            FriendlyName = entity.GetAttributeValue<string>("friendlyname") ?? entity.GetAttributeValue<string>("uniquename") ?? entity.Id.ToString("D"),
            UniqueName = entity.GetAttributeValue<string>("uniquename") ?? throw new InvalidDataException("A solution record is missing its unique name."),
            Version = entity.GetAttributeValue<string>("version") ?? "0.0.0.0",
            PublisherName = entity.GetAttributeValue<EntityReference>("publisherid")?.Name ?? string.Empty,
            IsManaged = entity.GetAttributeValue<bool>("ismanaged"),
        };
    }

    private static DataverseTableInfo CreateTableInfo(EntityMetadata metadata)
    {
        return new DataverseTableInfo
        {
            DisplayName = GetLabel(metadata.DisplayName, metadata.LogicalName),
            LogicalName = metadata.LogicalName,
            SchemaName = metadata.SchemaName ?? metadata.LogicalName,
            PrimaryIdAttribute = metadata.PrimaryIdAttribute,
            PrimaryNameAttribute = metadata.PrimaryNameAttribute,
            IsCustom = metadata.IsCustomEntity == true && metadata.IsManaged != true,
            Metadata = metadata,
        };
    }

    private static string GetAttributeTypeName(AttributeMetadata attribute)
    {
        return attribute.AttributeTypeName?.Value ?? attribute.AttributeType?.ToString() ?? "Unknown";
    }

    private static bool IsPreviewReadable(AttributeMetadata attribute)
    {
        return attribute.IsValidForRead == true &&
               attribute.AttributeOf is null &&
               attribute.AttributeType is not AttributeTypeCode.Virtual and
                   not AttributeTypeCode.CalendarRules and
                   not AttributeTypeCode.ManagedProperty and
                   not AttributeTypeCode.PartyList and
                   not AttributeTypeCode.EntityName &&
               !string.Equals(attribute.AttributeTypeName?.Value, "FileType", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(attribute.AttributeTypeName?.Value, "ImageType", StringComparison.OrdinalIgnoreCase);
    }

    private static DataverseColumnOrigin GetColumnOrigin(AttributeMetadata attribute)
    {
        if (attribute.IsPrimaryId == true || !string.IsNullOrWhiteSpace(attribute.AttributeOf))
        {
            return DataverseColumnOrigin.DataverseGenerated;
        }

        return attribute.IsCustomAttribute == true
            ? DataverseColumnOrigin.Custom
            : DataverseColumnOrigin.DataverseSystem;
    }

    private static string GetAttributeDetails(AttributeMetadata attribute)
    {
        return attribute switch
        {
            LookupAttributeMetadata lookup => $"Targets: {string.Join(", ", lookup.Targets ?? [])}",
            StringAttributeMetadata text => JoinDetails(text.FormatName?.Value, text.MaxLength.HasValue ? $"Max {text.MaxLength:N0}" : null, text.AutoNumberFormat),
            MemoAttributeMetadata memo => memo.MaxLength.HasValue ? $"Max {memo.MaxLength:N0}" : string.Empty,
            IntegerAttributeMetadata integer => JoinDetails(integer.Format?.ToString(), FormatRange(integer.MinValue, integer.MaxValue)),
            BigIntAttributeMetadata bigInteger => FormatRange(bigInteger.MinValue, bigInteger.MaxValue),
            DecimalAttributeMetadata decimalValue => JoinDetails(FormatRange(decimalValue.MinValue, decimalValue.MaxValue), decimalValue.Precision.HasValue ? $"Precision {decimalValue.Precision}" : null),
            DoubleAttributeMetadata doubleValue => JoinDetails(FormatRange(doubleValue.MinValue, doubleValue.MaxValue), doubleValue.Precision.HasValue ? $"Precision {doubleValue.Precision}" : null),
            MoneyAttributeMetadata money => JoinDetails(FormatRange(money.MinValue, money.MaxValue), money.Precision.HasValue ? $"Precision {money.Precision}" : null),
            DateTimeAttributeMetadata dateTime => JoinDetails(dateTime.Format?.ToString(), dateTime.DateTimeBehavior?.Value),
            EnumAttributeMetadata option => JoinDetails(option.OptionSet?.Name, option.OptionSet?.Options.Count > 0 ? $"{option.OptionSet.Options.Count:N0} options" : null),
            BooleanAttributeMetadata boolean => JoinDetails(boolean.OptionSet?.TrueOption?.Label?.UserLocalizedLabel?.Label, boolean.OptionSet?.FalseOption?.Label?.UserLocalizedLabel?.Label),
            _ => string.Empty,
        };
    }

    private static string FormatRange<T>(T? minimum, T? maximum)
        where T : struct
    {
        return minimum.HasValue || maximum.HasValue ? $"{minimum} .. {maximum}" : string.Empty;
    }

    private static string JoinDetails(params string?[] details)
    {
        return string.Join("; ", details.Where(detail => !string.IsNullOrWhiteSpace(detail)));
    }

    private static string GetLabel(Microsoft.Xrm.Sdk.Label? label, string fallback)
    {
        return label?.UserLocalizedLabel?.Label
               ?? label?.LocalizedLabels.FirstOrDefault(item => item.LanguageCode == 1033)?.Label
               ?? label?.LocalizedLabels.FirstOrDefault()?.Label
               ?? fallback;
    }

    private static bool IsSystemSolution(string uniqueName)
    {
        return uniqueName is "Default" or "Active" or "Basic" or "System";
    }

    private static string FormatTimestamp(DateTime? value)
    {
        return value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "never";
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Preserve the original export/verification failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve the original export/verification failure.
        }
    }

    private void EnsureConnected()
    {
        if (_client?.IsReady != true)
        {
            throw new InvalidOperationException("Connect to Dataverse first.");
        }
    }

    private static InvalidOperationException CreateConnectionException(
        Uri environmentUri,
        ServiceClient? client,
        Exception? exception)
    {
        var details = new List<string>
        {
            $"Unable to connect to {environmentUri.GetLeftPart(UriPartial.Authority)}.",
        };

        if (!string.IsNullOrWhiteSpace(client?.LastError))
        {
            details.Add($"Dataverse client: {client.LastError.Trim()}");
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            var detail = $"{current.GetType().Name}: {current.Message.Trim()}";
            if (!details.Contains(detail, StringComparer.Ordinal))
            {
                details.Add(detail);
            }
        }

        if (details.Count == 1)
        {
            details.Add("Dataverse authentication did not complete successfully.");
        }

        return new InvalidOperationException(string.Join(Environment.NewLine, details), exception);
    }

    private static string GetErrorMessage(Exception exception)
    {
        return exception switch
        {
            FaultException<OrganizationServiceFault> fault => fault.Detail.Message,
            _ => exception.Message,
        };
    }
}
