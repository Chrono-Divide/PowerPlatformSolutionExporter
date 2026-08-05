using dataverse_table_backup_restore.Services;
using Xunit;

namespace dataverse_table_backup_restore.Tests;

public sealed class ConnectionProfileStoreTests
{
    [Fact]
    public void ProfilesRoundTripWithoutSecretsAndCanBeRemoved()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "DataverseSnapshotManagerTests", Guid.NewGuid().ToString("N"));
        var profileFile = Path.Combine(testDirectory, "connection-profiles.json");

        try
        {
            var store = new ConnectionProfileStore(profileFile);
            store.Save("https://first.crm.dynamics.com/", "first@example.com");
            store.Save("https://first.crm.dynamics.com", "second@example.com");
            store.Save("https://second.crm.dynamics.com", string.Empty);

            var profiles = store.Load();
            Assert.Equal(3, profiles.Count);
            Assert.Contains(profiles, profile =>
                profile.EnvironmentUrl == "https://first.crm.dynamics.com" &&
                profile.UserName == "first@example.com");
            Assert.Contains(profiles, profile =>
                profile.EnvironmentUrl == "https://first.crm.dynamics.com" &&
                profile.UserName == "second@example.com");

            var json = File.ReadAllText(profileFile);
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("accessToken", json, StringComparison.OrdinalIgnoreCase);

            Assert.True(store.Remove("https://first.crm.dynamics.com", "first@example.com"));
            Assert.DoesNotContain(store.Load(), profile => profile.UserName == "first@example.com");
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
    public void InvalidOrCorruptProfilesDoNotBreakStartup()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "DataverseSnapshotManagerTests", Guid.NewGuid().ToString("N"));
        var profileFile = Path.Combine(testDirectory, "connection-profiles.json");

        try
        {
            Directory.CreateDirectory(testDirectory);
            File.WriteAllText(profileFile, "not json");
            var store = new ConnectionProfileStore(profileFile);

            Assert.Empty(store.Load());
            Assert.Throws<ArgumentException>(() => store.Save("not a Dataverse URL", "someone@example.com"));
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }
}
