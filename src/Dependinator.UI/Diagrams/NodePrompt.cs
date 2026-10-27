using System.Text;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using static Dependinator.UI.Diagrams.PromptParts;
using NodeType = Dependinator.Core.Parsing.NodeType;

namespace Dependinator.UI.Diagrams;

// What the About dialog shows of a node before any AI is asked: its comment, what it contains,
// and how many types outside it it uses and are used by it.
public record NodeFacts(
    string Kind,
    string LongName,
    string? Description,
    string Contents,
    int UsesCount,
    int UsedByCount
);

// The text that asks an AI a question about one node (a project, namespace, type, module or
// resource), "What is this and why is it here?" by default: the node with its comment and its
// parent's, what it contains, the types outside it that it uses and that use it (most links
// first), and where the host can read the files, a type's source. A container's source is too
// large to send, so its files are listed instead, for an AI that can read the code (Claude Code).
static class NodePrompt
{
    public const string DefaultQuestion = "What is this and why is it here?";

    const int MaxChildren = 40;
    const int MaxUnitsPerDirection = 25;
    const int MaxFiles = 20;
    const int MaxAnswerWords = 250;

    public static NodeFacts Facts(Node node)
    {
        var descendants = node.DescendantsAndSelfPreOrder().Skip(1).ToList();
        List<string> parts = [];
        AddCount(parts, descendants.Count(n => n.Type == NodeType.Namespace), "namespace");
        AddCount(parts, descendants.Count(n => n.Type.IsType), "type");
        AddCount(parts, descendants.Count(n => n.Type == NodeType.Module), "module");
        AddCount(parts, descendants.Count(n => n.Type == NodeType.Resource), "resource");
        AddCount(parts, descendants.Count(n => n.Type.IsMember && n.Parent == node), "member");
        if (parts.Count == 0 && node.Children.Count > 0)
            parts.Add(Count(node.Children.Count, "child", "children"));
        var contents = parts.Count > 0 ? $"Contains {string.Join(", ", parts)}" : "Contains nothing parsed";
        return new(
            NodeKind(node.Type),
            node.LongName,
            node.Description,
            contents,
            UnitCount(node, n => n.SourceLinks.Select(l => l.Target)),
            UnitCount(node, n => n.TargetLinks.Select(l => l.Source))
        );
    }

    static void AddCount(List<string> parts, int count, string noun)
    {
        if (count > 0)
            parts.Add(Count(count, noun));
    }

    // The code to show with the prompt: a type's own source, with the lines of its members for a
    // long one, or a member's type with that member's line; a container's is too large.
    public static IReadOnlyList<SourceRequest> GetSourceRequests(Node node)
    {
        if (HasSource(node))
        {
            var span = node.FileSpan!;
            List<int> memberLines =
            [
                .. node
                    .Children.Where(c => c.Type.IsMember && c.FileSpan?.Path == span.Path)
                    .Select(c => c.FileSpan!.StartLine),
            ];
            return [new SourceRequest(node.LongName, [new SourceUnit(span, memberLines)])];
        }
        if (node.Type.IsMember && node.FileSpan is { } memberSpan && Unit(node) is var unit && HasSource(unit))
            return [new SourceRequest(node.LongName, [new SourceUnit(unit.FileSpan!, [memberSpan.StartLine])])];
        return [];
    }

    public static string Build(IModel model, Node node, string question, IReadOnlyList<SourceSection>? sources = null)
    {
        var text = new StringBuilder();
        text.AppendLine(
            "I use Dependinator, a tool that maps the structure and dependencies of a codebase as a diagram of "
                + "nested nodes (projects, namespaces, types) and the links between them. I have a question about "
                + "one of its nodes."
        );
        // The prompt may be pasted into an AI that can change the code (Claude Code, say).
        text.AppendLine()
            .AppendLine(
                "This is a question only: do not change, create or commit any files, and do not run commands that "
                    + "do. If you can read the code, use it to check your answer."
            );
        if (Codebase(node) is { } codebase)
            text.AppendLine().AppendLine(codebase);

        AppendNode(text, model, node);
        AppendChildren(text, node);
        AppendUnits(text, "What it uses", "Types outside it that it uses, most links first:", Uses(node));
        AppendUnits(text, "What uses it", "Types outside it that use it, most links first:", UsedBy(node));

        if (sources is { Count: > 0 })
            AppendSource(text, model, sources, "The code of the node.");
        else
            AppendFiles(text, model, node);

        text.AppendLine().AppendLine("## My question").AppendLine();
        text.AppendLine(string.IsNullOrWhiteSpace(question) ? DefaultQuestion : question.Trim());
        text.AppendLine().Append(TaskSection);
        return text.ToString();
    }

    static readonly string TaskSection = $"""
        ## How to answer

        Answer the question for a developer who is new to this codebase: what the node is for, the role it plays among the parts it uses and that use it, and anything worth knowing about it (it is central to much of the code, say, or it mixes responsibilities). Prefer what the code and the links show over what the names suggest, and say so when you are guessing.

        Write plain text in short paragraphs, with "- " lists where they help; **bold** and `code` are fine, headings and tables are not. Keep it under about {MaxAnswerWords} words, unless the question needs more. If the information is not enough to answer, say what you would need to see instead of guessing.

        """;

    static void AppendNode(StringBuilder text, IModel model, Node node)
    {
        text.AppendLine().AppendLine("## The node").AppendLine();
        text.AppendLine($"{NodeKind(node.Type)} {node.LongName}");
        text.AppendLine(
            node.Description is { } description
                ? $"Its comment in the code: \"{Clip(description)}\""
                : "It has no comment in the code."
        );
        if (node.Parent is { IsRoot: false } parent)
            text.AppendLine($"It is in {Kind(parent.Type)} {parent.LongName}{Described(parent)}");
        if (node.FileSpan is { } span)
            text.AppendLine($"Declared in {DisplayPath(model, span.Path)}, line {span.StartLine + 1}");
    }

    static void AppendChildren(StringBuilder text, Node node)
    {
        var children = node.Children.Where(c => !c.IsNote && !c.IsZone).ToList();
        if (children.Count == 0)
            return;
        text.AppendLine().AppendLine("## What it contains").AppendLine();
        foreach (var child in children.OrderBy(c => c.ShortName, StringComparer.Ordinal).Take(MaxChildren))
        {
            var name = child.Type.IsMember ? MemberLabel(child) : Name(child, node);
            var kind = NodeKind(child.Type);
            var label = name == kind ? name : $"{kind} {name}";
            text.AppendLine($"- {label}{Described(child)}");
        }
        if (children.Count > MaxChildren)
            text.AppendLine($"- … and {children.Count - MaxChildren} more");
    }

    static void AppendUnits(StringBuilder text, string title, string intro, IReadOnlyList<(Node Unit, int Links)> units)
    {
        if (units.Count == 0)
            return;
        text.AppendLine().AppendLine($"## {title}").AppendLine();
        text.AppendLine(intro);
        foreach (var (unit, links) in units.Take(MaxUnitsPerDirection))
            text.AppendLine($"- {Kind(unit.Type)} {unit.LongName} ({Count(links, "link")}){Described(unit)}");
        if (units.Count > MaxUnitsPerDirection)
            text.AppendLine($"- … and {units.Count - MaxUnitsPerDirection} more");
    }

    // A container's files, where an AI that can read the code finds it.
    static void AppendFiles(StringBuilder text, IModel model, Node node)
    {
        var files = node.DescendantsAndSelfPreOrder()
            .Where(HasSource)
            .Select(n => DisplayPath(model, n.FileSpan!.Path))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
            return;
        text.AppendLine().AppendLine("## Where its code is").AppendLine();
        foreach (var file in files.Take(MaxFiles))
            text.AppendLine($"- {file}");
        if (files.Count > MaxFiles)
            text.AppendLine($"- … and {files.Count - MaxFiles} more files");
    }

    // The types outside the node that its code links to, with the number of links, most first.
    internal static IReadOnlyList<(Node Unit, int Links)> Uses(Node node) =>
        Grouped(node.DescendantsAndSelfPreOrder().SelectMany(n => n.SourceLinks).Select(l => l.Target), node);

    // The types outside the node whose code links to it.
    internal static IReadOnlyList<(Node Unit, int Links)> UsedBy(Node node) =>
        Grouped(node.DescendantsAndSelfPreOrder().SelectMany(n => n.TargetLinks).Select(l => l.Source), node);

    // By the type at each far end, leaving out the node itself, what is inside it, and what it is
    // inside: a method's call to another method of its type is no link to a type outside it.
    static IReadOnlyList<(Node Unit, int Links)> Grouped(IEnumerable<Node> ends, Node node) =>
        [
            .. ends.Where(n => !IsInside(n, node))
                .GroupBy(Unit)
                .Where(g => !IsInside(g.Key, node) && !IsInside(node, g.Key))
                .Select(g => (Unit: g.Key, Links: g.Count()))
                .OrderByDescending(u => u.Links)
                .ThenBy(u => u.Unit.LongName, StringComparer.Ordinal),
        ];

    // The number of types outside the node at the far ends of its links (as Grouped, unsorted).
    static int UnitCount(Node node, Func<Node, IEnumerable<Node>> ends)
    {
        HashSet<Node> units = [];
        foreach (var end in node.DescendantsAndSelfPreOrder().SelectMany(ends))
        {
            if (!IsInside(end, node) && Unit(end) is var unit && !IsInside(unit, node) && !IsInside(node, unit))
                units.Add(unit);
        }
        return units.Count;
    }

    static bool IsInside(Node other, Node node) => other == node || other.Ancestors().Contains(node);

    static string NodeKind(NodeType type) =>
        type switch
        {
            NodeType.FieldMember => "field",
            NodeType.ConstructorMember => "constructor",
            NodeType.EventMember => "event",
            NodeType.PropertyMember => "property",
            NodeType.MethodMember => "method",
            NodeType.ParameterMember => "parameter",
            NodeType.VariableMember => "variable",
            NodeType.OutputMember => "output",
            NodeType.ResourceMember => "resource",
            _ => Kind(type),
        };

    static string Count(int count, string noun, string? plural = null) =>
        $"{count} {(count == 1 ? noun : plural ?? noun + "s")}";
}
