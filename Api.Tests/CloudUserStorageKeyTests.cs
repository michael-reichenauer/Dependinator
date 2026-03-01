using Api;

namespace Api.Tests;

public class CloudUserStorageKeyTests
{
    [Fact]
    public void Create_ShouldReturnSameKey_WhenEmailsMatchButProviderIdsDiffer()
    {
        CloudUserInfo browserUser = new("swa-user-123", "User@example.com");
        CloudUserInfo vsCodeUser = new("oid-456", "user@example.com");

        string browserKey = CloudUserStorageKey.Create(browserUser);
        string vsCodeKey = CloudUserStorageKey.Create(vsCodeUser);

        Assert.Equal(browserKey, vsCodeKey);
    }

    [Fact]
    public void Create_ShouldFallBackToUserId_WhenEmailIsMissing()
    {
        CloudUserInfo user = new("user-123", null);

        string storageKey = CloudUserStorageKey.Create(user);

        Assert.Equal("user-123", storageKey);
    }
}
