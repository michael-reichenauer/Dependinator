using System.Text.Json;
using Dependinator.Utils;
using Dependinator.Utils.Logging;
using Microsoft.JSInterop;

namespace Dependinator.Shared;

public record VsCodeMessage(string Type, JsonElement Raw, string? Message);

interface IVsCodeMessageService
{
    event Action<VsCodeMessage> MessageReceived;
    ValueTask<bool> PostAsync(string type, object? data = null);
    Task InitAsync();
}

[Scoped]
class VsCodeMessageService : IVsCodeMessageService, IAsyncDisposable
{
    readonly IJSInterop jSInterop;
    DotNetObjectReference<VsCodeMessageService>? reference;

    public event Action<VsCodeMessage> MessageReceived = null!;

    public VsCodeMessageService(IJSInterop jSInterop)
    {
        this.jSInterop = jSInterop;
    }

    public async Task InitAsync()
    {
        if (reference != null)
            return;

        reference = jSInterop.Reference(this);
        await jSInterop.Call("listenToVsCodeMessages", reference, nameof(OnVsCodeMessage));
    }

    public async ValueTask<bool> PostAsync(string type, object? data = null)
    {
        return await jSInterop.Call<bool>("postVsCodeMessage", new { type = type, message = data });
    }

    [JSInvokable]
    public Task OnVsCodeMessage(JsonElement raw)
    {
        if (!raw.TryGetProperty("type", out var typeElement))
            return Task.CompletedTask;

        var type = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(type))
            return Task.CompletedTask;

        var message = new VsCodeMessage(type, raw, TryGetString(raw, "message"));
        if (type == "ui/error")
        {
            Log.Error("Comunication Error", message.Type, message.Message ?? "");
            return Task.CompletedTask;
        }

        MessageReceived?.Invoke(message);

        Log.Info("VS Code message:", message.Type, message.Message);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        reference?.Dispose();
        return ValueTask.CompletedTask;
    }

    static string? TryGetString(JsonElement raw, string propertyName)
    {
        if (!raw.TryGetProperty(propertyName, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }
}
