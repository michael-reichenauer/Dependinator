using Dependinator.Core.Shared;
using Dependinator.Reflection.Parsing.Solutions;
using Dependinator.Reflection.Tests.Parsing.Utils;

namespace Dependinator.Reflection.Tests.Parsing.Solutions;

public class SolutionParserTests
{
    [Fact]
    public async Task ParseAsync_ShouldParseSolution_WhenMicrosoftBuildRuntimeAssemblyIsNotInOutput()
    {
        var items = new ItemsMock();
        var parserFileService = new ParserFileService();
        string solutionPath = Root.SolutionFilePath;

        using var parser = new SolutionParser(solutionPath, items, false, parserFileService);
        if (!Try(out var e, await parser.ParseAsync()))
            Assert.Fail(e.AllErrorMessages());

        Assert.NotEmpty(items.Nodes);
    }
}
