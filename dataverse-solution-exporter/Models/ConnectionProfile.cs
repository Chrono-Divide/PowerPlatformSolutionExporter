namespace dataverse_solution_exporter.Models;

public sealed class ConnectionProfile
{
    public required string EnvironmentUrl { get; init; }
    public required string UserName { get; init; }
    public DateTimeOffset LastUsedUtc { get; init; }

    public override string ToString() => EnvironmentUrl;
}

public sealed class ConnectionProfileDocument
{
    public int FormatVersion { get; init; } = 1;
    public List<ConnectionProfile> Profiles { get; init; } = [];
}
