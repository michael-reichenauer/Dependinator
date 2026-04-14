using Dependinator.Core.Parsing;
using Dependinator.Roslyn.Parsing;
using Dependinator.Roslyn.Tests.Parsing.Utils;

namespace Dependinator.Roslyn.Tests.Parsing;

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
    [Fact(Skip = "Disabled, since always LspProject takes extra time")]
    public async Task TestLspProjectGenericRegistrationLinksAsync()
    {
        var sourceParser = new SourceParser();
        var projectPath = Path.Combine(Root.SolutionFolderPath, "Dependinator.Lsp", "Dependinator.Lsp.csproj");

        if (!Try(out var items, out var e, await sourceParser.ParseProjectAsync(projectPath)))
            Assert.Fail(e.AllErrorMessages());

        var links = items.Links().Where(link => link.Source.Contains(".Dependinator.Lsp.Program.Main(")).ToList();

        var workspaceFolderServiceLink = links.SingleOrDefault(link =>
            link.Target.EndsWith(".Dependinator.Lsp.WorkspaceFolderService")
        );
        Assert.NotNull(workspaceFolderServiceLink);
        Assert.Equal(NodeType.Type, workspaceFolderServiceLink.Properties.TargetType);

        var workspaceFolderChangeHandlerLink = links.SingleOrDefault(link =>
            link.Target.EndsWith(".Dependinator.Lsp.WorkspaceFolderChangeHandler")
        );
        Assert.NotNull(workspaceFolderChangeHandlerLink);
        Assert.Equal(NodeType.Type, workspaceFolderChangeHandlerLink.Properties.TargetType);
    }

    [Fact(Skip = "Disabled, since always parsing project takes extra time")]
    public async Task TestProjectSourceParserAsync()
    {
        var sourceParser = new SourceParser();
        if (!Try(out var items, out var e, await sourceParser.ParseProjectAsync(Root.ProjectFilePath)))
            Assert.Fail(e.AllErrorMessages());

        var SourceTestDataNodes = items.NodesContained<SourceTestData>(null);
        Assert.NotEmpty(SourceTestDataNodes);
    }

    [Fact(Skip = "Disabled, since always parsing whole solution takes time")]
    //[Fact]
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
