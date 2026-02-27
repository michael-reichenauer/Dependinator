using System.Net;
using System.Net.Http.Json;
using Dependinator.Core.Utils;
using Dependinator.Models;
using Dependinator.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Shared;
using static Dependinator.Core.Utils.Result;

namespace Dependinator.Wasm;

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

    public bool IsAvailable => options.Enabled;

    public Task<R<CloudAuthState>> LoginAsync()
    {
        NavigateToAuthPath(options.LoginPath, "post_login_redirect_uri");
        return Task.FromResult<R<CloudAuthState>>(new CloudAuthState(IsAvailable: true, IsAuthenticated: false, User: null));
    }

    public Task<R<CloudAuthState>> LogoutAsync()
    {
        NavigateToAuthPath(options.LogoutPath, "post_logout_redirect_uri");
        return Task.FromResult<R<CloudAuthState>>(new CloudAuthState(IsAvailable: true, IsAuthenticated: false, User: null));
    }

    public async Task<R<CloudAuthState>> GetAuthStateAsync()
    {
        return await SendAsync<CloudAuthState>(HttpMethod.Get, "/api/auth/me");
    }

    public async Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto)
    {
        CloudModelDocument document = CloudModelSerializer.CreateDocument(modelPath, modelDto);
        return await SendAsync<CloudModelMetadata>(HttpMethod.Put, $"/api/models/{document.ModelKey}", document);
    }

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

    void NavigateToAuthPath(string authPath, string redirectParameterName)
    {
        if (string.IsNullOrWhiteSpace(authPath))
            return;

        string absoluteAuthPath = navigationManager.ToAbsoluteUri(authPath).ToString();
        string redirectUri = Uri.EscapeDataString(navigationManager.Uri);
        navigationManager.NavigateTo($"{absoluteAuthPath}?{redirectParameterName}={redirectUri}", forceLoad: true);
    }

    string BuildApiUri(string path)
    {
        string normalizedPath = path.StartsWith("/", StringComparison.Ordinal) ? path : $"/{path}";
        if (string.IsNullOrWhiteSpace(options.ApiBaseAddress))
            return normalizedPath;

        return new Uri(new Uri(options.ApiBaseAddress), normalizedPath).ToString();
    }

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
