using DependinatorCore.Parsing.Sources.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DependinatorCore.Tests.Parsing.Sources.Roselyn;

[CollectionDefinition(nameof(RoslynCollection))]
public sealed class RoslynCollection : ICollectionFixture<RoslynFixture> { }

public sealed class RoslynFixture : IAsyncLifetime
{
    public MSBuildWorkspace Workspace { get; private set; } = null!;
    public IReadOnlyList<INamedTypeSymbol> AllTestTypes { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Workspace = Compiler.CreateWorkspace();
        var project = await Workspace.OpenProjectAsync(Root.ProjectFilePath);
        if (!Try(out var compilation, out var e, await Compiler.GetCompilationAsync(project)))
            throw new Exception($"Failed to get compilation for test project: {e.AllErrorMessages()}");

        AllTestTypes = Compiler.GetAllTypes(compilation).ToList();
    }

    public Task DisposeAsync()
    {
        Workspace.Dispose();
        return Task.CompletedTask;
    }
}
