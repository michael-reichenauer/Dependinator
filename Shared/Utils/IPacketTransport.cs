using System;

namespace Dependinator.Shared.Utils;

public interface IPacketTransport : IAsyncDisposable
{
    ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
    ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken);
}
