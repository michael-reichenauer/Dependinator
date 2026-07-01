using System.IO.Compression;
using Dependinator.Core.Shared;
using Microsoft.CodeAnalysis;

// Roslyn-based parsing of solutions and projects into the code model, extracting namespaces,
// types, and members with their links, plus source metadata such as comments/descriptions and
// file locations used for navigation.
namespace Dependinator.Roslyn.Parsing;

[Transient]
class SourceParser : ISourceParser
{
    public async Task<R<IReadOnlyList<Item>>> ParseSolutionAsync(string solutionPath)
    {
        // The demo model is pre-parsed and embedded, so load it directly instead of
        // running Roslyn. Used as a fallback when no real model is available and by
        // UI/e2e tests (Build.IsTestMode) for a fast, deterministic model.
        if (solutionPath == DemoModel.DemoSolutionName)
            return await LoadEmbeddedDemoModelAsync();

        try
        {
            using var workspace = Compiler.CreateWorkspace();

            Solution solution = await workspace.OpenSolutionAsync(solutionPath);

            foreach (var diag in workspace.Diagnostics)
                Log.Warn($"Workspace: [{diag.Kind}] {diag.Message}");

            var solutionName = Names.GetSolutionName(solutionPath);
            var description = SolutionDescriptionReader.TryReadFromReadme(solutionPath);
            var solutionNode = new Node(solutionName, new() { Type = NodeType.Solution, Description = description });

            var projects = solution
                .Projects.Where(p => p.Language == LanguageNames.CSharp)
                .Where(p => !IsTestProject(p))
                .ToList();

            Log.Info($"Solution projects: {projects.Count} ({string.Join(", ", projects.Select(p => p.Name))})");

            List<Item> solutionNodes = [];
            solutionNodes.Add(new Item(solutionNode, null));

            // // In sequence
            // foreach (var project in projects)
            // {
            //     if (!Try(out var items, out var e, await ParseProjectAsync(project, solutionNode.Name)))
            //     {
            //         Log.Warn($"Failed to parse project {project.Name}: {e.ErrorMessage}");
            //         continue;
            //     }

            //     solutionNodes.AddRange(items);
            // }

            // In parallel
            var parseProjectTasks = projects.Select(p => ParseProjectAsync(p, solutionNode.Name));

            await foreach (var parseProjectTask in Task.WhenEach(parseProjectTasks))
            {
                if (!Try(out var items, out var e, await parseProjectTask))
                    continue;
                solutionNodes.AddRange(items);
            }

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
        var description = GetAssemblyDescription(compilation);
        yield return new Item(
            new Node(moduleName, new() { Type = NodeType.Assembly, Description = description, Parent = parentName }),
            null
        );

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

        foreach (var item in NamespaceParser.ParseNamespaces(compilation, moduleName))
            yield return item;
    }

    // Reads the [assembly: AssemblyDescription("...")] value from the project's compiled
    // assembly attributes, used as the description for the assembly node.
    static string? GetAssemblyDescription(Compilation compilation)
    {
        var attribute = compilation.Assembly.GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "System.Reflection.AssemblyDescriptionAttribute"
            );

        if (attribute is null || attribute.ConstructorArguments.Length == 0)
            return null;

        return attribute.ConstructorArguments[0].Value as string;
    }

    // Reads the gzip-compressed, pre-parsed demo model embedded in this assembly
    // (Dependinator.Roslyn.demo.model) and deserializes it into parsed items.
    static async Task<R<IReadOnlyList<Item>>> LoadEmbeddedDemoModelAsync()
    {
        try
        {
            const string resourceName = "Dependinator.Roslyn.demo.model";
            await using var stream = typeof(SourceParser).Assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                return R.Error($"Embedded demo model resource '{resourceName}' was not found.");

            await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var json = await reader.ReadToEndAsync();

            var items = Json.Deserialize<List<Item>>(json);
            if (items is null)
                return R.Error("Failed to deserialize the embedded demo model.");

            return items;
        }
        catch (Exception e)
        {
            return R.Error(e);
        }
    }

    static async Task WriteCompressedDemoModelAsync(IReadOnlyList<Item> solutionNodes, string solutionPath)
    {
        try
        {
            Log.Info(solutionPath);
            Log.Info(DemoModel.WorkingSolutionPath);
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
        catch (Exception ex)
        {
            Log.Exception(ex);
            throw;
        }
    }

    static bool IsTestProject(Project project) => project.Name.EndsWith("Test") || project.Name.EndsWith(".Tests");
}
