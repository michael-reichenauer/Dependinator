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
    public void Create_ShouldNotContainEmail_WhenEmailIsPresent()
    {
        CloudUserInfo user = new("swa-user-123", "User@example.com");

        string storageKey = CloudUserStorageKey.Create(user);

        Assert.DoesNotContain("user@example.com", storageKey);
        Assert.Matches("^email-[0-9a-f]{64}$", storageKey);
    }

    [Fact]
    public void Create_ShouldFallBackToHashedUserId_WhenEmailIsMissing()
    {
        CloudUserInfo user = new("user-123", null);

        string storageKey = CloudUserStorageKey.Create(user);

        Assert.DoesNotContain("user-123", storageKey);
        Assert.Matches("^user-[0-9a-f]{64}$", storageKey);
    }
}
