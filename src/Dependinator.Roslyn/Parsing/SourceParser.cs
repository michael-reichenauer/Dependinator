using System.IO.Compression;
using Dependinator.Core.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

// Roslyn-based parsing of solutions and projects into the code model, extracting namespaces,
// types, and members with their links, plus source metadata such as comments/descriptions and
// file locations used for navigation.
namespace Dependinator.Roslyn.Parsing;

[Transient]
class SourceParser : ISourceParser
{
    public async Task<R<IReadOnlyList<Item>>> ParseSolutionAsync(string solutionPath, SolutionParseOptions options)
    {
        // The demo model is pre-parsed and embedded, so load it directly instead of
        // running Roslyn. Used as a fallback when no real model is available and by
        // UI/e2e tests (Build.IsTestMode) for a fast, deterministic model.
        if (solutionPath == DemoModel.DemoSolutionName)
            return await LoadEmbeddedDemoModelAsync();

        if (!File.Exists(solutionPath))
            return R.Error($"Solution file not found: {solutionPath}");

        try
        {
            if (!Try(out var workspace, out var workspaceError, Compiler.CreateWorkspace()))
                return workspaceError;
            using (workspace)
            {
                return await ParseSolutionAsync(workspace, solutionPath, options);
            }
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to parse {solutionPath}");
            return R.Error($"Failed to parse '{Names.GetSolutionName(solutionPath)}'.", e);
        }
    }

    async Task<R<IReadOnlyList<Item>>> ParseSolutionAsync(
        MSBuildWorkspace workspace,
        string solutionPath,
        SolutionParseOptions options
    )
    {
        Solution solution = await workspace.OpenSolutionAsync(solutionPath);

        foreach (var diag in workspace.Diagnostics)
            Log.Warn($"Workspace: [{diag.Kind}] {diag.Message}");

        var solutionName = Names.GetSolutionName(solutionPath);
        var description = SolutionDescriptionReader.TryReadFromReadme(solutionPath);
        var solutionNode = new Node(solutionName, new() { Type = NodeType.Solution, Description = description });

        var csharpProjects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToList();

        // A solution where no project could be loaded would silently render as an empty diagram,
        // so report it, together with whatever MSBuild complained about while loading.
        if (csharpProjects.Count == 0)
            return R.Error($"No C# projects could be loaded from '{solutionName}'.{GetDiagnosticsText(workspace)}");

        // Check the option first, so including tests skips the reference scan entirely.
        var projects = csharpProjects.Where(p => options.IncludeTestProjects || !IsTestProject(p)).ToList();
        if (projects.Count == 0)
            return R.Error(
                $"'{solutionName}' contains only test projects. "
                    + "Enable Settings > Include Test Projects in the menu to parse them."
            );

        Log.Info($"Solution projects: {projects.Count} ({string.Join(", ", projects.Select(p => p.Name))})");

        List<Item> solutionNodes = [];
        solutionNodes.Add(new Item(solutionNode, null));

        // Parse all projects in parallel
        var parseProjectTasks = projects
            .Select(p => (Project: p, Task: ParseProjectAsync(p, solutionNode.Name)))
            .ToList();

        ErrorResult? firstProjectError = null;
        var failedCount = 0;
        foreach (var (project, parseProjectTask) in parseProjectTasks)
        {
            if (!Try(out var items, out var e, await parseProjectTask))
            {
                Log.Warn($"Failed to parse project {project.Name}: {e.ErrorMessage}");
                firstProjectError ??= e;
                failedCount++;
                continue;
            }
            solutionNodes.AddRange(items);
        }

        // Some projects failing still yields a usable (if partial) model, but all of them failing
        // means there is nothing to show, so report it instead of returning a lone solution node.
        if (failedCount == projects.Count)
            return R.Error(
                $"Failed to parse all {projects.Count} projects in '{solutionName}'.{GetDiagnosticsText(workspace)}",
                firstProjectError!
            );

        if (failedCount > 0)
            Log.Warn($"Failed to parse {failedCount} of {projects.Count} projects in {solutionName}");

        return solutionNodes;
    }

    // The MSBuild failure diagnostics explain why projects did not load (e.g. an unsupported
    // project type or a missing SDK/workload); include the first few in the user-facing error.
    static string GetDiagnosticsText(MSBuildWorkspace workspace)
    {
        const int MaxDiagnostics = 3;
        const int MaxDiagnosticLength = 300;
        var failures = workspace
            .Diagnostics.Where(d => d.Kind == WorkspaceDiagnosticKind.Failure)
            .Select(d => d.Message)
            .Distinct()
            .Take(MaxDiagnostics)
            // MSBuild messages can be very long (whole build logs), so keep them readable.
            .Select(m => m.Length > MaxDiagnosticLength ? m[..MaxDiagnosticLength] + " ..." : m)
            .ToList();

        if (failures.Count == 0)
            return "";

        // Kept on one line; the message is shown in a snackbar (plain text) and logged.
        return " " + string.Join(" ", failures);
    }

    public async Task<R<IReadOnlyList<Item>>> ParseProjectAsync(string projectPath)
    {
        try
        {
            if (!Try(out var workspace, out var workspaceError, Compiler.CreateWorkspace()))
                return workspaceError;

            using (workspace)
            {
                var project = await workspace.OpenProjectAsync(projectPath);
                return await ParseProjectAsync(project, null);
            }
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to parse {projectPath}");
            return R.Error($"Failed to parse '{Path.GetFileName(projectPath)}'.", e);
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
        bool isExecutable =
            compilation.Options.OutputKind
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

    static readonly string[] TestNameSuffixes = ["Test", "Tests", "Spec", "Specs"];

    static readonly HashSet<string> TestFrameworkAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "xunit.core",
        "xunit.assert",
        "xunit.v3.core",
        "xunit.v3.assert",
        "nunit.framework",
        "Microsoft.VisualStudio.TestPlatform.TestFramework",
        "Microsoft.VisualStudio.TestPlatform.ObjectModel",
        "TUnit.Core",
    };

    // A project is a test project if it references a test framework assembly, which is independent
    // of naming convention. MSBuildWorkspace resolves metadata references from files, so FilePath
    // is the reference's ".dll" path, and project-to-project references live in ProjectReferences
    // rather than here. Projects that failed to restore have no references at all, so the older
    // naming convention is kept as a fallback.
    internal static bool IsTestProject(Project project) =>
        IsTestProject(
            project.Name,
            project.MetadataReferences.OfType<PortableExecutableReference>().Select(r => r.FilePath)
        );

    internal static bool IsTestProject(string projectName, IEnumerable<string?> referencePaths) =>
        referencePaths.Any(p => p is not null && TestFrameworkAssemblies.Contains(Path.GetFileNameWithoutExtension(p)))
        || TestNameSuffixes.Any(s => projectName.EndsWith(s, StringComparison.OrdinalIgnoreCase));
}
