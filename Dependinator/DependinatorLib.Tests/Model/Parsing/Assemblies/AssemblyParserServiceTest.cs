using Dependinator.Model.Parsers.Assemblies;
using Dependinator.Model.Parsing;

namespace DependinatorLib.Tests.Model.Parsing.Assemblies;

public class AssemblyFileParserServiceTest
{
    [Fact]
    public async Task ParserTest()
    {
        var parser = new AssemblyParserService(null!);

        var nodes = new List<Node>();
        var links = new List<Link>();
        Assert.True(Try(await parser.ParseAsync(Path.Combine(AppContext.BaseDirectory, "DependinatorLib.dll"), nodes.Add, links.Add)));

        Assert.True(nodes.Any());
        Assert.True(links.Any());
    }
}
