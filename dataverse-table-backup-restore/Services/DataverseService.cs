using System.ServiceModel;
using dataverse_table_backup_restore.Models;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace dataverse_table_backup_restore.Services;

public enum TableScope
{
    Custom,
    All,
}

public sealed class DataverseService : IDisposable
{
    private const string PrototypeClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
    private const string PrototypeRedirectUri = "app://58145B91-0C36-4500-8554-080854F2AC97";
    private ServiceClient? _client;
    private List<DataverseTableInfo> _allTables = [];

    public bool IsConnected => _client?.IsReady == true;
    public string ConnectedEnvironmentName => _client?.ConnectedOrgFriendlyName ?? string.Empty;
    public string ConnectedEnvironmentUrl => _client?.ConnectedOrgUriActual?.GetLeftPart(UriPartial.Authority) ?? string.Empty;

    public async Task ConnectAsync(string environmentUrl, string? userName, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(environmentUrl, UriKind.Absolute, out var environmentUri) || environmentUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Enter a valid HTTPS Dataverse environment URL.", nameof(environmentUrl));
        }

        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataverseSnapshotManager");
        Directory.CreateDirectory(cacheDirectory);
        var tokenCachePath = Path.Combine(cacheDirectory, "msal_cache.data");

        var userPart = string.IsNullOrWhiteSpace(userName)
            ? string.Empty
            : $"Username={userName.Trim()};";
        var connectionString = string.Join(
            string.Empty,
            "AuthType=OAuth;",
            $"Url={environmentUri.GetLeftPart(UriPartial.Authority)};",
            userPart,
            "Integrated Security=true;",
            $"AppId={PrototypeClientId};",
            $"RedirectUri={PrototypeRedirectUri};",
            $"TokenCacheStorePath={tokenCachePath};",
            "LoginPrompt=Auto;",
            "RequireNewInstance=true;");

        var client = await Task.Run(() => new ServiceClient(connectionString), cancellationToken);
        if (!client.IsReady)
        {
            var message = string.IsNullOrWhiteSpace(client.LastError)
                ? "Dataverse authentication did not complete successfully."
                : client.LastError;
            client.Dispose();
            throw new InvalidOperationException(message, client.LastException);
        }

        _client?.Dispose();
        _client = client;
        _allTables = [];
    }

    public async Task<IReadOnlyList<DataverseTableInfo>> LoadTablesAsync(
        TableScope scope,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        bool forceRefresh = false)
    {
        EnsureConnected();
        if (forceRefresh || _allTables.Count == 0)
        {
            progress?.Report("Loading Dataverse table metadata...");
            var response = await Task.Run(
                () => (RetrieveAllEntitiesResponse)_client!.Execute(new RetrieveAllEntitiesRequest
                {
                    EntityFilters = EntityFilters.Entity | EntityFilters.Attributes,
                    RetrieveAsIfPublished = true,
                }),
                cancellationToken);

            _allTables = response.EntityMetadata
                .Where(IsSelectableTable)
                .Select(CreateTableInfo)
                .OrderBy(table => table.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(table => table.LogicalName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var tables = scope == TableScope.Custom
            ? _allTables.Where(table => table.IsCustom).ToList()
            : _allTables.ToList();
        progress?.Report($"Loaded {tables.Count:N0} {scope.ToString().ToLowerInvariant()} tables.");
        return tables;
    }

    public async Task<TableSnapshot> ExportTableAsync(
        DataverseTableInfo table,
        bool includeData,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        EnsureConnected();
        var schema = CreateSchemaSnapshot(table);
        var records = includeData
            ? await RetrieveRecordsAsync(table, schema, progress, cancellationToken)
            : [];

        return new TableSnapshot
        {
            Schema = schema,
            Records = records,
        };
    }

    public async Task<OperationSummary> RestoreTableAsync(
        TableSnapshot snapshot,
        bool referencePass,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        EnsureConnected();
        var targetTable = _allTables.FirstOrDefault(
            table => string.Equals(table.LogicalName, snapshot.Schema.LogicalName, StringComparison.OrdinalIgnoreCase));
        if (targetTable is null)
        {
            throw new InvalidOperationException($"Table '{snapshot.Schema.LogicalName}' does not exist after the structure restore.");
        }

        if (!targetTable.IsCustom)
        {
            throw new InvalidOperationException($"Restore is limited to custom tables. '{targetTable.LogicalName}' is not an unmanaged custom table.");
        }

        var summary = new OperationSummary();
        var currentIds = await RetrieveRecordIdsAsync(targetTable, cancellationToken);
        var targetAttributes = targetTable.Metadata.Attributes
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.LogicalName))
            .ToDictionary(attribute => attribute.LogicalName, StringComparer.OrdinalIgnoreCase);

        var processed = 0;
        foreach (var record in snapshot.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exists = currentIds.Contains(record.Id);
            if (referencePass && !exists)
            {
                summary.Failed++;
                progress?.Report($"{targetTable.LogicalName} {record.Id:D}: reference pass skipped because record creation failed in pass 1.");
                processed++;
                continue;
            }

            var entity = new Entity(targetTable.LogicalName, record.Id);

            foreach (var attributeValue in record.Attributes)
            {
                if (!targetAttributes.TryGetValue(attributeValue.Key, out var metadata) ||
                    metadata.IsPrimaryId == true ||
                    metadata.AttributeOf is not null ||
                    metadata.IsSecured == true ||
                    metadata.AttributeType == AttributeTypeCode.Virtual)
                {
                    continue;
                }

                var validForOperation = exists
                    ? metadata.IsValidForUpdate == true
                    : metadata.IsValidForCreate == true;
                if (!validForOperation)
                {
                    continue;
                }

                var isReferenceOrState = attributeValue.Value.IsReference ||
                                         string.Equals(attributeValue.Key, "statecode", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(attributeValue.Key, "statuscode", StringComparison.OrdinalIgnoreCase);
                var requiredReferenceForCreate = !exists &&
                                                 attributeValue.Value.IsReference &&
                                                 metadata.RequiredLevel?.Value is AttributeRequiredLevel.ApplicationRequired or AttributeRequiredLevel.SystemRequired;

                if (referencePass != isReferenceOrState && !(requiredReferenceForCreate && !referencePass))
                {
                    continue;
                }

                if (attributeValue.Value.IsUnsupported)
                {
                    summary.Skipped++;
                    continue;
                }

                var sdkValue = attributeValue.Value.ToSdkValue();
                if (!exists && sdkValue is null)
                {
                    continue;
                }

                entity[attributeValue.Key] = sdkValue;
            }

            if (entity.Attributes.Count == 0)
            {
                summary.Skipped++;
                processed++;
                continue;
            }

            try
            {
                await Task.Run(
                    () =>
                    {
                        if (exists)
                        {
                            _client!.Update(entity);
                        }
                        else if (!referencePass)
                        {
                            _client!.Create(entity);
                        }
                    },
                    cancellationToken);

                if (!referencePass)
                {
                    if (exists)
                    {
                        summary.Updated++;
                    }
                    else
                    {
                        summary.Created++;
                        currentIds.Add(record.Id);
                    }
                }
                else
                {
                    summary.Updated++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                summary.Failed++;
                progress?.Report($"{targetTable.LogicalName} {record.Id:D}: {GetErrorMessage(exception)}");
            }

            processed++;
            if (processed % 100 == 0 || processed == snapshot.Records.Count)
            {
                progress?.Report($"{targetTable.LogicalName}: {processed:N0}/{snapshot.Records.Count:N0} records processed ({(referencePass ? "references" : "values")}).");
            }
        }

        return summary;
    }

    public async Task<int> DeleteRecordsNotInSnapshotAsync(
        TableSnapshot snapshot,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        EnsureConnected();
        var targetTable = _allTables.FirstOrDefault(
            table => string.Equals(table.LogicalName, snapshot.Schema.LogicalName, StringComparison.OrdinalIgnoreCase));
        if (targetTable is null || !targetTable.IsCustom)
        {
            throw new InvalidOperationException("Strict alignment deletion is limited to existing unmanaged custom tables.");
        }

        var snapshotIds = snapshot.Records.Select(record => record.Id).ToHashSet();
        var currentIds = await RetrieveRecordIdsAsync(targetTable, cancellationToken);
        var extraIds = currentIds.Where(id => !snapshotIds.Contains(id)).ToList();
        if (extraIds.Count == 0)
        {
            return 0;
        }

        progress?.Report($"Deleting {extraIds.Count:N0} records that are not present in the snapshot from {targetTable.LogicalName}...");
        return await DeleteRecordsAsync(targetTable, extraIds, progress, cancellationToken);
    }

    public async Task<int> ClearTableDataAsync(
        DataverseTableInfo table,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        EnsureConnected();
        if (!table.IsCustom)
        {
            throw new InvalidOperationException("Clear data is limited to unmanaged custom tables.");
        }

        var ids = await RetrieveRecordIdsAsync(table, cancellationToken);
        progress?.Report($"Deleting {ids.Count:N0} records from {table.LogicalName}. The table structure will not be changed.");
        return await DeleteRecordsAsync(table, ids, progress, cancellationToken);
    }

    public async Task RefreshMetadataAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        _allTables = [];
        await LoadTablesAsync(TableScope.All, progress, cancellationToken, forceRefresh: true);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
        _allTables = [];
    }

    private async Task<List<RecordSnapshot>> RetrieveRecordsAsync(
        DataverseTableInfo table,
        TableSchemaSnapshot schema,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var readableAttributes = schema.Attributes
            .Where(attribute => attribute.IsRestorable)
            .Select(attribute => attribute.LogicalName)
            .Append(table.PrimaryIdAttribute)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var query = new QueryExpression(table.LogicalName)
        {
            ColumnSet = new ColumnSet(readableAttributes),
            NoLock = true,
            PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = 1,
                ReturnTotalRecordCount = false,
            },
        };

        var records = new List<RecordSnapshot>();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await Task.Run(() => _client!.RetrieveMultiple(query), cancellationToken);
            foreach (var entity in page.Entities)
            {
                var values = new Dictionary<string, SnapshotValue>(StringComparer.OrdinalIgnoreCase);
                foreach (var attribute in schema.Attributes.Where(attribute => attribute.IsValidForRead && attribute.IsRestorable))
                {
                    entity.Attributes.TryGetValue(attribute.LogicalName, out var value);
                    values[attribute.LogicalName] = SnapshotValue.FromSdkValue(value);
                }

                records.Add(new RecordSnapshot
                {
                    Id = entity.Id,
                    Attributes = values,
                });
            }

            progress?.Report($"{table.LogicalName}: exported {records.Count:N0} records.");
            if (!page.MoreRecords)
            {
                break;
            }

            query.PageInfo.PageNumber++;
            query.PageInfo.PagingCookie = page.PagingCookie;
        }
        while (true);

        return records;
    }

    private async Task<HashSet<Guid>> RetrieveRecordIdsAsync(DataverseTableInfo table, CancellationToken cancellationToken)
    {
        var query = new QueryExpression(table.LogicalName)
        {
            ColumnSet = new ColumnSet(table.PrimaryIdAttribute),
            NoLock = true,
            PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = 1,
                ReturnTotalRecordCount = false,
            },
        };

        var ids = new HashSet<Guid>();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await Task.Run(() => _client!.RetrieveMultiple(query), cancellationToken);
            foreach (var entity in page.Entities)
            {
                ids.Add(entity.Id);
            }

            if (!page.MoreRecords)
            {
                break;
            }

            query.PageInfo.PageNumber++;
            query.PageInfo.PagingCookie = page.PagingCookie;
        }
        while (true);

        return ids;
    }

    private async Task<int> DeleteRecordsAsync(
        DataverseTableInfo table,
        IReadOnlyCollection<Guid> ids,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var deleted = 0;
        foreach (var batch in ids.Chunk(200))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requests = new OrganizationRequestCollection();
            foreach (var id in batch)
            {
                requests.Add(new DeleteRequest { Target = new EntityReference(table.LogicalName, id) });
            }

            var response = await Task.Run(
                () => (ExecuteMultipleResponse)_client!.Execute(new ExecuteMultipleRequest
                {
                    Requests = requests,
                    Settings = new ExecuteMultipleSettings
                    {
                        ContinueOnError = true,
                        ReturnResponses = true,
                    },
                }),
                cancellationToken);

            var failures = response.Responses.Count(item => item.Fault is not null);
            deleted += batch.Length - failures;
            foreach (var failure in response.Responses.Where(item => item.Fault is not null))
            {
                progress?.Report($"Delete failed in {table.LogicalName}: {failure.Fault.Message}");
            }

            progress?.Report($"{table.LogicalName}: deleted {deleted:N0}/{ids.Count:N0} records.");
        }

        return deleted;
    }

    private static bool IsSelectableTable(EntityMetadata metadata)
    {
        return !string.IsNullOrWhiteSpace(metadata.LogicalName) &&
               !string.IsNullOrWhiteSpace(metadata.PrimaryIdAttribute) &&
               metadata.IsIntersect != true &&
               metadata.IsActivityParty != true &&
               metadata.ObjectTypeCode.HasValue;
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
            EntitySetName = metadata.EntitySetName,
            IsCustom = metadata.IsCustomEntity == true && metadata.IsManaged != true,
            IsActivity = metadata.IsActivity == true,
            Metadata = metadata,
        };
    }

    private static TableSchemaSnapshot CreateSchemaSnapshot(DataverseTableInfo table)
    {
        return new TableSchemaSnapshot
        {
            DisplayName = table.DisplayName,
            LogicalName = table.LogicalName,
            SchemaName = table.SchemaName,
            PrimaryIdAttribute = table.PrimaryIdAttribute,
            PrimaryNameAttribute = table.PrimaryNameAttribute,
            EntitySetName = table.EntitySetName,
            IsCustom = table.IsCustom,
            IsActivity = table.IsActivity,
            OwnershipType = table.Metadata.OwnershipType?.ToString() ?? string.Empty,
            Attributes = table.Metadata.Attributes
                .Where(attribute => !string.IsNullOrWhiteSpace(attribute.LogicalName))
                .Select(attribute => new AttributeSchemaSnapshot
                {
                    LogicalName = attribute.LogicalName,
                    SchemaName = attribute.SchemaName ?? attribute.LogicalName,
                    DisplayName = GetLabel(attribute.DisplayName, attribute.LogicalName),
                    AttributeType = attribute.AttributeTypeName?.Value ?? attribute.AttributeType?.ToString() ?? "Unknown",
                    RequiredLevel = attribute.RequiredLevel?.Value.ToString() ?? AttributeRequiredLevel.None.ToString(),
                    IsPrimaryId = attribute.IsPrimaryId == true,
                    IsPrimaryName = attribute.IsPrimaryName == true,
                    IsValidForRead = attribute.IsValidForRead == true,
                    IsValidForCreate = attribute.IsValidForCreate == true,
                    IsValidForUpdate = attribute.IsValidForUpdate == true,
                    IsSecured = attribute.IsSecured == true,
                    IsRestorable = attribute.IsSecured != true &&
                                   attribute.AttributeOf is null &&
                                   attribute.AttributeType != AttributeTypeCode.Virtual &&
                                   attribute.IsPrimaryId != true &&
                                   attribute.IsValidForRead == true &&
                                   (attribute.IsValidForCreate == true || attribute.IsValidForUpdate == true),
                    Targets = attribute is LookupAttributeMetadata lookup ? lookup.Targets ?? [] : [],
                })
                .OrderBy(attribute => attribute.LogicalName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    private static string GetLabel(Microsoft.Xrm.Sdk.Label? label, string fallback)
    {
        return label?.UserLocalizedLabel?.Label
               ?? label?.LocalizedLabels.FirstOrDefault(item => item.LanguageCode == 1033)?.Label
               ?? label?.LocalizedLabels.FirstOrDefault()?.Label
               ?? fallback;
    }

    private void EnsureConnected()
    {
        if (_client?.IsReady != true)
        {
            throw new InvalidOperationException("Connect to Dataverse first.");
        }
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
