using System.Text;
using Dependinator.Core.Rpc;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace Dependinator.Core.Tests.Rpc;

public interface ICalcAdd
{
    Task<int> AddAsync(int a, int b);
    Task<string> AddStringsAsync(string a, string b);
    Task<int> AddWitchCancelAsync(int a, int b, CancellationToken ct);
    Task<int> AddWithProgressAsync(int a, int b, IProgress<int> progress);
    Task<int> AddWitchExceptionAsync(int a, int b);
}

public interface IMiniCalcAdd
{
    Task<int> AddAsync(int a, int b);
}

public sealed class CalcAddService(int extra) : ICalcAdd
{
    public Task<int> AddAsync(int a, int b) => Task.FromResult(a + b + extra);

    public Task<string> AddStringsAsync(string a, string b) => Task.FromResult(a + b + extra);

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

    public async Task<int> AddWitchExceptionAsync(int a, int b)
    {
        throw new ArgumentException($"{a} + {b} not supported");
    }
}

public sealed class MiniCalcAddService(int extra) : IMiniCalcAdd
{
    public Task<int> AddAsync(int a, int b) => Task.FromResult(a + b + extra);
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
    // public JsonRpcServiceTests(ITestOutputHelper output)
    // {
    //     ConfigLogger.Configure(
    //         new HostLoggingSettings(
    //             EnableFileLog: false,
    //             EnableConsoleLog: false,
    //             LogFilePath: null,
    //             Output: line => output.WriteLine(line)
    //         )
    //     );
    // }

    [Fact]
    public async Task TestCallAsync()
    {
        using var jsonRpcService = new JsonRpcService();
        jsonRpcService.AddLocalRpcTarget<ICalcAdd>(new CalcAddService(0));
        jsonRpcService.StartListening();
        ICalcAdd calcAdd = jsonRpcService.GetRemoteProxy<ICalcAdd>();

        int sum = await calcAdd.AddAsync(3, 8);
        Assert.Equal(3 + 8, sum);
    }

    [Fact]
    public async Task TestLargeCallAsync()
    {
        using var jsonRpcService = new JsonRpcService();
        jsonRpcService.AddLocalRpcTarget<ICalcAdd>(new CalcAddService(0));
        jsonRpcService.StartListening();

        ICalcAdd calcAdd = jsonRpcService.GetRemoteProxy<ICalcAdd>();
        var aBuilder = new StringBuilder();
        for (var i = 0; i < 100_000; i++)
            aBuilder.Append("abcdefghijklmnopqrst");
        var a = aBuilder.ToString();

        string sum = await calcAdd.AddStringsAsync(a, a);
        Assert.Equal(a + a + 0, sum);
    }

    [Fact]
    public async Task TestCancelAsync()
    {
        using var jsonRpcService = new JsonRpcService();
        jsonRpcService.AddLocalRpcTarget<ICalcAdd>(new CalcAddService(0));
        jsonRpcService.StartListening();

        ICalcAdd calcAdd = jsonRpcService.GetRemoteProxy<ICalcAdd>();

        // Check that cancellation token is forwarded to target and can be used there
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));
        CancellationToken ct = cts.Token;
        int sum = await calcAdd.AddWitchCancelAsync(3, 8, ct);
        Assert.Equal(3 + 8 + 899, sum);
    }

    [Fact]
    public async Task TestProgressAsync()
    {
        using var jsonRpcService = new JsonRpcService();
        jsonRpcService.AddLocalRpcTarget<ICalcAdd>(new CalcAddService(0));
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
    public async Task TestExceptionCallAsync()
    {
        using var jsonRpcService = new JsonRpcService();
        jsonRpcService.AddLocalRpcTarget<ICalcAdd>(new CalcAddService(0));
        jsonRpcService.StartListening();
        ICalcAdd calcAdd = jsonRpcService.GetRemoteProxy<ICalcAdd>();

        var exception = await Assert.ThrowsAsync<RemoteInvocationException>(() => calcAdd.AddWitchExceptionAsync(3, 8));
        Assert.Equal("3 + 8 not supported", exception.Message);
        Assert.Equal("System.ArgumentException", ((CommonErrorData)exception.DeserializedErrorData!).TypeName);
    }

    [Fact]
    public async Task TestMultipleDuplexAsync()
    {
        // Two json Rpc services on each side, connected to each other
        using var jsonRpcServiceA = new JsonRpcService();
        using var jsonRpcServiceB = new JsonRpcService();

        jsonRpcServiceA.RegisterSendMessageAction(jsonRpcServiceB.AddReceivedMessageAsync);
        jsonRpcServiceB.RegisterSendMessageAction(jsonRpcServiceA.AddReceivedMessageAsync);

        jsonRpcServiceA.AddLocalRpcTarget<ICalcAdd>(new CalcAddService(100));
        jsonRpcServiceA.AddLocalRpcTarget<ICalcProd>(new CalcProdService(200));
        jsonRpcServiceA.StartListening();

        jsonRpcServiceB.AddLocalRpcTarget<ICalcAdd>(new CalcAddService(330));
        jsonRpcServiceB.AddLocalRpcTarget<ICalcProd>(new CalcProdService(430));
        jsonRpcServiceB.StartListening();

        await jsonRpcServiceA.CheckConnectionAsync(TimeSpan.FromSeconds(1));
        await jsonRpcServiceB.CheckConnectionAsync(TimeSpan.FromSeconds(1));

        ICalcAdd calcAddA = jsonRpcServiceA.GetRemoteProxy<ICalcAdd>(); // A->B
        ICalcProd calcProdA = jsonRpcServiceA.GetRemoteProxy<ICalcProd>(); // A->B

        ICalcAdd calcAddB = jsonRpcServiceB.GetRemoteProxy<ICalcAdd>(); // B->A
        ICalcProd calcProdB = jsonRpcServiceB.GetRemoteProxy<ICalcProd>(); // B->A

        for (int i = 0; i < 1000; i++)
        {
            int sumA = await calcAddA.AddAsync(i, i);
            Assert.Equal(i + i + 330, sumA);
            int prodA = await calcProdA.MultiAsync(i, i);
            Assert.Equal(i * i + 430, prodA);

            int sumB = await calcAddB.AddAsync(i, i);
            Assert.Equal(i + i + 100, sumB);
            int prodB = await calcProdB.MultiAsync(i, i);
            Assert.Equal(i * i + 200, prodB);
        }
    }

    [Fact]
    public async Task TestSameFunctionNameDifferentTypesCallAsync()
    {
        // Two different services, but same function name
        using var jsonRpcService = new JsonRpcService();
        jsonRpcService.AddLocalRpcTarget(typeof(ICalcAdd), new CalcAddService(100));
        jsonRpcService.AddLocalRpcTarget(typeof(IMiniCalcAdd), new MiniCalcAddService(200));
        jsonRpcService.StartListening();

        ICalcAdd calcAdd = jsonRpcService.GetRemoteProxy<ICalcAdd>();
        IMiniCalcAdd miniCalcAdd = jsonRpcService.GetRemoteProxy<IMiniCalcAdd>();

        int sum = await calcAdd.AddAsync(3, 8);
        Assert.Equal(3 + 8 + 100, sum);

        int sumMini = await miniCalcAdd.AddAsync(3, 8);
        Assert.Equal(3 + 8 + 200, sumMini);
    }

    [Fact]
    public async Task TestConnectionCheckAsync()
    {
        using var jsonRpcService = new JsonRpcService();
        jsonRpcService.StartListening();
        await jsonRpcService.CheckConnectionAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TestConnectionCheckTimeoutAsync()
    {
        using var jsonRpcService = new JsonRpcService();

        // Ensure connection cannot occur
        jsonRpcService.RegisterSendMessageAction((_, __) => ValueTask.CompletedTask);

        // Check that CheckConnectionAsync will timeout
        jsonRpcService.StartListening();
        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            jsonRpcService.CheckConnectionAsync(TimeSpan.FromSeconds(1))
        );

        // Now enable communication anc check again
        jsonRpcService.RegisterSendMessageAction(jsonRpcService.AddReceivedMessageAsync);
        await jsonRpcService.CheckConnectionAsync(TimeSpan.FromSeconds(1));
    }
}
