namespace Api;

public sealed class CloudSyncOptions
{
    public const string SectionName = "CloudSync";

    public string ContainerName { get; init; } = "dependinator-models";
    public long MaxUserQuotaBytes { get; init; } = 10 * 1024 * 1024;
    public string? StorageConnectionString { get; init; }
    public string? ClerkIssuer { get; init; }

    // WORKAROUND: The auth provider's free tier caps token lifetime ('exp') at 7 days.
    // Instead of the token's own 'exp', the API accepts tokens for this many days after
    // their 'iat' (issued-at) time. See CloudSyncBearerTokenValidator for details and
    // how to revert once a paid plan allows configuring longer token lifetimes.
    public int MaxTokenAgeDays { get; init; } = 180;
}
