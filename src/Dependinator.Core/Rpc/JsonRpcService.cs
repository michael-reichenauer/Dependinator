using System.Diagnostics;
using StreamJsonRpc;

// JSON-RPC transport (built on StreamJsonRpc) used to communicate between hosts, such as
// the LSP server and the web UI. Handles message framing, base64 packaging, and generating
// local targets and remote proxies for the [Rpc]-annotated service interfaces.
namespace Dependinator.Core.Rpc;

public interface IJsonRpcService
{
    // Registers the send message action, that should send the package to the "other" side
    void RegisterSendMessageAction(SendBase64MessageAsync sendMessageAsync);

    // Adds the message, that was received from the "other" side
    ValueTask AddReceivedMessageAsync(string base64Message, CancellationToken ct);

    void AddLocalRpcTarget<TInterface>(TInterface target)
        where TInterface : class;

    void AddLocalRpcTarget(Type type, object target);

    T GetRemoteProxy<T>()
        where T : class;

    object GetRemoteProxy(Type type);

    void StartListening();

    Task CheckConnectionAsync(TimeSpan timeout);
}

[Singleton]
public class JsonRpcService : IJsonRpcService, IAsyncDisposable, IDisposable
{
    readonly JsonRpcMessageHandler messageHandler = new();
    readonly JsonRpc jsonRpc;

    public JsonRpcService()
    {
        jsonRpc = new JsonRpc(messageHandler);

        // Add support for checking remote connection
        AddLocalRpcTarget<IJsonRpcConnectionCheckService>(new JsonRpcConnectionCheckService());

        // By default, this message handler is "self" connected (sent messages loop back to this
        // same service), which is how hosts without a remote side run. Hosts with a remote side
        // use RegisterSendMessageAction to route messages to the other side instead. The loopback
        // uses the binary overloads to skip the base64 encoding/decoding roundtrip.
        messageHandler.RegisterSendMessageAction((SendBinaryMessageAsync)messageHandler.AddReceivedMessageAsync);
    }

    public void AddLocalRpcTarget<TInterface>(TInterface target)
        where TInterface : class
    {
        AddLocalRpcTarget(typeof(TInterface), target);
    }

    public void AddLocalRpcTarget(Type interfaceType, object target)
    {
        Log.Info("Add local RPC target:", interfaceType.FullName, target.GetType().FullName);

        if (!interfaceType.IsInterface)
            throw new ArgumentException("RPC target type must be an interface.", nameof(interfaceType));

        var options = new JsonRpcTargetOptions { MethodNameTransform = name => RpcMethodName(interfaceType, name) };

        jsonRpc.AddLocalRpcTarget(interfaceType, target, options);
    }

    public ValueTask AddReceivedMessageAsync(string base64Message, CancellationToken ct)
    {
        return messageHandler.AddReceivedMessageAsync(base64Message, ct);
    }

    public void RegisterSendMessageAction(SendBase64MessageAsync sendMessageAsync)
    {
        messageHandler.RegisterSendMessageAction(sendMessageAsync);
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
        return (T)GetRemoteProxy(typeof(T));
    }

    public object GetRemoteProxy(Type interfaceType)
    {
        Log.Info("Get remote proxy:", interfaceType.FullName);
        if (!interfaceType.IsInterface)
            throw new ArgumentException("RPC type must be an interface.", nameof(interfaceType));

        var options = new JsonRpcProxyOptions { MethodNameTransform = name => RpcMethodName(interfaceType, name) };

        return jsonRpc.Attach(interfaceType, options);
    }

    // Local targets and remote proxies must use the same method name convention, so calls on a
    // proxy for an interface reach the target registered for that same interface on the other side.
    static string RpcMethodName(Type interfaceType, string methodName) => $"{interfaceType.FullName}.{methodName}";

    public async Task CheckConnectionAsync(TimeSpan timeout)
    {
        var connectionCheckService = GetRemoteProxy<IJsonRpcConnectionCheckService>();

        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < timeout)
        {
            var id = Guid.NewGuid().ToString();

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            var ct = cts.Token;
            try
            {
                // Need to use "WithCancellation" since JsonRpc does not support request cancellation properly
                var result = await connectionCheckService.CheckConnectionAsync(id).WithCancellation(ct);
                if (result == id)
                    return;
            }
            catch
            {
                // Ignoring error, lets retry again
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Failed to connect using RPC to other side");
    }

    interface IJsonRpcConnectionCheckService
    {
        Task<string> CheckConnectionAsync(string id);
    }

    class JsonRpcConnectionCheckService : IJsonRpcConnectionCheckService
    {
        public Task<string> CheckConnectionAsync(string id) => Task.FromResult(id);
    }
}
