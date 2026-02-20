using DependinatorCore.Parsing.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DependinatorCore.Parsing.Sources.Roslyn;

class Compiler
{
    public static MSBuildWorkspace CreateWorkspace()
    {
        MSBuildLocatorHelper.Register();
        var workspace = MSBuildWorkspace.Create();
        return workspace;
    }

    public static async Task<R<Compilation>> GetCompilationAsync(Project project)
    {
        var compilation = await project.GetCompilationAsync();
        if (compilation is null)
            return R.Error($"No compilation (project may not be supported/loaded) for {project.FilePath}.");

        var diagnostics = compilation.GetDiagnostics();
        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .OrderBy(d => d.Location.IsInSource ? d.Location.GetLineSpan().Path : "")
            .ThenBy(d => d.Location.IsInSource ? d.Location.GetLineSpan().StartLinePosition.Line : int.MaxValue)
            .ToArray();

        foreach (var error in errors)
            Log.Warn($"Source Error: {error}");

        return compilation;
    }

    public static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation)
    {
        return GetAllNamedTypes(compilation.Assembly.GlobalNamespace);
    }

    static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol childNs)
            {
                foreach (var t in GetAllNamedTypes(childNs))
                    yield return t;
            }
            else if (member is INamedTypeSymbol namedType)
            {
                foreach (var t in GetAllNamedTypes(namedType))
                    yield return t;
            }
        }
    }

    static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamedTypeSymbol type)
    {
        yield return type;

        foreach (var nested in type.GetTypeMembers())
        {
            foreach (var t in GetAllNamedTypes(nested))
                yield return t;
        }
    }
}
