using DependinatorCore.Parsing.Sources;

namespace DependinatorCore.Tests.Parsing.Sources;

public class SourceParserTests
{
    [Fact]
    public async Task TestAsync()
    {
        var slnPath = "/workspaces/Dependinator/Dependinator.sln";

        var sourceParser = new SourceParser();
        if (!Try(out var nodes, out var e, await sourceParser.GetNodesAsync(slnPath)))
            Assert.Fail(e.AllErrorMessages());

        var x = nodes;
    }
}
