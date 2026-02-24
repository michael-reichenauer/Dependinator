using DependinatorRoslyn.Parsing;
using DependinatorRoslyn.Tests;

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
    public async Task TestSourceParserAsync()
    {
        var sourceParser = new SourceParser();
        if (!Try(out var allSourceNodes, out var e, await sourceParser.ParseProjectAsync(Root.ProjectFilePath)))
            Assert.Fail(e.AllErrorMessages());
        var sourceNodes = allSourceNodes
            .Where(sn => sn.Node is not null && sn.Node.Name.Contains(typeof(SourceTestData).FullName!))
            .ToList();
        Assert.NotEmpty(sourceNodes);
    }
}
