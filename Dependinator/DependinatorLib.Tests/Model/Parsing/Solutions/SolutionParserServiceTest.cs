using System.Runtime.CompilerServices;
using Dependinator.Model.Parsing;
using Dependinator.Model.Parsing.Solutions;

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

}