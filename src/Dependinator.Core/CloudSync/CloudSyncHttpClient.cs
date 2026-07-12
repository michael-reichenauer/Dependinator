using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shared;

namespace Dependinator.Core.CloudSync;

// HTTP client for the cloud-sync API, shared by browser hosts (Clerk token via JS interop)
// and the LSP host (token provided by the VS Code extension).
interface ICloudSyncHttpClient
{
    Task<R<CloudAuthState>> GetAuthStateAsync(CancellationToken ct = default);
    Task<R<CloudModelList>> ListAsync(CancellationToken ct = default);
    Task<R<CloudModelMetadata>> PushAsync(CloudModelDocument document, CancellationToken ct = default);
    Task<R<CloudModelDocument>> PullAsync(string modelKey, CancellationToken ct = default);
}

[Transient]
sealed class CloudSyncHttpClient : ICloudSyncHttpClient
{
    readonly HttpClient httpClient;
    readonly ICloudSyncApiContext context;

    public CloudSyncHttpClient(HttpClient httpClient, ICloudSyncApiContext context)
    {
        this.httpClient = httpClient;
        this.context = context;
    }

    // Reads authenticated user context from the API.
    public async Task<R<CloudAuthState>> GetAuthStateAsync(CancellationToken ct = default)
    {
        return await SendAsync<CloudAuthState>(HttpMethod.Get, "/api/auth/me", ct: ct);
    }

    // Lists remote models for the signed-in user.
    public async Task<R<CloudModelList>> ListAsync(CancellationToken ct = default)
    {
        return await SendAsync<CloudModelList>(HttpMethod.Get, "/api/models", ct: ct);
    }

    // Uploads a compressed model document via PUT.
    public async Task<R<CloudModelMetadata>> PushAsync(CloudModelDocument document, CancellationToken ct = default)
    {
        return await SendAsync<CloudModelMetadata>(HttpMethod.Put, $"/api/models/{document.ModelKey}", document, ct);
    }

    // Fetches a compressed remote model document.
    public async Task<R<CloudModelDocument>> PullAsync(string modelKey, CancellationToken ct = default)
    {
        return await SendAsync<CloudModelDocument>(HttpMethod.Get, $"/api/models/{modelKey}", ct: ct);
    }

    // Generic JSON helper for API calls with the host-provided Bearer token.
    async Task<R<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? content = null,
        CancellationToken ct = default
    )
    {
        try
        {
            using HttpRequestMessage request = new(method, BuildApiUri(path));

            string? token = await context.GetAccessTokenAsync(ct);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                // Azure SWA may strip the standard Authorization header before forwarding
                // to the managed Functions backend. Send via custom header as well.
                request.Headers.TryAddWithoutValidation("X-Dependinator-Authorization", $"Bearer {token}");
            }

            if (content is not null)
            {
                byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(content, content.GetType());
                request.Content = new ByteArrayContent(jsonBytes);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
                {
                    CharSet = "utf-8",
                };
            }

            using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                T? value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
                if (value is null)
                    return R.Error($"Cloud sync endpoint '{path}' returned an empty response.");

                return value;
            }

            string errorMessage = await ReadErrorMessageAsync(response, ct);
            return R.Error(errorMessage);
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }
    }

    // Builds absolute API URI when a base address override is configured.
    string BuildApiUri(string path)
    {
        string normalizedPath = path.StartsWith("/", StringComparison.Ordinal) ? path : $"/{path}";
        if (string.IsNullOrWhiteSpace(context.ApiBaseAddress))
            return normalizedPath;

        return new Uri(new Uri(context.ApiBaseAddress), normalizedPath).ToString();
    }

    // Parses structured API errors and falls back to status-based user messages.
    static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            ErrorResponse? errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(
                cancellationToken: ct
            );
            if (!string.IsNullOrWhiteSpace(errorResponse?.Message))
                return errorResponse.Message;
        }
        catch (Exception)
        {
            // Error body was not the expected JSON (e.g. an HTML page from a proxy);
            // fall through to the status-code based message.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Device sync is not enabled.",
            HttpStatusCode.NotFound => "Cloud model was not found.",
            _ => $"Cloud sync request failed with status code {(int)response.StatusCode}.",
        };
    }

    sealed record ErrorResponse(string Message);
}
