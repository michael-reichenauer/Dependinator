using Dependinator.Roslyn.Parsing;
using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Tests.Parsing;

public class CompilerTests
{
    [Fact(Skip = "Disabled, since always parsing project takes extra time")]
    //[Fact]
    public async Task TestDependinatorUISourceParserAsync()
    {
        var projectPath = Path.Combine(Root.SolutionFolderPath, "Dependinator.UI", "Dependinator.UI.csproj");

        using var workspace = Compiler.CreateWorkspace();
        var project = await workspace.OpenProjectAsync(projectPath);

        if (!Try(out var compilation, out var e, await Compiler.GetCompilationAsync(project)))
            Assert.Fail(e.AllErrorMessages());

        var allTypes = Compiler.GetAllTypes(compilation).ToList();

        var allTypeNames = allTypes.Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToList();

        // Razor component
        Assert.NotNull(allTypeNames.FirstOrDefault(n => n.Contains("AppProgress")));
    }
}
