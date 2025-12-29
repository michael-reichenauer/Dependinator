namespace Dependinator.Shared.Utils;

public interface IJsonRpcPacketTransport : IAsyncDisposable
{
    ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
    ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken);
}
