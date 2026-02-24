using DependinatorRoslyn.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DependinatorRoslyn.Tests.Parsing;

[CollectionDefinition(nameof(RoslynCollection))]
public sealed class RoslynCollection : ICollectionFixture<RoslynFixture> { }

public sealed class RoslynFixture : IAsyncLifetime
{
    public MSBuildWorkspace Workspace { get; private set; } = null!;
    public Compilation Compilation { get; private set; } = null!;
    public IReadOnlyList<INamedTypeSymbol> AllTestTypes { get; private set; } = null!;
    public string ModelName { get; private set; } = null!;

    public INamedTypeSymbol Type<T>()
    {
        var runtimeType = typeof(T);
        if (runtimeType.IsGenericType && !runtimeType.IsGenericTypeDefinition)
            runtimeType = runtimeType.GetGenericTypeDefinition();

        var typeName = GetMetadataTypeName(runtimeType);
        var matchingTypes = AllTestTypes.Where(type => GetMetadataTypeName(type) == typeName).ToList();
        return matchingTypes.Count switch
        {
            1 => matchingTypes[0],
            0 => throw new InvalidOperationException($"Type '{typeName}' was not found in Roslyn fixture."),
            _ => throw new InvalidOperationException($"Type '{typeName}' was matched by multiple Roslyn symbols."),
        };
    }

    public async Task InitializeAsync()
    {
        Workspace = Compiler.CreateWorkspace();
        var project = await Workspace.OpenProjectAsync(Root.ProjectFilePath);
        if (!Try(out var compilation, out var e, await Compiler.GetCompilationAsync(project)))
            throw new Exception($"Failed to get compilation for test project: {e.AllErrorMessages()}");

        Compilation = compilation;
        AllTestTypes = Compiler.GetAllTypes(compilation).ToList();
        ModelName = Names.GetModuleName(compilation);
    }

    public Task DisposeAsync()
    {
        Workspace.Dispose();
        return Task.CompletedTask;
    }

    static string GetMetadataTypeName(Type type)
    {
        if (type.IsNested)
            return $"{GetMetadataTypeName(type.DeclaringType!)}.{type.Name}";
        return string.IsNullOrEmpty(type.Namespace) ? type.Name : $"{type.Namespace}.{type.Name}";
    }

    static string GetMetadataTypeName(INamedTypeSymbol type)
    {
        if (type.ContainingType is not null)
            return $"{GetMetadataTypeName(type.ContainingType)}.{type.MetadataName}";
        if (type.ContainingNamespace.IsGlobalNamespace)
            return type.MetadataName;
        return $"{type.ContainingNamespace.ToDisplayString()}.{type.MetadataName}";
    }
}
