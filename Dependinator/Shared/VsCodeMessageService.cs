using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Dependinator.Shared;

public record VsCodeMessage(string Type, JsonElement Raw, string? Message);

// Receives and sends messages from and to the VS Code Extension Host
// Used to communicate with the Language Server via the the extension host.
interface IVsCodeMessageService
{
    Task InitAsync();
}

[Scoped]
class VsCodeMessageService : IVsCodeMessageService, IAsyncDisposable
{
    readonly IJSInterop jSInterop;
    private readonly IJsonRpcService jsonRpcService;
    DotNetObjectReference<VsCodeMessageService>? reference;

    public VsCodeMessageService(IJSInterop jSInterop, IJsonRpcService jsonRpcService)
    {
        this.jSInterop = jSInterop;
        this.jsonRpcService = jsonRpcService;
    }

    public async Task InitAsync()
    {
        if (reference != null)
            return;

        var isVsCodeWebView = await jSInterop.Call<bool>("isVsCodeWebView");
        if (!isVsCodeWebView)
            return;

        reference = jSInterop.Reference(this);
        await jSInterop.Call("listenToVsCodeMessages", reference, nameof(OnVsCodeMessage));

        jsonRpcService.RegisterSendMessageAction(SendMessageToLspAsync);
    }

    public async ValueTask SendMessageToLspAsync(string base64Message, CancellationToken ct)
    {
        await jSInterop.Call<bool>("postVsCodeMessage", new { type = "lsp/message", message = base64Message });
    }

    [JSInvokable]
    public async Task OnVsCodeMessage(JsonElement raw)
    {
        if (!raw.TryGetProperty("type", out var typeElement))
            return;

        var type = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(type))
            return;

        var message = TryGetString(raw, "message");
        if (message is null)
            return;

        if (type == "ui/error")
        {
            Log.Error("Communication Error", message, message);
            return;
        }

        await jsonRpcService.AddReceivedMessageAsync(message, CancellationToken.None);
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
