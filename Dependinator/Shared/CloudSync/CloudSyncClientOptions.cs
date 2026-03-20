namespace Dependinator.Shared.CloudSync;

// Client-side configuration used by HTTP cloud sync clients.
public sealed class CloudSyncClientOptions
{
    public const string SectionName = "CloudSync";

    // Master switch for enabling cloud sync features.
    public bool Enabled { get; init; } = true;

    // Optional explicit base URL for API endpoints when not hosted at same origin.
    public string? ApiBaseAddress { get; init; }

    // Path used for hosted auth login redirection.
    public string LoginPath { get; init; } = "/.auth/login/entraExternalId";

    // Path used for hosted auth logout redirection.
    public string LogoutPath { get; init; } = "/.auth/logout";
}
