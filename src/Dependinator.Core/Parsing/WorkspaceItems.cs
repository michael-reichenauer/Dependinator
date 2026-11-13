using Dependinator.Core.Shared;

namespace Dependinator.Core.Parsing;

// Makes one model of the items of a workspace model's sources, each parsed by its own parser.
// The root node of every source ends up side by side under the model's root, with the names its
// own model has, so what is true of a node there (its source file, a rule, a search) is true here.
//   - A source that failed is a problem item, and the others still make the model.
//   - A source whose root is named like a node before it (two "main.bicep", two project folders
//     named "app") gets its folder in the name of its root, and so in the names of all its nodes.
//   - A node that an earlier source already has is left to that source: a project that two
//     solutions share is shown once, in the first.
static class WorkspaceItems
{
    public static Result<IReadOnlyList<Item>> Combine(
        string workspacePath,
        IReadOnlyList<(string Path, Result<IReadOnlyList<Item>> Result)> sources
    )
    {
        if (sources.Count == 0)
            return new Error("No sources are chosen for this workspace model.");

        var folder = ModelPaths.WorkspaceFolder(workspacePath);
        List<Item> combined = [];
        HashSet<string> nodeNames = new(StringComparer.Ordinal);
        HashSet<(string Message, string? NodeName)> problems = [];
        List<Error> errors = [];

        foreach (var (path, result) in sources)
        {
            if (result is not IReadOnlyList<Item> parsed)
            {
                errors.Add(result.Error);
                var message = $"{ModelPaths.DisplayName(path)} could not be parsed. {result.Error.Message}";
                combined.Add(new Item(null, null, Problem: new ParseProblem(message, 1)));
                continue;
            }

            var items = parsed;
            var rootName = RootName(items);
            if (rootName is not null && nodeNames.Contains(rootName))
            {
                var newRootName = UniqueRootName(rootName, WorkspaceSources.Key(folder, path), nodeNames);
                items = [.. items.Select(item => Renamed(item, rootName, newRootName))];
                rootName = newRootName;
            }

            foreach (var item in items)
            {
                // A node the model already has stays as the earlier source made it; a link to it
                // from this source leads to that node.
                if (item.Node is not null && !nodeNames.Add(item.Node.Name))
                    continue;
                if (item.Problem is not null && !problems.Add((item.Problem.Message, item.Problem.NodeName)))
                    continue;
                combined.Add(item);
            }

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

    // The node all the nodes of a source are under: every parser makes one of the solution type.
    static string? RootName(IReadOnlyList<Item> items) =>
        items.Select(item => item.Node).FirstOrDefault(node => node?.Properties.Type == NodeType.Solution)?.Name;

    // "app (services∕app)": the root's name with the source's folder below the workspace, in the
    // brackets that keep it one part of the name. The folder is written with a division slash
    // and "*" for its dots, since "/" and "." would split the name into parts.
    static string UniqueRootName(string rootName, string sourceKey, HashSet<string> nodeNames)
    {
        var sourceFolder = WorkspaceSources
            .FolderOf(sourceKey)
            .Replace('/', '∕')
            .Replace('.', '*')
            .Replace('$', '＄')
            .Replace('#', '＃')
            .Replace("(", "")
            .Replace(")", "");

        var name = sourceFolder == "" ? rootName : $"{rootName} ({sourceFolder})";
        for (var number = 2; nodeNames.Contains(name); number++)
            name = $"{rootName} ({(sourceFolder == "" ? "" : $"{sourceFolder} ")}{number})";
        return name;
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
