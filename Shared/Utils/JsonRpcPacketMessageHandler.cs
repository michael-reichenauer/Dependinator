using System.Buffers;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace Dependinator.Shared.Utils;

public interface IJsonRpcPacketWriter
{
    ValueTask WriteBinaryPackageAsync(ReadOnlyMemory<byte> binaryPayload, CancellationToken ct);
    ValueTask WriteStringPackageAsync(string stringPayload, CancellationToken ct);
    void SetWriteBinaryPackageAction(WriteBinaryPackageActionAsync writePackageActionAsync);
    void SetWriteStringPackageAction(WriteStringPackageActionAsync writeMessageActionAsync);
}

public delegate ValueTask WriteBinaryPackageActionAsync(ReadOnlyMemory<byte> payload, CancellationToken ct);
public delegate ValueTask WriteStringPackageActionAsync(string message, CancellationToken ct);

public sealed class JsonRpcPacketMessageHandler : MessageHandlerBase, IJsonRpcPacketWriter
{
    readonly SemaphoreSlim writeGate = new(1, 1);
    readonly Channel<ReadOnlyMemory<byte>> packageChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    WriteBinaryPackageActionAsync writeBinaryPackageActionAsync = null!;
    WriteStringPackageActionAsync writeStringPackageActionAsync = null!;

    public JsonRpcPacketMessageHandler()
        //       : base(new StreamJsonRpc.MessagePackFormatter()) { }
        : base(new StreamJsonRpc.SystemTextJsonFormatter()) { }

    public override bool CanRead => true;
    public override bool CanWrite => true;

    public void SetWriteBinaryPackageAction(WriteBinaryPackageActionAsync writePackageActionAsync)
    {
        if (writePackageActionAsync is null)
            throw new InvalidOperationException($"{nameof(writePackageActionAsync)} cannot be null");

        this.writeBinaryPackageActionAsync = writePackageActionAsync;
    }

    public void SetWriteStringPackageAction(WriteStringPackageActionAsync WriteStringPackageActionAsync)
    {
        if (WriteStringPackageActionAsync is null)
            throw new InvalidOperationException($"{nameof(WriteStringPackageActionAsync)} cannot be null");

        this.writeStringPackageActionAsync = WriteStringPackageActionAsync;
    }

    public ValueTask WriteBinaryPackageAsync(ReadOnlyMemory<byte> binaryPackage, CancellationToken ct)
    {
        return packageChannel.Writer.WriteAsync(binaryPackage, ct);
    }

    public ValueTask WriteStringPackageAsync(string stringPackage, CancellationToken ct)
    {
        var payload = Convert.FromBase64String(stringPackage);
        return packageChannel.Writer.WriteAsync(payload, ct);
    }

    protected override async ValueTask<JsonRpcMessage?> ReadCoreAsync(CancellationToken ct)
    {
        var payload = await packageChannel.Reader.ReadAsync(ct).ConfigureAwait(false);

        // Formatter expects a ReadOnlySequence<byte>.
        var seq = new ReadOnlySequence<byte>(payload);
        var rpcMessage = this.Formatter.Deserialize(seq);

        return rpcMessage;
    }

    protected override async ValueTask WriteCoreAsync(JsonRpcMessage rpcMessage, CancellationToken ct)
    {
        if (writeBinaryPackageActionAsync is null && writeStringPackageActionAsync is null)
            throw new InvalidOperationException(
                $"{nameof(SetWriteStringPackageAction)} or {writeStringPackageActionAsync} has not yet been called"
            );

        // Serialize message into a buffer.
        var buffer = new ArrayBufferWriter<byte>(initialCapacity: 1024);
        this.Formatter.Serialize(buffer, rpcMessage);

        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (writeBinaryPackageActionAsync is not null)
            {
                await writeBinaryPackageActionAsync(buffer.WrittenMemory, ct).ConfigureAwait(false);
            }
            else
            {
                var base64Message = Convert.ToBase64String(buffer.WrittenMemory.ToArray());
                await writeStringPackageActionAsync(base64Message, ct).ConfigureAwait(false);
            }
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
