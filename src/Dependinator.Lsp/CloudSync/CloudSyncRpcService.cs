using Dependinator.Core.CloudSync;
using Dependinator.Core.Utils;
using Dependinator.Core.Utils.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Shared;
using static Dependinator.Core.Utils.ResultShim;

namespace Dependinator.Lsp.CloudSync;

// Parameters and result of the interactive Clerk sign-in the extension runs on request
// (loopback browser flow; the extension stores the token in VS Code secret storage).
public record SignInParams
{
    public static readonly string Method = "dependinator/cloudSync/signIn";
}

public record SignInResult(string Token);

public record ClearTokenParams
{
    public static readonly string Method = "dependinator/cloudSync/clearToken";
}

// Cloud sync operations invoked by the webview UI over the JSON-RPC tunnel. Delegates
// HTTP calls to the shared Core client and interactive sign-in/token custody to the extension.
class CloudSyncRpcService : ICloudSyncRpcService
{
    const string NotConfiguredError = "VS Code device sync is not configured. Set dependinator.cloudSync.baseUrl.";
    const string LoginRequiredError = "Device sync requires login.";

    // The extension enforces a 5 minute sign-in timeout; allow a little extra for the round trip.
    static readonly TimeSpan signInTimeout = TimeSpan.FromMinutes(5.5);

    readonly ICloudSyncHttpClient httpClient;
    readonly LspCloudSyncContext context;
    readonly ILanguageServerFacade server;

    public CloudSyncRpcService(
        ICloudSyncHttpClient httpClient,
        LspCloudSyncContext context,
        ILanguageServerFacade server
    )
    {
        this.httpClient = httpClient;
        this.context = context;
        this.server = server;
    }

    // Asks the extension to run the interactive Clerk sign-in and verifies the acquired token.
    public async Task<Result<CloudAuthState>> LoginAsync()
    {
        if (!context.IsEnabled)
            return new Error(NotConfiguredError);

        string token;
        try
        {
            using CancellationTokenSource cts = new(signInTimeout);
            SignInResult result = await server
                .SendRequest(SignInParams.Method, new SignInParams())
                .Returning<SignInResult>(cts.Token);
            token = result.Token;
        }
        catch (OperationCanceledException)
        {
            return new Error("Sign-in timed out. Please try again.");
        }
        catch (Exception ex)
        {
            return new Error("Sign-in failed.", ex);
        }

        if (string.IsNullOrWhiteSpace(token))
            return new Error("Sign-in did not return a token.");

        context.SetToken(token);
        Log.Info("Cloud sync access token acquired via extension sign-in");

        if (!Try(out var authState, out var error, await httpClient.GetAuthStateAsync()))
            return error;

        if (!authState.IsAuthenticated)
        {
            // The fresh token was rejected by the API; clear it so the user can retry cleanly.
            Log.Warn("Token acquisition succeeded, but the API returned unauthenticated");
            await ClearStoredTokenAsync();
            return new Error(
                "Device sync login completed, but the API did not accept the token. Check Clerk configuration."
            );
        }

        return authState;
    }

    // Clears the token here and in the extension's secret storage.
    public async Task<Result<CloudAuthState>> LogoutAsync()
    {
        await ClearStoredTokenAsync();
        return SignedOutState();
    }

    public async Task<Result<CloudAuthState>> GetAuthStateAsync()
    {
        if (!context.IsEnabled)
            return new CloudAuthState(IsAvailable: false, IsAuthenticated: false, User: null);

        if (!context.HasToken)
            return SignedOutState();

        if (!Try(out var authState, out var error, await httpClient.GetAuthStateAsync()))
            return error;

        if (!authState.IsAuthenticated)
        {
            // The stored token was rejected by the API; drop it so the user is prompted to log in again.
            Log.Warn("Stored cloud sync token was not accepted by the API; clearing it");
            await ClearStoredTokenAsync();
            return SignedOutState();
        }

        return authState;
    }

    public async Task<Result<CloudModelList>> ListAsync()
    {
        if (!Try(out var error, RequireToken()))
            return error;

        return await httpClient.ListAsync();
    }

    public async Task<Result<CloudModelMetadata>> PushAsync(CloudModelDocument document)
    {
        if (!Try(out var error, RequireToken()))
            return error;

        return await httpClient.PushAsync(document);
    }

    public async Task<Result<CloudModelDocument>> PullAsync(string modelKey)
    {
        if (!Try(out var error, RequireToken()))
            return error;

        return await httpClient.PullAsync(modelKey);
    }

    public async Task<Result> DeleteAsync(string modelKey)
    {
        if (!Try(out var error, RequireToken()))
            return error;

        return await httpClient.DeleteAsync(modelKey);
    }

    Result RequireToken()
    {
        if (!context.IsEnabled)
            return new Error(NotConfiguredError);
        if (!context.HasToken)
            return new Error(LoginRequiredError);
        return Result.Ok;
    }

    async Task ClearStoredTokenAsync()
    {
        context.SetToken(null);
        try
        {
            await server
                .SendRequest(ClearTokenParams.Method, new ClearTokenParams())
                .ReturningVoid(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log.Exception(ex, "Failed to clear token in extension");
        }
    }

    CloudAuthState SignedOutState() => new(IsAvailable: context.IsEnabled, IsAuthenticated: false, User: null);
}
