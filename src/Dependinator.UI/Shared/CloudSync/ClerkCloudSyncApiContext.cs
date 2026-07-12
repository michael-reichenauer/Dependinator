using Dependinator.Core.CloudSync;
using Microsoft.Extensions.Options;

namespace Dependinator.UI.Shared.CloudSync;

// Cloud-sync API context for browser hosts: config from options and the Bearer token
// fetched fresh from the Clerk browser session via JS interop.
[Scoped]
sealed class ClerkCloudSyncApiContext : ICloudSyncApiContext
{
    readonly IJSInterop jsInterop;
    readonly CloudSyncClientOptions options;

    public ClerkCloudSyncApiContext(IJSInterop jsInterop, IOptions<CloudSyncClientOptions> options)
    {
        this.jsInterop = jsInterop;
        this.options = options.Value;
    }

    public bool IsEnabled => options.Enabled;

    public string? ApiBaseAddress => options.ApiBaseAddress;

    // Gets the current session JWT from Clerk; null (unauthenticated request) when unavailable.
    public async Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        try
        {
            return await jsInterop.Call<string?>("clerkGetToken");
        }
        catch
        {
            return null;
        }
    }
}
