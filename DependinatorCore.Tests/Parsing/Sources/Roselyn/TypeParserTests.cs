using DependinatorCore.Parsing;
using DependinatorCore.Parsing.Sources.Roslyn;
using DependinatorCore.Utils.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DependinatorCore.Tests.Parsing.Sources.Roselyn;

public interface SourceTestInterface { }

public class SourceTestBaseType { }

public class SourceTestDerivedType : SourceTestBaseType, SourceTestInterface { }

public class TypeParserTests : IAsyncLifetime
{
    MSBuildWorkspace workspace = null!;
    IReadOnlyList<INamedTypeSymbol> allTestTypes = null!;

    public async Task InitializeAsync()
    {
        workspace = Compiler.CreateWorkspace();
        var project = await workspace.OpenProjectAsync(Root.ProjectFilePath);
        if (!Try(out var compilation, out var e, await Compiler.GetCompilationAsync(project)))
            throw new Exception("Failed to get compilation for test project");
        allTestTypes = Compiler.GetAllTypes(compilation).ToList();
    }

    public Task DisposeAsync()
    {
        workspace.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Test()
    {
        foreach (var type in allTestTypes)
        {
            foreach (var item in TypeParser.ParseType(type, "TestType"))
            {
                Log.Info($"Item {item.Node?.Name}");
            }
        }
    }
}
