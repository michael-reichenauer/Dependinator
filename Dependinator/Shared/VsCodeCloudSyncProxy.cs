using System.Collections.Concurrent;
using System.Text.Json;
using Dependinator.Models;
using Shared;

namespace Dependinator.Shared;

interface IVsCodeCloudSyncProxy
{
    Task<bool> IsAvailableAsync();
    Task<R<CloudAuthState>> LoginAsync();
    Task<R<CloudAuthState>> LogoutAsync();
    Task<R<CloudAuthState>> GetAuthStateAsync();
    Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto);
    Task<R<ModelDto>> PullAsync(string modelPath);
    Task HandleResponseAsync(string message);
}

[Scoped]
class VsCodeCloudSyncProxy(IJSInterop jSInterop) : IVsCodeCloudSyncProxy
{
    static readonly JsonSerializerOptions serializerOptions = new() { PropertyNameCaseInsensitive = true };
    readonly ConcurrentDictionary<string, TaskCompletionSource<CloudSyncEnvelope>> pendingRequests = new();

    public async Task<bool> IsAvailableAsync()
    {
        return await jSInterop.Call<bool>("isVsCodeWebView");
    }

    public async Task<R<CloudAuthState>> LoginAsync()
    {
        return await SendAndReadAsync<CloudAuthState>("login");
    }

    public async Task<R<CloudAuthState>> LogoutAsync()
    {
        return await SendAndReadAsync<CloudAuthState>("logout");
    }

    public async Task<R<CloudAuthState>> GetAuthStateAsync()
    {
        return await SendAndReadAsync<CloudAuthState>("getAuthState");
    }

    public async Task<R<CloudModelMetadata>> PushAsync(string modelPath, ModelDto modelDto)
    {
        CloudModelDocument document = CloudModelSerializer.CreateDocument(modelPath, modelDto);
        return await SendAndReadAsync<CloudModelMetadata>("push", document);
    }

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

    public Task HandleResponseAsync(string message)
    {
        CloudSyncEnvelope? envelope = JsonSerializer.Deserialize<CloudSyncEnvelope>(message, serializerOptions);
        if (envelope is null || string.IsNullOrWhiteSpace(envelope.RequestId))
            return Task.CompletedTask;

        if (pendingRequests.TryRemove(envelope.RequestId, out TaskCompletionSource<CloudSyncEnvelope>? tcs))
            tcs.TrySetResult(envelope);

        return Task.CompletedTask;
    }

    async Task<R<T>> SendAndReadAsync<T>(string action, object? payload = null)
    {
        string requestId = Guid.NewGuid().ToString("N");
        TaskCompletionSource<CloudSyncEnvelope> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingRequests[requestId] = tcs;

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

    sealed record CloudSyncEnvelope(string RequestId, string Action, string? Payload, string? Error);

    sealed record CloudPullRequest(string ModelKey, string NormalizedPath);
}
