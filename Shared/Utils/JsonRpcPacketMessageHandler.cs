using System.Buffers;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace Dependinator.Shared.Utils;

public interface IJsonRpcPacketWriter
{
    ValueTask WritePackageAsync(ReadOnlyMemory<byte> payload, CancellationToken ct);
}

public delegate ValueTask WritePackageActionAsync(ReadOnlyMemory<byte> payload, CancellationToken ct);

public sealed class JsonRpcPacketMessageHandler : MessageHandlerBase, IJsonRpcPacketWriter
{
    readonly SemaphoreSlim writeGate = new(1, 1);
    readonly Channel<ReadOnlyMemory<byte>> packageChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    WritePackageActionAsync writePackageActionAsync = null!;

    public JsonRpcPacketMessageHandler()
        : base(new StreamJsonRpc.MessagePackFormatter()) { }

    public override bool CanRead => true;
    public override bool CanWrite => true;

    public void SetWritePackageAction(WritePackageActionAsync writePackageActionAsync)
    {
        if (writePackageActionAsync is null)
            throw new InvalidOperationException($"{nameof(writePackageActionAsync)} cannot be null");

        this.writePackageActionAsync = writePackageActionAsync;
    }

    public ValueTask WritePackageAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        return packageChannel.Writer.WriteAsync(payload, ct);
    }

    protected override async ValueTask<JsonRpcMessage?> ReadCoreAsync(CancellationToken ct)
    {
        var payload = await packageChannel.Reader.ReadAsync(ct).ConfigureAwait(false);

        // Formatter expects a ReadOnlySequence<byte>.
        var seq = new ReadOnlySequence<byte>(payload);
        return this.Formatter.Deserialize(seq);
    }

    protected override async ValueTask WriteCoreAsync(JsonRpcMessage content, CancellationToken ct)
    {
        if (writePackageActionAsync is null)
            throw new InvalidOperationException($"{nameof(SetWritePackageAction)} has not yet been called");

        // Serialize message into a buffer.
        var buffer = new ArrayBufferWriter<byte>(initialCapacity: 1024);
        this.Formatter.Serialize(buffer, content);

        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await writePackageActionAsync(buffer.WrittenMemory, ct).ConfigureAwait(false);
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
