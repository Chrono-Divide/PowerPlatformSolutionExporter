using dataverse_table_backup_restore.Models;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace dataverse_table_backup_restore.Tests;

public sealed class SnapshotModelTests
{
    [Fact]
    public void DesignCatalogContainsExactlyTheVerified72Tables()
    {
        Assert.Equal(72, DesignTableCatalog.LogicalNames.Count);
        Assert.All(DesignTableCatalog.LogicalNames, name => Assert.StartsWith("kfs_", name));
        Assert.Contains("kfs_jiseki_shori_jouhou", DesignTableCatalog.LogicalNames);
        Assert.Contains("kfs_youkyuu_shorui", DesignTableCatalog.LogicalNames);
    }

    [Fact]
    public void SnapshotValuesRoundTripSupportedDataverseTypes()
    {
        var id = Guid.NewGuid();
        var values = new object?[]
        {
            null,
            "example",
            true,
            42,
            9876543210L,
            123.45m,
            new Money(500.25m),
            new OptionSetValue(7),
            new OptionSetValueCollection([new OptionSetValue(1), new OptionSetValue(3)]),
            new EntityReference("kfs_user", id) { Name = "Example User" },
            id,
            new DateTime(2026, 8, 5, 10, 30, 0, DateTimeKind.Utc),
            new byte[] { 1, 2, 3, 4 },
        };

        foreach (var value in values)
        {
            var snapshot = SnapshotValue.FromSdkValue(value);
            Assert.False(snapshot.IsUnsupported);
            var restored = snapshot.ToSdkValue();

            if (value is EntityReference expectedReference)
            {
                var actualReference = Assert.IsType<EntityReference>(restored);
                Assert.Equal(expectedReference.LogicalName, actualReference.LogicalName);
                Assert.Equal(expectedReference.Id, actualReference.Id);
            }
            else if (value is Money expectedMoney)
            {
                Assert.Equal(expectedMoney.Value, Assert.IsType<Money>(restored).Value);
            }
            else if (value is OptionSetValue expectedOption)
            {
                Assert.Equal(expectedOption.Value, Assert.IsType<OptionSetValue>(restored).Value);
            }
            else if (value is OptionSetValueCollection expectedOptions)
            {
                Assert.Equal(expectedOptions.Select(item => item.Value), Assert.IsType<OptionSetValueCollection>(restored).Select(item => item.Value));
            }
            else if (value is byte[] expectedBytes)
            {
                Assert.Equal(expectedBytes, Assert.IsType<byte[]>(restored));
            }
            else
            {
                Assert.Equal(value, restored);
            }
        }
    }

    [Fact]
    public void UnsupportedValuesAreExplicitlyMarked()
    {
        var snapshot = SnapshotValue.FromSdkValue(new Version(1, 2, 3));
        Assert.True(snapshot.IsUnsupported);
        Assert.Throws<NotSupportedException>(() => snapshot.ToSdkValue());
    }
}
