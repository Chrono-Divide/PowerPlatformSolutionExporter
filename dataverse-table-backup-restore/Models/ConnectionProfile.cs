namespace dataverse_table_backup_restore.Models;

public sealed class ConnectionProfile
{
    public required string EnvironmentUrl { get; init; }
    public required string UserName { get; init; }
    public DateTimeOffset LastUsedUtc { get; init; }
}

public sealed class ConnectionProfileDocument
{
    public int FormatVersion { get; init; } = 1;
    public List<ConnectionProfile> Profiles { get; init; } = [];
}
