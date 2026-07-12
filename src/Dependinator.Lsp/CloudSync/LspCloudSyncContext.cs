using Dependinator.Core.CloudSync;
using Newtonsoft.Json.Linq;

// Cloud sync in the LSP host: the extension owns the Clerk access token (VS Code secret
// storage) and hands it to this process, which performs the actual cloud-sync API calls
// on behalf of the webview UI.
namespace Dependinator.Lsp.CloudSync;

// Holds the cloud-sync configuration and access token provided by the VS Code extension,
// seeded from LSP initializationOptions and updated via notifications.
class LspCloudSyncContext : ICloudSyncApiContext
{
    string? accessToken;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiBaseAddress);

    public string? ApiBaseAddress { get; private set; }

    public bool HasToken => !string.IsNullOrWhiteSpace(accessToken);

    public Task<string?> GetAccessTokenAsync(CancellationToken ct) => Task.FromResult(accessToken);

    public void SetBaseUrl(string? baseUrl) =>
        ApiBaseAddress = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim();

    public void SetToken(string? token) => accessToken = string.IsNullOrWhiteSpace(token) ? null : token;

    // Seeds config and token from the extension's initializationOptions:
    // { cloudSync: { baseUrl, accessToken } }.
    public void InitializeFromOptions(object? initializationOptions)
    {
        if (initializationOptions is not JToken options)
            return;

        JToken? cloudSync = options["cloudSync"];
        if (cloudSync is null)
            return;

        SetBaseUrl(cloudSync.Value<string>("baseUrl"));
        SetToken(cloudSync.Value<string>("accessToken"));
    }
}
