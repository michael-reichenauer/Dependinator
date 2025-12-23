using System.Text.Json;
using Dependinator.Utils;
using Dependinator.Utils.Logging;
using Microsoft.JSInterop;

namespace Dependinator.Shared;

public record VsCodeMessage(string Type, JsonElement Raw, string? Message, string? Error, string? Path);

interface IVsCodeMessageService
{
    event Action<VsCodeMessage> MessageReceived;
    VsCodeMessage? LastMessage { get; }
    Task InitAsync();
}

[Scoped]
class VsCodeMessageService : IVsCodeMessageService, IAsyncDisposable
{
    readonly IJSInterop jSInterop;
    readonly IApplicationEvents applicationEvents;
    DotNetObjectReference<VsCodeMessageService>? reference;

    public event Action<VsCodeMessage> MessageReceived = null!;
    public VsCodeMessage? LastMessage { get; private set; }

    public VsCodeMessageService(IJSInterop jSInterop, IApplicationEvents applicationEvents)
    {
        this.jSInterop = jSInterop;
        this.applicationEvents = applicationEvents;
    }

    public async Task InitAsync()
    {
        if (reference != null)
            return;

        reference = jSInterop.Reference(this);
        await jSInterop.Call("listenToVsCodeMessages", reference, nameof(OnVsCodeMessage));
    }

    [JSInvokable]
    public Task OnVsCodeMessage(JsonElement message)
    {
        if (!message.TryGetProperty("type", out var typeElement))
            return Task.CompletedTask;

        var type = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(type))
            return Task.CompletedTask;

        var info = new VsCodeMessage(
            type,
            message,
            TryGetString(message, "message"),
            TryGetString(message, "error"),
            TryGetString(message, "path")
        );

        LastMessage = info;
        MessageReceived?.Invoke(info);
        applicationEvents.TriggerUIStateChanged();

        Log.Info("VS Code message:", info.Type, info.Message ?? info.Error ?? info.Path);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        reference?.Dispose();
        return ValueTask.CompletedTask;
    }

    static string? TryGetString(JsonElement message, string propertyName)
    {
        if (!message.TryGetProperty(propertyName, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }
}
