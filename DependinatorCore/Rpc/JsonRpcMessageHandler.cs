using System.Buffers;
using System.Threading.Channels;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace DependinatorCore.Rpc;

public delegate ValueTask WriteBinaryMessageActionAsync(ReadOnlyMemory<byte> binaryMessage, CancellationToken ct);
public delegate ValueTask WriteBase64MessageActionAsync(string base64Message, CancellationToken ct);

public sealed class JsonRpcMessageHandler : MessageHandlerBase
{
    readonly SemaphoreSlim writeGate = new(1, 1);
    readonly Channel<ReadOnlyMemory<byte>> messagesChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    WriteBinaryMessageActionAsync sendBinaryMessageActionAsync = null!;

    public JsonRpcMessageHandler()
        //       : base(new StreamJsonRpc.MessagePackFormatter()) { } // More efficient
        : base(new StreamJsonRpc.SystemTextJsonFormatter()) { }

    public override bool CanRead => true;
    public override bool CanWrite => true;

    // Registers the write message action, that should transfer the message to the "other" side
    public void RegisterSendMessageAction(WriteBinaryMessageActionAsync sendBinaryMessageActionAsync)
    {
        if (sendBinaryMessageActionAsync is null)
            throw new InvalidOperationException($"{nameof(sendBinaryMessageActionAsync)} cannot be null");

        this.sendBinaryMessageActionAsync = sendBinaryMessageActionAsync;
    }

    // Registers the write message action, that should transfer the message to the "other" side
    public void RegisterSendMessageAction(WriteBase64MessageActionAsync sendBase64MessageActionAsync)
    {
        if (sendBase64MessageActionAsync is null)
            throw new InvalidOperationException($"{nameof(sendBase64MessageActionAsync)} cannot be null");

        this.sendBinaryMessageActionAsync = async (binaryMessage, ct) =>
        {
            var base64Message = Convert.ToBase64String(binaryMessage.ToArray());
            await sendBase64MessageActionAsync(base64Message, ct).ConfigureAwait(false);
        };
    }

    // Adds the message, that was received from the "other" side
    public ValueTask AddReceivedMessageAsync(ReadOnlyMemory<byte> binaryPackage, CancellationToken ct)
    {
        return messagesChannel.Writer.WriteAsync(binaryPackage, ct);
    }

    // Adds the message, that was received fromt the "other" side
    public ValueTask AddReceivedMessageAsync(string base64Message, CancellationToken ct)
    {
        var binaryPackage = Convert.FromBase64String(base64Message);
        return AddReceivedMessageAsync(binaryPackage, ct);
    }

    protected override async ValueTask<JsonRpcMessage?> ReadCoreAsync(CancellationToken ct)
    {
        var payload = await messagesChannel.Reader.ReadAsync(ct).ConfigureAwait(false);
        Log.Info($"Read message {payload.Length} bytes");

        // Formatter expects a ReadOnlySequence<byte>.
        var seq = new ReadOnlySequence<byte>(payload);
        var rpcMessage = this.Formatter.Deserialize(seq);

        return rpcMessage;
    }

    protected override async ValueTask WriteCoreAsync(JsonRpcMessage rpcMessage, CancellationToken ct)
    {
        if (sendBinaryMessageActionAsync is null)
            throw new InvalidOperationException($"{nameof(RegisterSendMessageAction)} has not yet been called");

        // Serialize message into a buffer.
        var buffer = new ArrayBufferWriter<byte>(initialCapacity: 1024);
        this.Formatter.Serialize(buffer, rpcMessage);

        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Log.Info($"Write message {buffer.WrittenMemory.Length} bytes");
            await sendBinaryMessageActionAsync(buffer.WrittenMemory, ct).ConfigureAwait(false);
        }
        finally
        {
            writeGate.Release();
        }
    }

    protected override ValueTask FlushAsync(CancellationToken ct) => default;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            writeGate.Dispose();
        }
        base.Dispose(disposing);
    }
}
