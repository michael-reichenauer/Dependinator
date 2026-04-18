using System.IO.Compression;
using Dependinator.Core.Shared;
using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

[Transient]
class SourceParser : ISourceParser
{
    public async Task<R<IReadOnlyList<Item>>> ParseSolutionAsync(string solutionPath)
    {
        try
        {
            using var workspace = Compiler.CreateWorkspace();

            Solution solution = await workspace.OpenSolutionAsync(solutionPath);

            foreach (var diag in workspace.Diagnostics)
                Log.Warn($"Workspace: [{diag.Kind}] {diag.Message}");

            var solutionName = Names.GetSolutionName(solutionPath);
            var solutionNode = new Node(solutionName, new() { Type = NodeType.Solution });

            var projects = solution
                .Projects.Where(p => p.Language == LanguageNames.CSharp)
                .Where(p => !IsTestProject(p))
                .ToList();

            Log.Info($"Solution projects: {projects.Count} ({string.Join(", ", projects.Select(p => p.Name))})");

            List<Item> solutionNodes = [];
            solutionNodes.Add(new Item(solutionNode, null));

            foreach (var project in projects)
            {
                if (!Try(out var items, out var e, await ParseProjectAsync(project, solutionNode.Name)))
                {
                    Log.Warn($"Failed to parse project {project.Name}: {e.ErrorMessage}");
                    continue;
                }

                solutionNodes.AddRange(items);
            }

            // var parseProjectTasks = projects.Select(p => ParseProjectAsync(p, solutionNode.Name));

            // await foreach (var parseProjectTask in Task.WhenEach(parseProjectTasks))
            // {
            //     if (!Try(out var items, out var e, await parseProjectTask))
            //         continue;
            //     solutionNodes.AddRange(items);
            // }

            // await WriteCompressedDemoModelAsync(solutionNodes, solutionPath);

            return solutionNodes;
        }
        catch (Exception e)
        {
            return R.Error(e);
        }
    }

    public async Task<R<IReadOnlyList<Item>>> ParseProjectAsync(string projectPath)
    {
        try
        {
            using var workspace = Compiler.CreateWorkspace();

            var project = await workspace.OpenProjectAsync(projectPath);
            // Log.Info("Parse:", projectPath);
            return await ParseProjectAsync(project, null);
        }
        catch (Exception e)
        {
            return R.Error(e);
        }
    }

    public async Task<R<IReadOnlyList<Item>>> ParseProjectAsync(Project project, string? parentName)
    {
        // Log.Info("Parse:", project.Name);
        if (!Try(out var compilation, out var e, await Compiler.GetCompilationAsync(project)))
            return e;

        return ParseProjectCompilation(compilation, parentName).ToList();
    }

    static IEnumerable<Item> ParseProjectCompilation(Compilation compilation, string? parentName)
    {
        var moduleName = Names.GetModuleName(compilation);
        yield return new Item(new Node(moduleName, new() { Type = NodeType.Assembly, Parent = parentName }), null);

        var typeNames = Compiler
            .GetAllTypes(compilation)
            .Where(t => !t.IsImplicitlyDeclared)
            .Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToList();

        foreach (var type in Compiler.GetAllTypes(compilation).Where(t => !t.IsImplicitlyDeclared))
        {
            foreach (var item in TypeParser.ParseType(type, compilation, moduleName))
                yield return item;
        }
    }

    static async Task WriteCompressedDemoModelAsync(IReadOnlyList<Item> solutionNodes, string solutionPath)
    {
        if (Build.IsWasm || solutionPath != DemoModel.WorkingSolutionPath)
            return;

        string outputPath = DemoModel.DemoOutputPath;
        var json = Json.Serialize(solutionNodes);
        json = json.Replace("Dependinator", "Demo");

        await using var file = File.Create(outputPath);
        await using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
        await using var writer = new StreamWriter(gzip);
        await writer.WriteAsync(json);
        Log.Info($"Wrote demo model {json.Length} json size to {outputPath}");
    }

    static bool IsTestProject(Project project) => project.Name.EndsWith("Test") || project.Name.EndsWith(".Tests");
}
