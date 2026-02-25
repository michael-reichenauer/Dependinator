using DependinatorRoslyn.Parsing;
using DependinatorRoslyn.Tests.Parsing.Utils;

namespace DependinatorRoslyn.Tests.Parsing;

// Some Type Comment
// Second Row
public class SourceTestData
{
    // Number Field Comment
    public int number;

    // First Function Comment
    public int FirstFunction(string name)
    {
        return name.Length;
    }

    public void SecondFunction() { }
}

public class SourceParserTests
{
    [Fact]
    public async Task TestProjectSourceParserAsync()
    {
        var sourceParser = new SourceParser();
        if (!Try(out var items, out var e, await sourceParser.ParseProjectAsync(Root.ProjectFilePath)))
            Assert.Fail(e.AllErrorMessages());

        var SourceTestDataNodes = items.NodesContained<SourceTestData>(null);
        Assert.NotEmpty(SourceTestDataNodes);
    }

    [Fact]
    public async Task TestSolutionSourceParserAsync()
    {
        var sourceParser = new SourceParser();
        if (!Try(out var items, out var e, await sourceParser.ParseSolutionAsync(Root.SolutionFilePath)))
            Assert.Fail(e.AllErrorMessages());

        var nodes = items.NodesContained(typeof(TypeParser), null);
        Assert.NotEmpty(nodes);

        var links = items.LinksToContained(typeof(TypeParser), null);
        Assert.NotEmpty(links);
    }
}
