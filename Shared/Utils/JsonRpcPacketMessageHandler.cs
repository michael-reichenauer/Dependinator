using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace Dependinator.Shared.Utils;

public sealed class JsonRpcPacketMessageHandler : MessageHandlerBase
{
    private readonly IJsonRpcPacketTransport transport;

    private readonly SemaphoreSlim writeGate = new(1, 1);

    public JsonRpcPacketMessageHandler(IJsonRpcPacketTransport transport)
        : base(new StreamJsonRpc.MessagePackFormatter())
    {
        this.transport = transport;
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;

    protected override async ValueTask<JsonRpcMessage?> ReadCoreAsync(CancellationToken ct)
    {
        var payload = await transport.ReceiveAsync(ct).ConfigureAwait(false);

        // Formatter expects a ReadOnlySequence<byte>.
        var seq = new ReadOnlySequence<byte>(payload);
        return this.Formatter.Deserialize(seq);
    }

    protected override async ValueTask WriteCoreAsync(JsonRpcMessage content, CancellationToken ct)
    {
        // Serialize message into a buffer.
        var buffer = new ArrayBufferWriter<byte>(initialCapacity: 1024);
        this.Formatter.Serialize(buffer, content);

        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await transport.SendAsync(buffer.WrittenMemory, ct).ConfigureAwait(false);
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
