using DependinatorCore.Parsing.Solutions;
using DependinatorCore.Shared;

namespace DependinatorCore.Tests.Parsing.Solutions;

public class SolutionParserTests
{
    [Fact]
    public async Task ParseAsync_ShouldParseSolution_WhenMicrosoftBuildRuntimeAssemblyIsNotInOutput()
    {
        var items = new Parsing.Utils.ItemsMock();
        var parserFileService = new ParserFileService(new EmbeddedResources<ParserFileService>());

        using var parser = new SolutionParser("/workspaces/Dependinator/Dependinator.sln", items, false, parserFileService);
        if (!Try(out var e, await parser.ParseAsync()))
            Assert.Fail(e.AllErrorMessages());

        Assert.NotEmpty(items.Nodes);
    }
}
