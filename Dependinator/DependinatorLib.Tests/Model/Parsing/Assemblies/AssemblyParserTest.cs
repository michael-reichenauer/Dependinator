using Dependinator.Model.Parsing;
using Dependinator.Model.Parsing.Assemblies;


namespace DependinatorLib.Tests.Model.Parsing.Assemblies;

public class AssemblyParserTest
{
    [Fact]
    public async Task ParserTest()
    {
        var nodes = new List<Node>();
        var links = new List<Link>();

        var parser = new AssemblyParser(
            Path.Combine(AppContext.BaseDirectory, "DependinatorLib.dll"),
            "",
            "Dependinator",
            nodes.Add,
            links.Add,
            true);

        Assert.True(Try(await parser.ParseAsync()));
        Assert.True(nodes.Any());
        Assert.True(links.Any());
    }
}
