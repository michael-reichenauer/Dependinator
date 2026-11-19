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
    // their 'iat' (issued-at) time. It matters for the VS Code extension, which signs in
    // once and sends that one token until it is rejected; the browser hosts get a fresh
    // token from their Clerk session for each call. A year, so a device signed in once
    // stays signed in for well over six months. See CloudSyncBearerTokenValidator for
    // details and how to revert once a paid plan allows configuring longer token lifetimes.
    public int MaxTokenAgeDays { get; init; } = 365;
}
