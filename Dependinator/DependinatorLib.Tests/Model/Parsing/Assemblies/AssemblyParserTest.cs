using System.Threading.Channels;
using Dependinator.Parsing;
using Dependinator.Parsing.Assemblies;

namespace DependinatorLib.Tests.Model.Parsing.Assemblies;

public class AssemblyParserTest
{
    [Fact]
    public async Task ParserTest()
    {
        var channel = Channel.CreateUnbounded<IItem>();
        var parser = new AssemblyParser(
            Path.Combine(AppContext.BaseDirectory, "DependinatorLib.dll"),
            "",
            "Dependinator",
            channel.Writer,
            true);

        Assert.True(Try(await parser.ParseAsync()));
        var list = await channel.Reader.ReadAllAsync().ToList();
        Assert.True(list.Any());
    }
}
