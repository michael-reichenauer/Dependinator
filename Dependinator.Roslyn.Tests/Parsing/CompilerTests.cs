using Dependinator.Roslyn.Parsing;
using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Tests.Parsing;

public class CompilerTests
{
    //[Fact(Skip = "Disabled, since always parsing project takes extra time")]
    [Fact]
    public async Task TestDependinatorUISourceParserAsync()
    {
        var projectPath = Path.Combine(Root.SolutionFolderPath, "Dependinator.UI", "Dependinator.UI.csproj");

        using var workspace = Compiler.CreateWorkspace();
        var project = await workspace.OpenProjectAsync(projectPath);

        if (!Try(out var compilation, out var e, await Compiler.GetCompilationAsync(project)))
            Assert.Fail(e.AllErrorMessages());

        var diagnostics = compilation.GetDiagnostics();
        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .OrderBy(d => d.Location.IsInSource ? d.Location.GetLineSpan().Path : "")
            .ThenBy(d => d.Location.IsInSource ? d.Location.GetLineSpan().StartLinePosition.Line : int.MaxValue)
            .ToArray();
        Assert.Empty(errors);

        var allTypes = Compiler.GetAllTypes(compilation).ToList();

        var allTypeNames = allTypes
            .Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Order()
            .ToList();

        // Razor component
        Assert.NotNull(allTypeNames.FirstOrDefault(n => n.Contains("AppProgress")));
    }
}
