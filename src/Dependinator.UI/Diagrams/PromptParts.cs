using System.Text;
using Dependinator.Core.Shared;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using NodeType = Dependinator.Core.Parsing.NodeType;

namespace Dependinator.UI.Diagrams;

// The parts the AI prompts share (CyclePrompt, RulePrompt): names relative to a container, the
// unit and member a node belongs to, the links between two parts listed per member or per type,
// descriptions on one line, and the source excerpts with paths relative to the model's folder.
static class PromptParts
{
    const int MaxDetailedSources = 12;
    const int MaxLinesPerDirection = 30;
    const int MaxTargetMembers = 8;
    const int MaxDescriptionLength = 300;
    const int MaxSourceLines = 400;

    // A type's span, and the declaration lines of its members that make the links.
    internal static SourceUnit SourceOf(IGrouping<Node, Link> linksOfType)
    {
        var span = linksOfType.Key.FileSpan!;
        var memberLines = linksOfType
            .Select(l => Member(l.Source))
            .Where(m => m != linksOfType.Key && m.FileSpan?.Path == span.Path)
            .Select(m => m.FileSpan!.StartLine)
            .Distinct();
        return new SourceUnit(span, [.. memberLines]);
    }

    // Types, and the modules and resources of infrastructure models, have spans worth showing.
    internal static bool HasSource(Node node) =>
        node.FileSpan is not null && (node.Type.IsType || node.Type is NodeType.Module or NodeType.Resource);

    internal static void AppendSource(
        StringBuilder text,
        IModel model,
        IReadOnlyList<SourceSection> sources,
        string intro
    )
    {
        text.AppendLine().AppendLine("## Source").AppendLine();
        text.AppendLine(intro);
        var shownLines = 0;
        foreach (var source in sources)
        {
            text.AppendLine().AppendLine($"### {source.Direction}");
            foreach (var excerpt in source.Excerpts)
            {
                if (shownLines + excerpt.Lines.Count > MaxSourceLines)
                {
                    text.AppendLine().AppendLine("(The rest of the source is left out.)");
                    return;
                }
                shownLines += excerpt.Lines.Count;
                text.AppendLine();
                text.AppendLine($"{DisplayPath(model, excerpt.Path)}, lines {excerpt.StartLine}–{excerpt.EndLine}:");
                text.AppendLine().AppendLine($"```{FenceLanguage(excerpt.Path)}");
                foreach (var line in excerpt.Lines)
                    text.AppendLine(line);
                text.AppendLine("```");
            }
        }
    }

    // A path relative to the model's folder (where the .sln is), when inside it.
    internal static string DisplayPath(IModel model, string path)
    {
        var folder = Path.GetDirectoryName(model.Path);
        if (string.IsNullOrEmpty(folder))
            return path;
        var relative = Path.GetRelativePath(folder, path);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? path : relative;
    }

    internal static string FenceLanguage(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".bicep" => "bicep",
            _ => "",
        };

    // The codebase a node is in: the model's top-level node above it (the solution, say).
    internal static string? Codebase(Node container)
    {
        var top = container.AncestorsAndSelf().FirstOrDefault(n => n.Parent is { IsRoot: true });
        if (top is null)
            return null;
        return top.Description is { } description
            ? $"The codebase is {top.DisplayName}: {Clip(description)}"
            : $"The codebase is {top.DisplayName}.";
    }

    internal static void AppendLinks(StringBuilder text, IReadOnlyList<Link> links, Node container)
    {
        // By name rather than node: nodes can share a display name (overloads, say), and a line
        // per node would repeat it.
        var bySource = links
            .GroupBy(l => Name(Member(l.Source), container))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();
        if (bySource.Count <= MaxDetailedSources)
        {
            foreach (var group in bySource)
            {
                text.AppendLine($"- {group.Key} → {Targets(group, container)}");
                if (
                    group.Select(l => Member(l.Source).Description).FirstOrDefault(d => d is not null) is
                    { } description
                )
                    text.AppendLine($"  \"{Clip(description)}\"");
            }
            return;
        }

        var byType = links
            .GroupBy(l => Name(Unit(l.Source), container))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();
        foreach (var group in byType.Take(MaxLinesPerDirection))
        {
            var targets = group.Select(l => Name(Unit(l.Target), container)).Distinct().Order(StringComparer.Ordinal);
            text.AppendLine($"- {group.Key} → {string.Join(", ", targets)}");
        }
        if (byType.Count > MaxLinesPerDirection)
            text.AppendLine($"- … and {byType.Count - MaxLinesPerDirection} more");
    }

    // The targets of one source's links, grouped by type, with the members used in parentheses:
    // "Parsing.NodeType (FieldMember, MethodMember)".
    internal static string Targets(IEnumerable<Link> links, Node container)
    {
        var byType = links
            .Select(l => l.Target)
            .GroupBy(t => Name(Unit(t), container))
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        return string.Join(
            ", ",
            byType.Select(group =>
            {
                List<string> used =
                [
                    .. group
                        .Where(t => t.Type.IsMember)
                        .Select(t => MemberLabel(Member(t)))
                        .Distinct()
                        .Order(StringComparer.OrdinalIgnoreCase),
                ];
                if (used.Count == 0)
                    return group.Key;
                IReadOnlyList<string> shown =
                    used.Count <= MaxTargetMembers ? used : [.. used.Take(MaxTargetMembers), "…"];
                return $"{group.Key} ({string.Join(", ", shown)})";
            })
        );
    }

    // A used member by its name alone: overloads read as one, and their parameters are noise here.
    // A model parsed before the C# parser typed constructors (one opened where it is not parsed
    // again, as in the standalone web app) has them as methods named after their type.
    internal static string MemberLabel(Node member)
    {
        var unit = Unit(member);
        var name = Name(member, unit).Split('(')[0];
        return member.Type == NodeType.ConstructorMember || name == unit.ShortName.Split('<')[0] ? "constructor" : name;
    }

    // The closest node that is not a member: a member's type, or the node itself. In a model parsed
    // before conversion operators were named without the dots of their target type
    // ("implicitoperatorNs.Result"), made-up parents are between the operator and its type and are
    // passed. In a model without types (tests), the closest node that is not a member.
    internal static Node Unit(Node node)
    {
        var nonMembers = node.AncestorsAndSelf().Where(n => !n.Type.IsMember).ToList();
        return nonMembers.FirstOrDefault(n => n.Type is not (NodeType.None or NodeType.Root or NodeType.Parent))
            ?? nonMembers[0];
    }

    // The member the node is part of (a getter or a local function counts as its property or
    // method), or the node itself when it is not a member.
    internal static Node Member(Node node)
    {
        if (!node.Type.IsMember)
            return node;
        while (node.Parent is { Type.IsMember: true } parent)
            node = parent;
        return node;
    }

    // The node's name relative to the given ancestor, its full name otherwise. A long name has no
    // namespaces in brackets: "Result<IParser>", not "Result<global::Demo.IParser>".
    internal static string Name(Node node, Node relativeTo)
    {
        var prefix = relativeTo.LongName + ".";
        return !relativeTo.IsRoot && node.LongName.StartsWith(prefix, StringComparison.Ordinal)
            ? node.LongName[prefix.Length..]
            : node.LongName;
    }

    internal static string Described(Node node) =>
        node.Description is { } description ? $": \"{Clip(description)}\"" : "";

    // A description on one line; a long one is cut after its last sentence that fits, or at a word.
    internal static string Clip(string description)
    {
        var text = string.Join(" ", description.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length <= MaxDescriptionLength)
            return text;
        var sentenceEnd = text.LastIndexOf(". ", MaxDescriptionLength, StringComparison.Ordinal);
        if (sentenceEnd > MaxDescriptionLength / 2)
            return text[..(sentenceEnd + 1)];
        var wordEnd = text.LastIndexOf(' ', MaxDescriptionLength);
        return text[..(wordEnd > 0 ? wordEnd : MaxDescriptionLength)] + "…";
    }

    internal static string Kind(NodeType type) =>
        type switch
        {
            NodeType.Solution => "solution",
            NodeType.SolutionFolder => "solution folder",
            NodeType.Assembly or NodeType.Dll or NodeType.Exe => "project",
            NodeType.Externals => "external references",
            NodeType.Namespace => "namespace",
            NodeType.ClassType => "class",
            NodeType.InterfaceType => "interface",
            NodeType.EnumType => "enum",
            NodeType.StructType => "struct",
            NodeType.RecordType => "record",
            NodeType.Type => "type",
            NodeType.Module => "module",
            NodeType.Resource => "resource",
            _ => "group",
        };

    // The web host and the VS Code extension see the workspace files; the standalone browser
    // app has none.
    internal static bool CanReadSource => Dependinator.Core.Build.IsWeb || Dependinator.Core.Build.IsVsCodeExtWasm;

    // Each excerpt once (two types in a short file both show all of it), under its first direction.
    internal static async Task<IReadOnlyList<SourceSection>> ReadSourcesAsync(
        IWorkspaceFileService workspaceFileService,
        IReadOnlyList<SourceRequest> requests
    )
    {
        List<SourceSection> sections = [];
        HashSet<(string, int)> shown = [];
        foreach (var request in requests)
        {
            List<SourceExcerpt> excerpts = [];
            foreach (var unit in request.Units)
            {
                var text = await workspaceFileService.ReadSourceFileAsync(unit.Span.Path);
                if (text is not string fileText)
                {
                    Log.Info("No source for the prompt", text.Error.Message);
                    continue;
                }
                excerpts.AddRange(SourceExcerpts.Cut(unit, fileText).Where(e => shown.Add((e.Path, e.StartLine))));
            }
            if (excerpts.Count > 0)
                sections.Add(new SourceSection(request.Direction, excerpts));
        }
        return sections;
    }
}
