using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Dependinator.Shared;

public record VsCodeMessage(string Type, JsonElement Raw, string? Message);

interface IVsCodeMessageService
{
    Task InitAsync();
}

[Scoped]
class VsCodeMessageService : IVsCodeMessageService, IAsyncDisposable
{
    readonly IJSInterop jSInterop;
    private readonly IJsonRpcPacketWriter jsonRpcPacketWriter;
    DotNetObjectReference<VsCodeMessageService>? reference;

    public VsCodeMessageService(IJSInterop jSInterop, IJsonRpcPacketWriter jsonRpcPacketWriter)
    {
        this.jSInterop = jSInterop;
        this.jsonRpcPacketWriter = jsonRpcPacketWriter;
    }

    public async Task InitAsync()
    {
        if (reference != null)
            return;

        reference = jSInterop.Reference(this);
        await jSInterop.Call("listenToVsCodeMessages", reference, nameof(OnVsCodeMessage));

        jsonRpcPacketWriter.SetWriteStringPackageAction(SendPackageToLspAsync);
    }

    public async ValueTask SendPackageToLspAsync(string stringPackage, CancellationToken ct)
    {
        await jSInterop.Call<bool>("postVsCodeMessage", new { type = "lsp/message", message = stringPackage });
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
            Log.Error("Comunication Error", message, message);
            return;
        }

        await jsonRpcPacketWriter.WriteStringPackageAsync(message, CancellationToken.None);
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
