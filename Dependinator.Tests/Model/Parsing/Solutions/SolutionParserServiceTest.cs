using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dependinator.Parsing;
using Dependinator.Parsing.Solutions;


namespace Dependinator.Tests.Model.Parsing.Assemblies;

public class SolutionParserServiceTest
{
    [Fact]
    public async Task ParserTest()
    {
        var parser = new SolutionParserService();

        var path = GetSolutionPath();

        var channel = Channel.CreateUnbounded<IItem>();
        Assert.True(Try(await parser.ParseAsync(path, channel.Writer)));
        var list = await channel.Reader.ReadAllAsync().ToList();
        Assert.True(list.Any());
    }


    static string GetSolutionPath([CallerFilePath] string sourceFilePath = "")
    {
        return Path.Combine(
            Path.GetDirectoryName(
            Path.GetDirectoryName(
            Path.GetDirectoryName(
            Path.GetDirectoryName(
            Path.GetDirectoryName(sourceFilePath)))))!, "Dependinator.sln");
    }

    [Fact]
    public async Task Test()
    {
        var x = Math.Pow(1 / 7.0, 2);

        int i = 0;
        await foreach (var item in Scan())
        {
            Assert.Equal($"{i++}", item);
        }

        Assert.Equal(100, i);
    }

    IAsyncEnumerable<string> Scan()
    {
        var channel = Channel.CreateBounded<string>(10);
        Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                await channel.Writer.WriteAsync($"{i}");
            }
            channel.Writer.Complete();
        }).RunInBackground();

        return channel.Reader.ReadAllAsync();
    }

    [Fact]
    public async Task Test2()
    {
        int i = 0;
        var reader = Scan2();
        while (await reader.WaitToReadAsync())
        {
            for (int j = 0; j < 1000; j++)
            {
                if (!reader.TryRead(out string? item)) break;
                Assert.Equal($"{i++}", item);
                Console.WriteLine(item);
            }
        }

        Assert.Equal(100, i);
    }

    ChannelReader<string> Scan2()
    {
        var channel = Channel.CreateBounded<string>(10);
        Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                await channel.Writer.WriteAsync($"{i}");
            }
            channel.Writer.Complete();
        }).RunInBackground();

        return channel.Reader;
    }


}