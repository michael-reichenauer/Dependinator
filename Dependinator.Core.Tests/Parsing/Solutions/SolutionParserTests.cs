using Dependinator.Core.Parsing.Solutions;
using Dependinator.Core.Shared;

namespace Dependinator.Core.Tests.Parsing.Solutions;

public class SolutionParserTests
{
    [Fact]
    public async Task ParseAsync_ShouldParseSolution_WhenMicrosoftBuildRuntimeAssemblyIsNotInOutput()
    {
        var items = new Parsing.Utils.ItemsMock();
        var parserFileService = new ParserFileService();
        string solutionPath = Root.SolutionFilePath;

        using var parser = new SolutionParser(solutionPath, items, false, parserFileService);
        if (!Try(out var e, await parser.ParseAsync()))
            Assert.Fail(e.AllErrorMessages());

        Assert.NotEmpty(items.Nodes);
    }
}
