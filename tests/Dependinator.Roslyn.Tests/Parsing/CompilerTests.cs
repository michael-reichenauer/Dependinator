using Dependinator.Roslyn.Parsing;
using Microsoft.CodeAnalysis;

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

            var compilation = AssertOk(await Compiler.GetCompilationAsync(project));

            var allTypes = Compiler.GetAllTypes(compilation).ToList();

            var allTypeNames = allTypes
                .Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Order()
                .ToList();

            // Razor component
            Assert.NotNull(allTypeNames.FirstOrDefault(n => n.Contains("AppProgress")));
        }
    }
}
