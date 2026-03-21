namespace Dependinator.Shared.CloudSync;

// Client-side configuration used by HTTP cloud sync clients.
public sealed class CloudSyncClientOptions
{
    public const string SectionName = "CloudSync";

    // Master switch for enabling cloud sync features.
    public bool Enabled { get; init; } = true;

    // Optional explicit base URL for API endpoints when not hosted at same origin.
    public string? ApiBaseAddress { get; init; }

    // Clerk publishable key for frontend authentication.
    public string? ClerkPublishableKey { get; init; }

    // Clerk sign-in page URL used for login redirection.
    public string LoginPath { get; init; } = "";

    // Clerk sign-out URL used for logout redirection.
    public string LogoutPath { get; init; } = "";
}
