using System;
using System.Threading.Channels;
using StreamJsonRpc;

namespace Shared.Tests;

public interface ICalc
{
    Task<int> AddAsync(int a, int b);
}

public sealed class CalcService : ICalc
{
    public Task<int> AddAsync(int a, int b) => Task.FromResult(a + b);
}

public sealed class PacketTransport(
    ChannelReader<ReadOnlyMemory<byte>> ReadChannel,
    ChannelWriter<ReadOnlyMemory<byte>> WriteChannel
) : IPacketTransport
{
    public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken ct) => ReadChannel.ReadAsync(ct);

    public ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct) =>
        WriteChannel.WriteAsync(payload, ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class JsonRpcTests
{
    [Fact]
    public async Task TestAsync()
    {
        // Create buffered channnels between client and server
        var channel1 = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
            new UnboundedChannelOptions { SingleWriter = true, SingleReader = true }
        );
        var channel2 = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
            new UnboundedChannelOptions { SingleWriter = true, SingleReader = true }
        );

        IPacketTransport transport1 = new PacketTransport(channel1.Reader, channel2.Writer);
        IPacketTransport transport2 = new PacketTransport(channel2.Reader, channel1.Writer);

        using var handlerServer = new PacketMessageHandler(transport1);
        using var rpcServer = new JsonRpc(handlerServer, new CalcService());
        rpcServer.StartListening();

        using var handlerClient = new PacketMessageHandler(transport2);
        using var rpcClient = new JsonRpc(handlerClient);
        rpcClient.StartListening();

        ICalc calc = rpcClient.Attach<ICalc>();

        for (int i = 0; i < 1000; i++)
        {
            int sum = await calc.AddAsync(i, i);
            Assert.Equal(i + i, sum);
        }
    }
}
