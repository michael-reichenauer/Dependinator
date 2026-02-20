using DependinatorCore.Parsing.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DependinatorCore.Parsing.Sources.Roslyn;

[Transient]
class SourceParser : ISourceParser
{
    static SymbolDisplayFormat MemberFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
    );

    public async Task<R<IReadOnlyList<Item>>> ParseSolutionAsync(string slnPath, bool isSkipTests = true)
    {
        MSBuildLocatorHelper.Register();
        using var workspace = MSBuildWorkspace.Create();

        var solution = await workspace.OpenSolutionAsync(slnPath);

        var projects = solution
            .Projects.Where(p => p.Language == LanguageNames.CSharp)
            .Where(p => !IsTestProject(p) || !isSkipTests);

        var parseProjectTasks = projects.Select(ParseProjectAsync);

        List<Item> solutionNodes = [];
        await foreach (var parseProjectTask in Task.WhenEach(parseProjectTasks))
        {
            if (!Try(out var items, out var e, await parseProjectTask))
                continue;
            solutionNodes.AddRange(items);
        }

        return solutionNodes;
    }

    public async Task<R<IReadOnlyList<Item>>> ParseProjectAsync(string projectPath)
    {
        MSBuildLocatorHelper.Register();
        using var workspace = MSBuildWorkspace.Create();

        var project = await workspace.OpenProjectAsync(projectPath);
        return await ParseProjectAsync(project);
    }

    public async Task<R<IReadOnlyList<Item>>> ParseProjectAsync(Project project)
    {
        if (!Try(out var compilation, out var e, await GetCompilationAsync(project)))
            return e;

        return ParseProjectCompilation(compilation).ToList();
    }

    static async Task<R<Compilation>> GetCompilationAsync(Project project)
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

    static IEnumerable<Item> ParseProjectCompilation(Compilation compilation)
    {
        var moduleName = Names.GetModuleName(compilation);

        foreach (var type in GetAllNamedTypes(compilation.Assembly.GlobalNamespace).Where(t => !t.IsImplicitlyDeclared))
        {
            foreach (var item in TypeParser.ParseType(type, moduleName))
                yield return item;
        }
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

    static bool IsTestProject(Project project) => project.Name.EndsWith("Test") || project.Name.EndsWith(".Tests");
}
