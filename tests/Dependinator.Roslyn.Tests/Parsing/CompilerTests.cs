using Dependinator.Roslyn.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dependinator.Roslyn.Tests.Parsing;

public class CompilerTests
{
    [Fact(Skip = "Disabled since always parsing project takes time")]
    // [Fact]
    public async Task TestDependinatorUISourceParserAsync()
    {
        var projectPath = Path.Combine(Root.SrcFolderPath, "Dependinator.UI", "Dependinator.UI.csproj");

        var workspace = AssertOk(Compiler.CreateWorkspace());

        using (workspace)
        {
            var project = await workspace.OpenProjectAsync(projectPath);

            var compilation = AssertOk(await Compiler.GetCompilationAsync(project)).Compilation;

            var allTypes = Compiler.GetAllTypes(compilation).ToList();

            var allTypeNames = allTypes
                .Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Order()
                .ToList();

            // Razor component
            Assert.NotNull(allTypeNames.FirstOrDefault(n => n.Contains("AppProgress")));
        }
    }

    // The projects of this solution use a newer C# than the parser's Roslyn knows by number
    // (LangVersion 15, unions). Its features must not be counted as compile errors: they
    // stayed in the problem indicator whatever the user fixed.
    [Fact]
    public async Task GetCompilationAsync_ShouldReportNoErrors_ForANewerLanguageVersion()
    {
        var projectPath = Path.Combine(Root.SrcFolderPath, "Dependinator.Mcp", "Dependinator.Mcp.csproj");

        using var workspace = AssertOk(Compiler.CreateWorkspace());
        var project = await workspace.OpenProjectAsync(projectPath);

        var compiled = AssertOk(await Compiler.GetCompilationAsync(project));

        Assert.Equal(0, compiled.ErrorCount);
    }

    [Theory]
    [InlineData(LanguageVersion.Default, LanguageVersion.Preview)] // Not recognized, or not set
    [InlineData(LanguageVersion.CSharp12, LanguageVersion.CSharp12)]
    public void WithUsableLanguageVersion_ShouldUseAllFeatures_OnlyForAnUnknownVersion(
        LanguageVersion specified,
        LanguageVersion expected
    )
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace
            .AddProject("Sample", LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(specified));

        var options = (CSharpParseOptions)Compiler.WithUsableLanguageVersion(project).ParseOptions!;

        Assert.Equal(expected, options.LanguageVersion);
    }
}
