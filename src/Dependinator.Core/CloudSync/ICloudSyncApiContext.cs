// Client-side cloud sync transport shared by all hosts: the HTTP client that talks to the
// cloud-sync API and the RPC surface the VS Code webview UI uses to reach the LSP-hosted
// implementation. Each host supplies its own ICloudSyncApiContext (config + access token).
namespace Dependinator.Core.CloudSync;

// Per-host configuration and credentials for the cloud-sync API transport.
interface ICloudSyncApiContext
{
    // False when cloud sync is disabled or not configured in the current host.
    bool IsEnabled { get; }

    // Base URL of the API; null/empty means same-origin relative URIs (browser hosts).
    string? ApiBaseAddress { get; }

    // Current bearer token, or null when signed out (requests are then sent unauthenticated).
    Task<string?> GetAccessTokenAsync(CancellationToken ct);
}
