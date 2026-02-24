using System.IO.Compression;
using DependinatorCore.Shared;
using Microsoft.CodeAnalysis;

namespace DependinatorRoslyn.Parsing;

[Transient]
class SourceParser : ISourceParser
{
    public async Task<R<IReadOnlyList<Item>>> ParseSolutionAsync(string solutionPath)
    {
        using var workspace = Compiler.CreateWorkspace();

        Solution solution = await workspace.OpenSolutionAsync(solutionPath);

        var solutionName = Names.GetSolutionName(solutionPath);
        var solutionNode = new Node(solutionName, new() { Type = NodeType.Solution });

        var projects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).Where(p => !IsTestProject(p));

        var parseProjectTasks = projects.Select(p => ParseProjectAsync(p, solutionNode.Name));

        List<Item> solutionNodes = [];
        solutionNodes.Add(new Item(solutionNode, null));

        await foreach (var parseProjectTask in Task.WhenEach(parseProjectTasks))
        {
            if (!Try(out var items, out var e, await parseProjectTask))
                continue;
            solutionNodes.AddRange(items);
        }

        // await WriteCompressedExampleModelAsync(solutionNodes, solutionPath);

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

    static async Task WriteCompressedExampleModelAsync(IReadOnlyList<Item> solutionNodes, string solutionPath)
    {
        if (Build.IsWasm || solutionPath != ExampleModel.SolutionExample)
            return;

        string outputPath = ExampleModel.EmbeddedBrowserExamplePath;
        var json = Json.Serialize(solutionNodes);

        await using var file = File.Create(outputPath);
        await using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
        await using var writer = new StreamWriter(gzip);
        await writer.WriteAsync(json);
        Log.Info($"Wrote example model {json.Length} json size to {outputPath}");
    }

    static bool IsTestProject(Project project) => project.Name.EndsWith("Test") || project.Name.EndsWith(".Tests");
}
