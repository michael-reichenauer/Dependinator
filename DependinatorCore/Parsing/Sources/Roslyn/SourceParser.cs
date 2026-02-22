using Microsoft.CodeAnalysis;

namespace DependinatorCore.Parsing.Sources.Roslyn;

[Transient]
class SourceParser : ISourceParser
{
    public async Task<R<IReadOnlyList<Item>>> ParseSolutionAsync(string slnPath, bool isSkipTests = true)
    {
        using var workspace = Compiler.CreateWorkspace();

        Solution solution = await workspace.OpenSolutionAsync(slnPath);
        var solutionName = Names.GetSolutionName(slnPath);

        var projects = solution
            .Projects.Where(p => p.Language == LanguageNames.CSharp)
            .Where(p => !IsTestProject(p) || !isSkipTests);

        var parseProjectTasks = projects.Select(p => ParseProjectAsync(p, solutionName));

        List<Item> solutionNodes = [];
        solutionNodes.Add(new Item(new Node(solutionName, new() { Type = NodeType.Solution }), null));
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
        using var workspace = Compiler.CreateWorkspace();

        var project = await workspace.OpenProjectAsync(projectPath);
        return await ParseProjectAsync(project, null);
    }

    public async Task<R<IReadOnlyList<Item>>> ParseProjectAsync(Project project, string? parentName)
    {
        if (!Try(out var compilation, out var e, await Compiler.GetCompilationAsync(project)))
            return e;

        return ParseProjectCompilation(compilation, parentName).ToList();
    }

    static IEnumerable<Item> ParseProjectCompilation(Compilation compilation, string? parentName)
    {
        var moduleName = Names.GetModuleName(compilation);
        yield return new Item(new Node(moduleName, new() { Type = NodeType.Assembly, Parent = parentName }), null);

        foreach (var type in Compiler.GetAllTypes(compilation).Where(t => !t.IsImplicitlyDeclared))
        {
            foreach (var item in TypeParser.ParseType(type, compilation, moduleName))
                yield return item;
        }
    }

    static bool IsTestProject(Project project) => project.Name.EndsWith("Test") || project.Name.EndsWith(".Tests");
}
