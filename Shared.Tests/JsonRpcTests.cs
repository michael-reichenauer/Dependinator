using System;
using System.Threading.Channels;
using StreamJsonRpc;

namespace Shared.Tests;

public interface ICalcAdd
{
    Task<int> AddAsync(int a, int b);
    Task<int> AddWitchCancelAsync(int a, int b, CancellationToken ct);
}

public sealed class CalcAddService(int extra) : ICalcAdd
{
    public Task<int> AddAsync(int a, int b) => Task.FromResult(a + b + extra);

    public async Task<int> AddWitchCancelAsync(int a, int b, CancellationToken ct)
    {
        try
        {
            await Task.Delay(5000, ct);
            return a + b + extra;
        }
        catch (OperationCanceledException)
        {
            return a + b + extra + 899;
        }
    }
}

public interface ICalcProd
{
    Task<int> MultiAsync(int a, int b);
}

public sealed class CalcProdService(int extra) : ICalcProd
{
    public Task<int> MultiAsync(int a, int b) => Task.FromResult(a * b + extra);
}

public sealed class JsonRpcPacketTransport(
    ChannelReader<ReadOnlyMemory<byte>> ReadChannel,
    ChannelWriter<ReadOnlyMemory<byte>> WriteChannel
) : IJsonRpcPacketTransport
{
    public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken ct) => ReadChannel.ReadAsync(ct);

    public ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct) =>
        WriteChannel.WriteAsync(payload, ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class JsonRpcTests : IAsyncDisposable
{
    readonly JsonRpcPacketMessageHandler clientPacketHandler;
    readonly JsonRpc rpcClient;
    readonly JsonRpcPacketMessageHandler serverPacketHandler;
    readonly JsonRpc rpcServer;

    public JsonRpcTests()
    {
        // Create buffered channnels to conect client and server transports
        var channel1 = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        var channel2 = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();

        IJsonRpcPacketTransport transport1 = new JsonRpcPacketTransport(channel1.Reader, channel2.Writer);
        IJsonRpcPacketTransport transport2 = new JsonRpcPacketTransport(channel2.Reader, channel1.Writer);

        serverPacketHandler = new JsonRpcPacketMessageHandler(transport1);
        rpcServer = new JsonRpc(serverPacketHandler);

        clientPacketHandler = new JsonRpcPacketMessageHandler(transport2);
        rpcClient = new JsonRpc(clientPacketHandler);
    }

    public async ValueTask DisposeAsync()
    {
        await clientPacketHandler.DisposeAsync();
        rpcClient.Dispose();
        await serverPacketHandler.DisposeAsync();
        rpcServer.Dispose();
    }

    [Fact]
    public async Task TestSimpleAsync()
    {
        rpcServer.AddLocalRpcTarget(new CalcAddService(0));
        rpcServer.StartListening();
        rpcClient.StartListening();

        ICalcAdd calcAddClient = rpcClient.Attach<ICalcAdd>();

        int sumClient = await calcAddClient.AddAsync(3, 8);
        Assert.Equal(3 + 8, sumClient);
    }

    [Fact]
    public async Task TestCancelAsync()
    {
        rpcServer.AddLocalRpcTarget(new CalcAddService(0));
        rpcServer.StartListening();
        rpcClient.StartListening();

        ICalcAdd calcAddClient = rpcClient.Attach<ICalcAdd>();

        // Check that cancellation token is forwarded to target and can be used there
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        CancellationToken ct = cts.Token;
        int sumClient = await calcAddClient.AddWitchCancelAsync(3, 8, ct);
        Assert.Equal(3 + 8 + 899, sumClient);
    }

    [Fact]
    public async Task TestMultipleDuplexAsync()
    {
        rpcServer.AddLocalRpcTarget(new CalcAddService(100));
        rpcServer.AddLocalRpcTarget(new CalcProdService(100));
        rpcServer.StartListening();

        rpcClient.AddLocalRpcTarget(new CalcAddService(200));
        rpcClient.AddLocalRpcTarget(new CalcProdService(200));
        rpcClient.StartListening();

        ICalcAdd calcAddClient = rpcClient.Attach<ICalcAdd>();
        ICalcProd calcProdClient = rpcClient.Attach<ICalcProd>();
        ICalcAdd calcAddServer = rpcServer.Attach<ICalcAdd>();
        ICalcProd calcProdServer = rpcServer.Attach<ICalcProd>();

        for (int i = 0; i < 1000; i++)
        {
            int sumClient = await calcAddClient.AddAsync(i, i);
            Assert.Equal(i + i + 100, sumClient);
            int prodClient = await calcProdClient.MultiAsync(i, i);
            Assert.Equal(i * i + 100, prodClient);

            int sumServer = await calcAddServer.AddAsync(i, i);
            Assert.Equal(i + i + 200, sumServer);
            int prodServer = await calcProdServer.MultiAsync(i, i);
            Assert.Equal(i * i + 200, prodServer);
        }
    }
}
