using Dependinator.Core.CloudSync;
using Dependinator.UI.Modeling.Dtos;
using Microsoft.Extensions.Options;
using Shared;

namespace Dependinator.UI.Shared.CloudSync;

// Cloud sync transport for browser-hosted app modes: Clerk sign-in via JS interop and
// API calls through the shared Core HTTP client (token supplied by ClerkCloudSyncApiContext).
sealed class HttpCloudSyncService : ICloudSyncService
{
    readonly ICloudSyncHttpClient httpClient;
    readonly IJSInterop jsInterop;
    readonly CloudSyncClientOptions options;

    public HttpCloudSyncService(
        ICloudSyncHttpClient httpClient,
        IJSInterop jsInterop,
        IOptions<CloudSyncClientOptions> options
    )
    {
        this.httpClient = httpClient;
        this.jsInterop = jsInterop;
        this.options = options.Value;
    }

    // Returns true when cloud sync HTTP transport is enabled.
    public bool IsAvailable => options.Enabled;

    // Opens Clerk sign-in modal and waits for the user to complete sign-in.
    public async Task<Result<CloudAuthState>> LoginAsync()
    {
        try
        {
            bool success = await jsInterop.Call<bool>("clerkSignIn");
            if (!success)
                return new Error("Sign-in was canceled or timed out.");

            return await GetAuthStateAsync();
        }
        catch (Exception ex)
        {
            return new Error(ex);
        }
    }

    // Signs out via Clerk.
    public async Task<Result<CloudAuthState>> LogoutAsync()
    {
        try
        {
            await jsInterop.Call("clerkSignOut");
        }
        catch (Exception ex)
        {
            return new Error(ex);
        }

        return new CloudAuthState(IsAvailable: true, IsAuthenticated: false, User: null);
    }

    // Reads authenticated user context from the API.
    public async Task<Result<CloudAuthState>> GetAuthStateAsync()
    {
        return await httpClient.GetAuthStateAsync();
    }

    // Lists remote models for the signed-in user.
    public async Task<Result<CloudModelList>> ListAsync()
    {
        return await httpClient.ListAsync();
    }

    // Uploads current model DTO as a compressed document via PUT.
    public async Task<Result<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto)
    {
        CloudModelDocument document = CloudModelSerializer.CreateDocument(modelPath, modelDto);
        return await httpClient.PushAsync(document);
    }

    // Fetches a compressed remote model document and converts it back to local DTO.
    // Returns a NotFoundError when no remote model exists for the path.
    public async Task<Result<ModelDto>> PullAsync(string modelPath)
    {
        string modelKey = CloudModelPath.CreateKey(modelPath);
        Result<CloudModelDocument> documentResult = await httpClient.PullAsync(modelKey);
        if (documentResult is NotFoundError)
            return documentResult.Error;

        if (!Try(out var document, out var error, documentResult))
            return error;

        return CloudModelSerializer.ReadModel(document);
    }

    // Deletes the remote model for the path; a missing remote copy counts as already deleted.
    public async Task<Result> DeleteAsync(string modelPath)
    {
        string modelKey = CloudModelPath.CreateKey(modelPath);
        return await httpClient.DeleteAsync(modelKey);
    }
}
