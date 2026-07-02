using Dependinator.Core.Parsing;
using Dependinator.Core.Shared;
using Dependinator.Roslyn.Parsing;
using Dependinator.Roslyn.Tests.Parsing.Utils;
using Microsoft.CodeAnalysis.CSharp;

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

[Collection(nameof(RoslynCollection))]
public class SourceParserTests(RoslynFixture fixture)
{
    [Fact]
    public void GetAssemblyDescription_ShouldReturnAttributeValueAndItsSourceLocation()
    {
        var (description, fileSpan) = SourceParser.GetAssemblyDescription(fixture.Compilation, Root.ProjectFilePath);

        Assert.Equal("Test assembly for Roslyn parsing.", description);
        Assert.NotNull(fileSpan);
        Assert.EndsWith("Usings.cs", fileSpan.Path);
        Assert.Equal(5, fileSpan.StartLine); // 0-based line of the AssemblyDescription attribute
    }

    [Fact]
    public void GetAssemblyDescription_ShouldFallBackToUsingsFile_WhenNoAttribute()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText("namespace Sample;", path: "/repo/src/Sample/Usings.cs");
        var compilation = CSharpCompilation.Create("Sample", [syntaxTree]);

        var (description, fileSpan) = SourceParser.GetAssemblyDescription(compilation, null);

        Assert.Null(description);
        Assert.NotNull(fileSpan);
        Assert.Equal("/repo/src/Sample/Usings.cs", fileSpan.Path);
        Assert.Equal(0, fileSpan.StartLine);
    }

    [Fact]
    public void GetAssemblyDescription_ShouldFallBackToProjectFile_WhenNoSuitableSourceFile()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), $"Sample-{Guid.NewGuid()}.csproj");
        File.WriteAllText(
            projectPath,
            "<Project>\n  <PropertyGroup>\n    <Description>Sample</Description>\n  </PropertyGroup>\n</Project>\n"
        );
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText("namespace Sample;", path: "/repo/src/Sample/Program.cs");
            var compilation = CSharpCompilation.Create("Sample", [syntaxTree]);

            var (description, fileSpan) = SourceParser.GetAssemblyDescription(compilation, projectPath);

            Assert.Null(description);
            Assert.NotNull(fileSpan);
            Assert.Equal(projectPath, fileSpan.Path);
            Assert.Equal(2, fileSpan.StartLine); // 0-based line of the <Description> element
        }
        finally
        {
            File.Delete(projectPath);
        }
    }

    [Fact(Skip = "Disabled, since always LspProject takes extra time")]
    public async Task TestLspProjectGenericRegistrationLinksAsync()
    {
        var sourceParser = new SourceParser();
        var projectPath = Path.Combine(Root.SrcFolderPath, "Dependinator.Lsp", "Dependinator.Lsp.csproj");

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

    [Fact]
    public async Task ParseSolutionAsync_ShouldLoadEmbeddedDemoModel_ForDemoSolutionPath()
    {
        var sourceParser = new SourceParser();

        // "/Demo.sln" is resolved from the embedded pre-parsed demo model (no Roslyn parse),
        // which is what UI/e2e tests load via Build.IsTestMode.
        if (!Try(out var items, out var e, await sourceParser.ParseSolutionAsync(DemoModel.DemoSolutionName)))
            Assert.Fail(e.AllErrorMessages());

        var nodes = items.Nodes().ToList();
        Assert.NotEmpty(nodes);
        Assert.Contains(nodes, n => n.Properties.Type == NodeType.Solution && n.Name.Contains("Demo"));
    }

    [Fact(Skip = "Disabled, since always parsing whole solution takes time")]
    // [Fact]
    public async Task TestSolutionSourceParserAsync()
    {
        var sourceParser = new SourceParser();
        if (!Try(out var items, out var e, await sourceParser.ParseSolutionAsync(Root.SolutionFilePath)))
            Assert.Fail(e.AllErrorMessages());

        var nodes = items.Nodes().OrderBy(n => n.Name).ToList();
        Assert.NotEmpty(nodes);

        var n = nodes.Where(n => n.Name.Contains("AppBar")).ToList();

        var links = items.Links().OrderBy(l => l.Source).ThenBy(l => l.Target).ToList();
        Assert.NotEmpty(links);
    }
}
