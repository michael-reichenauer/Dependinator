using System.Collections.Concurrent;
using System.Text.Json;
using Dependinator.Models;
using Shared;

namespace Dependinator.Shared.CloudSync;

// Bridge interface used by the Blazor app to communicate with the VS Code extension host.
interface IVsCodeCloudSyncProxy : ICloudSyncService
{
    Task<bool> IsAvailableAsync();
    Task HandleResponseAsync(string message);
}

// Implements ICloudSyncService by forwarding calls through VS Code webview messages.
[Scoped]
class VsCodeCloudSyncProxy : IVsCodeCloudSyncProxy
{
    static readonly JsonSerializerOptions serializerOptions = new() { PropertyNameCaseInsensitive = true };
    readonly IJSInterop jSInterop;
    readonly TimeSpan requestTimeout;
    readonly TimeSpan loginRequestTimeout;
    readonly ConcurrentDictionary<string, TaskCompletionSource<CloudSyncEnvelope>> pendingRequests = new();

    public VsCodeCloudSyncProxy(IJSInterop jSInterop)
        : this(jSInterop, TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(5)) { }

    internal VsCodeCloudSyncProxy(IJSInterop jSInterop, TimeSpan requestTimeout, TimeSpan? loginRequestTimeout = null)
    {
        this.jSInterop = jSInterop;
        this.requestTimeout = requestTimeout;
        this.loginRequestTimeout = loginRequestTimeout ?? TimeSpan.FromMinutes(5);
    }

    public bool IsAvailable => true;

    // Checks whether the active host exposes the VS Code bridge object.
    public async Task<bool> IsAvailableAsync()
    {
        return await jSInterop.Call<bool>("isVsCodeWebView");
    }

    // Starts extension-hosted login flow.
    public async Task<R<CloudAuthState>> LoginAsync()
    {
        return await SendAndReadAsync<CloudAuthState>("login", timeoutOverride: loginRequestTimeout);
    }

    // Starts extension-hosted logout flow.
    public async Task<R<CloudAuthState>> LogoutAsync()
    {
        return await SendAndReadAsync<CloudAuthState>("logout");
    }

    // Fetches auth info from extension host side.
    public async Task<R<CloudAuthState>> GetAuthStateAsync()
    {
        return await SendAndReadAsync<CloudAuthState>("getAuthState");
    }

    // Lists remote models via extension host bridge.
    public async Task<R<CloudModelList>> ListAsync()
    {
        return await SendAndReadAsync<CloudModelList>("list");
    }

    // Pushes model payload to extension host and returns updated metadata.
    public async Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto)
    {
        CloudModelDocument document = CloudModelSerializer.CreateDocument(modelPath, modelDto);
        return await SendAndReadAsync<CloudModelMetadata>("push", document);
    }

    // Requests a remote model payload and decodes the response document locally.
    public async Task<R<ModelDto>> PullAsync(string modelPath)
    {
        string modelKey = CloudModelPath.CreateKey(modelPath);
        string normalizedPath = CloudModelPath.Normalize(modelPath);
        if (
            !Try(
                out var document,
                out var error,
                await SendAndReadAsync<CloudModelDocument>("pull", new CloudPullRequest(modelKey, normalizedPath))
            )
        )
            return error;

        return CloudModelSerializer.ReadModel(document);
    }

    // Completes pending bridge requests when extension sends async responses.
    public Task HandleResponseAsync(string message)
    {
        CloudSyncEnvelope? envelope = JsonSerializer.Deserialize<CloudSyncEnvelope>(message, serializerOptions);
        if (envelope is null || string.IsNullOrWhiteSpace(envelope.RequestId))
            return Task.CompletedTask;

        if (pendingRequests.TryRemove(envelope.RequestId, out TaskCompletionSource<CloudSyncEnvelope>? tcs))
            tcs.TrySetResult(envelope);

        return Task.CompletedTask;
    }

    // Sends a request packet, stores a completion source by request id, and waits for reply or timeout.

    async Task<R<T>> SendAndReadAsync<T>(string action, object? payload = null, TimeSpan? timeoutOverride = null)
    {
        string requestId = Guid.NewGuid().ToString("N");
        TaskCompletionSource<CloudSyncEnvelope> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingRequests[requestId] = tcs;

        try
        {
            string requestJson = JsonSerializer.Serialize(
                new CloudSyncEnvelope(requestId, action, JsonSerializer.Serialize(payload, serializerOptions), null),
                serializerOptions
            );

            bool isPosted = await jSInterop.Call<bool>(
                "postVsCodeMessage",
                new { type = "cloudSync/request", message = requestJson }
            );
            if (!isPosted)
            {
                pendingRequests.TryRemove(requestId, out _);
                return R.Error("VS Code cloud sync bridge is not available.");
            }

            TimeSpan effectiveTimeout = timeoutOverride ?? requestTimeout;
            Task completedTask = await Task.WhenAny(tcs.Task, Task.Delay(effectiveTimeout));
            if (completedTask != tcs.Task)
            {
                pendingRequests.TryRemove(requestId, out _);
                return R.Error($"VS Code cloud sync action '{action}' timed out.");
            }

            CloudSyncEnvelope response = await tcs.Task;
            if (!string.IsNullOrWhiteSpace(response.Error))
                return R.Error(response.Error);
            if (string.IsNullOrWhiteSpace(response.Payload))
                return R.Error($"VS Code cloud sync action '{action}' returned no payload.");

            T? value = JsonSerializer.Deserialize<T>(response.Payload, serializerOptions);
            if (value is null)
                return R.Error($"VS Code cloud sync action '{action}' returned an invalid payload.");

            return value;
        }
        catch (Exception ex)
        {
            pendingRequests.TryRemove(requestId, out _);
            return R.Error(ex);
        }
    }

    sealed record CloudSyncEnvelope(string RequestId, string Action, string? Payload, string? Error);

    sealed record CloudPullRequest(string ModelKey, string NormalizedPath);
}
