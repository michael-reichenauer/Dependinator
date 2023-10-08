using Dependinator.Model.Parsers.Assemblies;
using Dependinator.Model.Parsing;


namespace DependinatorLib.Tests.Model.Parsing.Assemblies;

public class AssemblyParserTest
{
    [Fact]
    public async Task ParserTest()
    {
        List<NodeData> nodes = new List<NodeData>();
        List<LinkData> links = new List<LinkData>();

        AssemblyParser parser = new AssemblyParser(
            Path.Combine(AppContext.BaseDirectory, "DependinatorLib.dll"),
            "",
            "Dependinator",
            node => nodes.Add(node),
            link => links.Add(link),
            true);

        var r = await parser.ParseAsync();

        Assert.True(nodes.Any());
        Assert.True(links.Any());
    }
}
