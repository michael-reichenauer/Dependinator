using DependinatorCore.Rpc;

namespace DependinatorCore.Tests.Rpc;

public interface ICalcAdd
{
    Task<int> AddAsync(int a, int b);
    Task<int> AddWitchCancelAsync(int a, int b, CancellationToken ct);
    Task<int> AddWithProgressAsync(int a, int b, IProgress<int> progress);
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

    public async Task<int> AddWithProgressAsync(int a, int b, IProgress<int> progress)
    {
        progress.Report(a);
        progress.Report(b);
        return a + b;
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

public class JsonRpcServiceTests
{
    [Fact]
    public async Task TestSimpleAsync()
    {
        using var jsonRpcService = new JsonRpcService();
        jsonRpcService.AddLocalRpcTarget(new CalcAddService(0));
        jsonRpcService.StartListening();

        ICalcAdd calcAdd = jsonRpcService.GetRemoteProxy<ICalcAdd>();

        int sum = await calcAdd.AddAsync(3, 8);
        Assert.Equal(3 + 8, sum);
    }

    [Fact]
    public async Task TestCancelAsync()
    {
        using var jsonRpcService = new JsonRpcService();
        jsonRpcService.AddLocalRpcTarget(new CalcAddService(0));
        jsonRpcService.StartListening();

        ICalcAdd calcAdd = jsonRpcService.GetRemoteProxy<ICalcAdd>();

        // Check that cancellation token is forwarded to target and can be used there
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        CancellationToken ct = cts.Token;
        int sum = await calcAdd.AddWitchCancelAsync(3, 8, ct);
        Assert.Equal(3 + 8 + 899, sum);
    }

    [Fact]
    public async Task TestProgressAsync()
    {
        using var jsonRpcService = new JsonRpcService();
        jsonRpcService.AddLocalRpcTarget(new CalcAddService(0));
        jsonRpcService.StartListening();

        ICalcAdd calcAdd = jsonRpcService.GetRemoteProxy<ICalcAdd>();

        List<int> progressResults = [];
        var progress = new Progress<int>(progressResults.Add);

        int sum = await calcAdd.AddWithProgressAsync(3, 8, progress);
        Assert.Equal(3 + 8, sum);
        Assert.Equal(2, progressResults.Count);
        Assert.Contains(3, progressResults);
        Assert.Contains(8, progressResults);
    }

    [Fact]
    public async Task TestMultipleDuplexAsync()
    {
        // Two json Rpc services on each side, connected to each other
        using var jsonRpcServiceA = new JsonRpcService();
        using var jsonRpcServiceB = new JsonRpcService();

        jsonRpcServiceA.RegisterSendMessageAction(jsonRpcServiceB.AddReceivedMessageAsync);
        jsonRpcServiceB.RegisterSendMessageAction(jsonRpcServiceA.AddReceivedMessageAsync);

        jsonRpcServiceA.AddLocalRpcTarget(new CalcAddService(200));
        jsonRpcServiceA.AddLocalRpcTarget(new CalcProdService(200));
        jsonRpcServiceA.StartListening();

        jsonRpcServiceB.AddLocalRpcTarget(new CalcAddService(100));
        jsonRpcServiceB.AddLocalRpcTarget(new CalcProdService(100));
        jsonRpcServiceB.StartListening();

        ICalcAdd calcAddA = jsonRpcServiceA.GetRemoteProxy<ICalcAdd>();
        ICalcProd calcProdA = jsonRpcServiceA.GetRemoteProxy<ICalcProd>();
        ICalcAdd calcAddB = jsonRpcServiceB.GetRemoteProxy<ICalcAdd>();
        ICalcProd calcProdB = jsonRpcServiceB.GetRemoteProxy<ICalcProd>();

        for (int i = 0; i < 1000; i++)
        {
            int sumA = await calcAddA.AddAsync(i, i);
            Assert.Equal(i + i + 100, sumA);
            int prodA = await calcProdA.MultiAsync(i, i);
            Assert.Equal(i * i + 100, prodA);

            int sumB = await calcAddB.AddAsync(i, i);
            Assert.Equal(i + i + 200, sumB);
            int prodB = await calcProdB.MultiAsync(i, i);
            Assert.Equal(i * i + 200, prodB);
        }
    }
}
