using System.Net;
using System.Net.Http.Json;
using Dependinator.Modeling.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Shared;

namespace Dependinator.Shared.CloudSync;

// HTTP transport implementation for cloud sync used in browser-hosted app modes.
sealed class HttpCloudSyncService : ICloudSyncService
{
    readonly HttpClient httpClient;
    readonly NavigationManager navigationManager;
    readonly CloudSyncClientOptions options;

    public HttpCloudSyncService(
        HttpClient httpClient,
        NavigationManager navigationManager,
        IOptions<CloudSyncClientOptions> options
    )
    {
        this.httpClient = httpClient;
        this.navigationManager = navigationManager;
        this.options = options.Value;
    }

    // Returns true when cloud sync HTTP transport is enabled.
    public bool IsAvailable => options.Enabled;

    // Starts browser auth flow by navigating to the configured login endpoint.

    public Task<R<CloudAuthState>> LoginAsync()
    {
        NavigateToAuthPath(options.LoginPath, "post_login_redirect_uri");
        return Task.FromResult<R<CloudAuthState>>(
            new CloudAuthState(IsAvailable: true, IsAuthenticated: false, User: null)
        );
    }

    // Starts browser logout flow by navigating to the configured logout endpoint.
    public Task<R<CloudAuthState>> LogoutAsync()
    {
        NavigateToAuthPath(options.LogoutPath, "post_logout_redirect_uri");
        return Task.FromResult<R<CloudAuthState>>(
            new CloudAuthState(IsAvailable: true, IsAuthenticated: false, User: null)
        );
    }

    // Reads authenticated user context from the API.
    public async Task<R<CloudAuthState>> GetAuthStateAsync()
    {
        return await SendAsync<CloudAuthState>(HttpMethod.Get, "/api/auth/me");
    }

    // Lists remote models for the signed-in user.
    public async Task<R<CloudModelList>> ListAsync()
    {
        return await SendAsync<CloudModelList>(HttpMethod.Get, "/api/models");
    }

    // Uploads current model DTO as a compressed document via PUT.
    public async Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto)
    {
        CloudModelDocument document = CloudModelSerializer.CreateDocument(modelPath, modelDto);
        return await SendAsync<CloudModelMetadata>(HttpMethod.Put, $"/api/models/{document.ModelKey}", document);
    }

    // Fetches a compressed remote model document and converts it back to local DTO.
    public async Task<R<ModelDto>> PullAsync(string modelPath)
    {
        string modelKey = CloudModelPath.CreateKey(modelPath);
        if (
            !Try(
                out var document,
                out var error,
                await SendAsync<CloudModelDocument>(HttpMethod.Get, $"/api/models/{modelKey}")
            )
        )
            return error;

        return CloudModelSerializer.ReadModel(document);
    }

    // Generic JSON helper for API calls with mapped error handling.
    async Task<R<T>> SendAsync<T>(HttpMethod method, string path, object? content = null)
    {
        try
        {
            using HttpRequestMessage request = new(method, BuildApiUri(path));
            if (content is not null)
                request.Content = JsonContent.Create(content);

            using HttpResponseMessage response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                T? value = await response.Content.ReadFromJsonAsync<T>();
                if (value is null)
                    return R.Error($"Cloud sync endpoint '{path}' returned an empty response.");

                return value;
            }

            string errorMessage = await ReadErrorMessageAsync(response);
            return R.Error(errorMessage);
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }
    }

    // Redirects the browser to a server auth endpoint with a return-url query parameter.

    void NavigateToAuthPath(string authPath, string redirectParameterName)
    {
        if (string.IsNullOrWhiteSpace(authPath))
            return;

        string absoluteAuthPath = navigationManager.ToAbsoluteUri(authPath).ToString();
        string redirectUri = Uri.EscapeDataString(navigationManager.Uri);
        navigationManager.NavigateTo($"{absoluteAuthPath}?{redirectParameterName}={redirectUri}", forceLoad: true);
    }

    // Builds absolute API URI when a base address override is configured.
    string BuildApiUri(string path)
    {
        string normalizedPath = path.StartsWith("/", StringComparison.Ordinal) ? path : $"/{path}";
        if (string.IsNullOrWhiteSpace(options.ApiBaseAddress))
            return normalizedPath;

        return new Uri(new Uri(options.ApiBaseAddress), normalizedPath).ToString();
    }

    // Parses structured API errors and falls back to status-based user messages.
    static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        ErrorResponse? errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        if (!string.IsNullOrWhiteSpace(errorResponse?.Message))
            return errorResponse.Message;

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Cloud sync requires login.",
            HttpStatusCode.NotFound => "Cloud model was not found.",
            _ => $"Cloud sync request failed with status code {(int)response.StatusCode}.",
        };
    }

    sealed record ErrorResponse(string Message);
}
