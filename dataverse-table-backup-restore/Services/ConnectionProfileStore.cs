using System.Text.Json;
using dataverse_table_backup_restore.Models;

namespace dataverse_table_backup_restore.Services;

public sealed class ConnectionProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _filePath;

    public ConnectionProfileStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataverseSnapshotManager",
            "connection-profiles.json");
    }

    public IReadOnlyList<ConnectionProfile> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var document = JsonSerializer.Deserialize<ConnectionProfileDocument>(json, JsonOptions);
            return (document?.Profiles ?? [])
                .Where(profile => profile is not null && TryNormalizeEnvironmentUrl(profile.EnvironmentUrl, out _))
                .GroupBy(
                    profile => $"{NormalizeEnvironmentUrl(profile.EnvironmentUrl)}\n{profile.UserName?.Trim() ?? string.Empty}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(profile => profile.LastUsedUtc).First())
                .Select(profile => new ConnectionProfile
                {
                    EnvironmentUrl = NormalizeEnvironmentUrl(profile.EnvironmentUrl),
                    UserName = profile.UserName?.Trim() ?? string.Empty,
                    LastUsedUtc = profile.LastUsedUtc,
                })
                .OrderByDescending(profile => profile.LastUsedUtc)
                .ToList();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public ConnectionProfile Save(string environmentUrl, string? userName)
    {
        var normalizedUrl = NormalizeEnvironmentUrl(environmentUrl);
        var normalizedUserName = userName?.Trim() ?? string.Empty;
        var profiles = Load()
            .Where(profile => !IsMatch(profile, normalizedUrl, normalizedUserName))
            .ToList();
        var savedProfile = new ConnectionProfile
        {
            EnvironmentUrl = normalizedUrl,
            UserName = normalizedUserName,
            LastUsedUtc = DateTimeOffset.UtcNow,
        };
        profiles.Insert(0, savedProfile);
        Write(profiles);
        return savedProfile;
    }

    public bool Remove(string environmentUrl, string? userName)
    {
        var normalizedUrl = NormalizeEnvironmentUrl(environmentUrl);
        var normalizedUserName = userName?.Trim() ?? string.Empty;
        var profiles = Load().ToList();
        var removed = profiles.RemoveAll(profile => IsMatch(profile, normalizedUrl, normalizedUserName)) > 0;
        if (removed)
        {
            Write(profiles);
        }

        return removed;
    }

    public static string NormalizeEnvironmentUrl(string environmentUrl)
    {
        if (!TryNormalizeEnvironmentUrl(environmentUrl, out var normalizedUrl))
        {
            throw new ArgumentException("Enter a valid HTTPS Dataverse environment URL.", nameof(environmentUrl));
        }

        return normalizedUrl;
    }

    private static bool TryNormalizeEnvironmentUrl(string environmentUrl, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (!Uri.TryCreate(environmentUrl?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        normalizedUrl = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    private static bool IsMatch(ConnectionProfile profile, string environmentUrl, string userName)
    {
        return string.Equals(
                   NormalizeEnvironmentUrl(profile.EnvironmentUrl),
                   environmentUrl,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(profile.UserName?.Trim() ?? string.Empty, userName, StringComparison.OrdinalIgnoreCase);
    }

    private void Write(IReadOnlyCollection<ConnectionProfile> profiles)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The connection profile path has no parent directory.");
        Directory.CreateDirectory(directory);
        var document = new ConnectionProfileDocument { Profiles = profiles.ToList() };
        var temporaryFile = _filePath + ".tmp";
        File.WriteAllText(temporaryFile, JsonSerializer.Serialize(document, JsonOptions));
        File.Move(temporaryFile, _filePath, overwrite: true);
    }
}
