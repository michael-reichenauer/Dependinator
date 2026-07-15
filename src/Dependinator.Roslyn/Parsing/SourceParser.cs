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

            // Parse all projects in parallel
            var parseProjectTasks = projects
                .Select(p => (Project: p, Task: ParseProjectAsync(p, solutionNode.Name)))
                .ToList();

            foreach (var (project, parseProjectTask) in parseProjectTasks)
            {
                if (!Try(out var items, out var e, await parseProjectTask))
                {
                    Log.Warn($"Failed to parse project {project.Name}: {e.ErrorMessage}");
                    continue;
                }
                solutionNodes.AddRange(items);
            }

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
            return await ParseProjectAsync(project, null);
        }
        catch (Exception e)
        {
            return R.Error(e);
        }
    }

    public async Task<R<IReadOnlyList<Item>>> ParseProjectAsync(Project project, string? parentName)
    {
        if (!Try(out var compilation, out var e, await Compiler.GetCompilationAsync(project)))
            return e;

        return ParseProjectCompilation(compilation, parentName, project.FilePath).ToList();
    }

    static IEnumerable<Item> ParseProjectCompilation(Compilation compilation, string? parentName, string? projectPath)
    {
        var moduleName = Names.GetModuleName(compilation);
        var (description, fileSpan) = GetAssemblyDescription(compilation, projectPath);
        bool isExecutable = compilation.Options.OutputKind
            is OutputKind.ConsoleApplication
                or OutputKind.WindowsApplication
                or OutputKind.WindowsRuntimeApplication;
        yield return new Item(
            new Node(
                moduleName,
                new()
                {
                    Type = NodeType.Assembly,
                    Description = description,
                    Parent = parentName,
                    IsExecutable = isExecutable,
                    FileSpan = fileSpan,
                }
            ),
            null
        );

        foreach (var type in Compiler.GetAllTypes(compilation).Where(t => !t.IsImplicitlyDeclared))
        {
            foreach (var item in TypeParser.ParseType(type, compilation, moduleName))
                yield return item;
        }

        foreach (var item in NamespaceParser.ParseNamespaces(compilation, moduleName))
            yield return item;
    }

    // Reads the [assembly: AssemblyDescription("...")] value from the project's compiled
    // assembly attributes, used as the description for the assembly node, together with a
    // source location where the description can be edited or added ("show source").
    internal static (string? Description, FileSpan? FileSpan) GetAssemblyDescription(
        Compilation compilation,
        string? projectPath
    )
    {
        var attribute = compilation
            .Assembly.GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "System.Reflection.AssemblyDescriptionAttribute"
            );

        var fileSpan = GetAssemblyDescriptionSpan(attribute, compilation, projectPath);

        if (attribute is null || attribute.ConstructorArguments.Length == 0)
            return (null, fileSpan);

        return (attribute.ConstructorArguments[0].Value as string, fileSpan);
    }

    // The assembly node has no single source definition, so pick the best editable location:
    // the hand-written [assembly: AssemblyDescription(...)] attribute if present, otherwise a
    // hand-written Usings.cs/AssemblyInfo.cs file, otherwise the project file itself (where a
    // <Description> property generates the attribute).
    static FileSpan? GetAssemblyDescriptionSpan(AttributeData? attribute, Compilation compilation, string? projectPath)
    {
        if (attribute?.ApplicationSyntaxReference is { } syntaxRef)
        {
            var lineSpan = syntaxRef.GetSyntax().GetLocation().GetLineSpan();
            if (!IsGeneratedPath(lineSpan.Path))
                return Locations.ToFileSpan(lineSpan);
        }

        foreach (var fileName in new[] { "Usings.cs", "AssemblyInfo.cs" })
        {
            var path = compilation
                .SyntaxTrees.Select(t => t.FilePath)
                .Where(p => !IsGeneratedPath(p))
                .FirstOrDefault(p => string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase));

            if (path is not null)
                return new FileSpan(path, 0, 0);
        }

        if (projectPath is not null && File.Exists(projectPath))
        {
            var line = GetProjectDescriptionLine(projectPath);
            return new FileSpan(projectPath, line, line);
        }

        return null;
    }

    static bool IsGeneratedPath(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || path.Contains("/obj/");

    static int GetProjectDescriptionLine(string projectPath)
    {
        try
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(projectPath))
            {
                if (line.Contains("<Description>"))
                    return lineNumber;
                lineNumber++;
            }
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to read project file {projectPath}: {e.Message}");
        }

        return 0;
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

    // Heuristic: skip test projects by naming convention (e.g. "Foo.Tests", "FooTests", "FooTest")
    static bool IsTestProject(Project project) => project.Name.EndsWith("Test") || project.Name.EndsWith("Tests");
}
