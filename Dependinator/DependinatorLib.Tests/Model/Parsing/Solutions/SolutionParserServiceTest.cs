using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dependinator.Model.Parsing;
using Dependinator.Model.Parsing.Solutions;
using ICSharpCode.Decompiler.IL;

namespace DependinatorLib.Tests.Model.Parsing.Assemblies;

public class SolutionParserServiceTest
{
    [Fact]
    public async Task ParserTest()
    {
        var nodes = new List<Node>();
        var links = new List<Link>();

        var parser = new SolutionParserService();

        var path = GetSolutionPath();

        Assert.True(Try(await parser.ParseAsync(path, nodes.Add, links.Add)));
        Assert.True(nodes.Any());
        Assert.True(links.Any());
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