using StreamJsonRpc;

namespace Dependinator.Shared.Utils;

public interface IJsonRpcService
{
    // Registers the send message action, that should send the package to the "other" side
    void RegisterSendMessageAction(WriteBase64MessageActionAsync writeMessageActionAsync);

    // Adds the message, that was received from the "other" side
    ValueTask AddReceivedMessageAsync(string base64Message, CancellationToken ct);

    void AddLocalRpcTarget(object target);

    T GetRemoteProxy<T>()
        where T : class;

    void StartListening();
}

[Singleton]
public class JsonRpcService : IJsonRpcService, IAsyncDisposable, IDisposable
{
    readonly JsonRpcMessageHandler messageHandler = new();
    readonly JsonRpc jsonRpc;

    public JsonRpcService()
    {
        jsonRpc = new JsonRpc(messageHandler);
        // By default, this message handle is "self" connected, use ResisterSendMessageAction to register other side
        RegisterSendMessageAction(AddReceivedMessageAsync);
    }

    public void AddLocalRpcTarget(object target)
    {
        jsonRpc.AddLocalRpcTarget(target);
    }

    public ValueTask AddReceivedMessageAsync(string base64Message, CancellationToken ct)
    {
        return messageHandler.AddReceivedMessageAsync(base64Message, ct);
    }

    public void RegisterSendMessageAction(WriteBase64MessageActionAsync writeMessageActionAsync)
    {
        messageHandler.RegisterSendMessageAction(writeMessageActionAsync);
    }

    public void StartListening()
    {
        jsonRpc.StartListening();
    }

    public async ValueTask DisposeAsync()
    {
        jsonRpc.Dispose();
        await messageHandler.DisposeAsync();
    }

    [Obsolete("Call IAsyncDisposable.DisposeAsync instead.")]
    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    public T GetRemoteProxy<T>()
        where T : class
    {
        return jsonRpc.Attach<T>();
    }
}
