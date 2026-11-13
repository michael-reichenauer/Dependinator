using Dependinator.Core.Shared;

namespace Dependinator.Core.Parsing;

// Makes one model of the items of a workspace model's sources, each parsed by its own parser.
// The root node of every source ends up side by side under the model's root, with the names its
// own model has, so what is true of a node there (its source file, a rule, a search) is true here.
//   - A source that failed is a problem item, and the others still make the model.
//   - A source whose root is named like a node before it (two "main.bicep", two project folders
//     named "app") gets its folder in the name of its root, and so in the names of all its nodes.
//     A root keeps the name it had in its last parse (knownSources), whichever of the other
//     sources are parsed this time and in whatever order: the layout, the rules and what a
//     failed source keeps in the model all go by node names. That name is held for its source
//     also while the source is left out of the model or fails.
//   - A node that an earlier source already has is left to that source: a project that two
//     solutions share is shown once, in the first. Two projects that only share their name are
//     shown as one as well, since a C# project's nodes are not named by its solution: that is
//     reported as a problem.
static class WorkspaceItems
{
    public static Result<IReadOnlyList<Item>> Combine(
        string workspacePath,
        IReadOnlyList<(string Path, Result<IReadOnlyList<Item>> Result)> sources,
        IReadOnlyList<ParsedSource>? knownSources = null
    )
    {
        if (sources.Count == 0)
            return new Error("No sources are chosen for this workspace model.");

        var folder = ModelPaths.WorkspaceFolder(workspacePath);
        List<Item> combined = [];
        HashSet<string> nodeNames = new(StringComparer.Ordinal);
        Dictionary<string, string> projectFiles = new(StringComparer.Ordinal);
        HashSet<(string Message, string? NodeName)> problems = [];
        List<Error> errors = [];

        // The root names the sources had before, each held for its source: also for one that
        // fails this time (the model keeps its nodes) or is not in this parse. A source whose
        // root has another name now gives its old name up.
        Dictionary<string, string> knownRoots = [];
        Dictionary<string, string> rootOwners = new(StringComparer.Ordinal);
        foreach (var known in knownSources ?? [])
        {
            var isNamedAnew = sources.Any(source =>
                source.Path == known.Path
                && source.Result is IReadOnlyList<Item> items
                && !IsNameOf(known.RootName, RootName(items), WorkspaceSources.Key(folder, known.Path))
            );
            if (!isNamedAnew && knownRoots.TryAdd(known.Path, known.RootName))
                rootOwners.TryAdd(known.RootName, known.Path);
        }

        foreach (var (path, result) in sources)
        {
            if (result is not IReadOnlyList<Item> parsed)
            {
                errors.Add(result.Error);
                var message = $"{ModelPaths.DisplayName(path)} could not be parsed. {result.Error.Message}";
                combined.Add(new Item(null, null, Problem: new ParseProblem(message, 1)));
                continue;
            }

            bool IsTaken(string name) =>
                nodeNames.Contains(name) || (rootOwners.TryGetValue(name, out var owner) && owner != path);

            var items = parsed;
            var rootName = RootName(items);
            if (rootName is not null)
            {
                var newRootName =
                    knownRoots.TryGetValue(path, out var knownRoot) && !IsTaken(knownRoot) ? knownRoot
                    : IsTaken(rootName) ? UniqueRootName(rootName, WorkspaceSources.Key(folder, path), IsTaken)
                    : rootName;
                if (newRootName != rootName)
                {
                    items = [.. items.Select(item => Renamed(item, rootName, newRootName))];
                    rootName = newRootName;
                }
            }

            // Only the nodes of the sources before this one are left out: a parser can give a
            // node more than once (a project built for two frameworks), each adding to it.
            HashSet<string> sourceNodeNames = new(StringComparer.Ordinal);
            foreach (var item in items)
            {
                var problem = item.Problem;
                if (item.Node is { } node)
                {
                    // A node the model already has stays as the earlier source made it; a link
                    // to it from this source leads to that node.
                    if (nodeNames.Contains(node.Name))
                    {
                        if (!IsAnotherProject(node, projectFiles))
                            continue;
                        problem = SameNamedProjectProblem(path, node.Name);
                    }
                    else
                    {
                        sourceNodeNames.Add(node.Name);
                        if (ProjectFile(node) is { } file)
                            projectFiles.TryAdd(node.Name, file);
                    }
                }

                if (problem is not null && !problems.Add((problem.Message, problem.NodeName)))
                    continue;
                combined.Add(problem == item.Problem ? item : new Item(null, null, Problem: problem));
            }
            nodeNames.UnionWith(sourceNodeNames);

            if (rootName is not null)
                combined.Add(new Item(null, null, Source: new ParsedSource(path, rootName)));
        }

        if (errors.Count == sources.Count)
        {
            return sources.Count == 1
                ? errors[0]
                : new Error($"None of the {sources.Count} sources could be parsed. {errors[0].Message}", errors[0]);
        }

        return combined;
    }

    // The file a project node is edited in (in the project's folder), which tells two projects
    // of one name apart.
    static string? ProjectFile(Node node) =>
        node.Properties.Type == NodeType.Assembly ? node.Properties.FileSpan?.Path : null;

    static bool IsAnotherProject(Node node, Dictionary<string, string> projectFiles) =>
        ProjectFile(node) is { } file
        && projectFiles.TryGetValue(node.Name, out var earlierFile)
        && !string.Equals(file, earlierFile, StringComparison.OrdinalIgnoreCase);

    static ParseProblem SameNamedProjectProblem(string path, string nodeName) =>
        new(
            $"{ModelPaths.DisplayName(path)} has a project named like a project of another source. "
                + "The two are shown as one: leave one of the sources out of the model.",
            1,
            nodeName
        );

    // The node all the nodes of a source are under: every parser makes one of the solution type.
    static string? RootName(IReadOnlyList<Item> items) =>
        items.Select(item => item.Node).FirstOrDefault(node => node?.Properties.Type == NodeType.Solution)?.Name;

    // "app (services∕app)": the root's name with the source's folder below the workspace, in the
    // brackets that keep it one part of the name. The folder is written with a division slash
    // and "*" for its dots, since "/" and "." would split the name into parts.
    static string UniqueRootName(string rootName, string sourceKey, Func<string, bool> isTaken)
    {
        var sourceFolder = FolderText(sourceKey);
        var name = sourceFolder == "" ? rootName : $"{rootName} ({sourceFolder})";
        for (var number = 2; isTaken(name); number++)
            name = $"{rootName} ({(sourceFolder == "" ? "" : $"{sourceFolder} ")}{number})";
        return name;
    }

    static string FolderText(string sourceKey) =>
        WorkspaceSources
            .FolderOf(sourceKey)
            .Replace('/', '∕')
            .Replace('.', '*')
            .Replace('$', '＄')
            .Replace('#', '＃')
            .Replace("(", "")
            .Replace(")", "");

    // Whether a root name from an earlier parse is a name of this root: the name itself, or one
    // it was given to tell it apart (UniqueRootName). A source whose root changed its name (a
    // package was renamed) is named anew.
    static bool IsNameOf(string knownRootName, string? rootName, string sourceKey)
    {
        if (rootName is null)
            return false;
        if (knownRootName == rootName)
            return true;

        var start = $"{rootName} ({FolderText(sourceKey)}";
        if (!knownRootName.StartsWith(start, StringComparison.Ordinal) || !knownRootName.EndsWith(')'))
            return false;
        var number = knownRootName[start.Length..^1].TrimStart(' ');
        return number.All(char.IsAsciiDigit);
    }

    static Item Renamed(Item item, string from, string to)
    {
        string Name(string name) =>
            name == from ? to
            : name.StartsWith(from, StringComparison.Ordinal) && name.Length > from.Length && name[from.Length] == '.'
                ? to + name[from.Length..]
            : name;

        return new Item(
            item.Node is { } node ? new Node(Name(node.Name), Renamed(node.Properties, Name)) : null,
            item.Link is { } link ? link with { Source = Name(link.Source), Target = Name(link.Target) } : null,
            item.LineDescription is { } line
                ? line with
                {
                    Source = Name(line.Source),
                    Target = Name(line.Target),
                }
                : null,
            item.Problem is { NodeName: { } nodeName } problem
                ? problem with
                {
                    NodeName = Name(nodeName),
                }
                : item.Problem,
            item.Source
        );
    }

    static NodeProperties Renamed(NodeProperties properties, Func<string, string> name) =>
        properties.Parent is { } parent && name(parent) != parent ? properties.WithParent(name(parent)) : properties;
}
