using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Cycles;
using Dependinator.UI.Diagrams.Icons;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Diagrams.Rules;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams.Tooltips;

// The hover card of a node or line (initTooltips in jsInterop.js shows it, asking through Canvas.GetTooltipHtml). It is built only when the pointer rests
// on an item, one item at a time, so the diagram tiles carry no tooltip text at all: a node's
// kind, where it is, its description, what it uses and is used by, and its problems; a line's
// ends, link count and first links, and whether it is part of a cycle or breaks a rule.
interface ITooltipService
{
    // The card's inner HTML for a hover group's element id ("<id>.n" or "<id>.l"), or null when
    // there is nothing to show (the item is gone, or it is not a node or line).
    string? GetHtml(string elementId);
}

[Scoped]
partial class TooltipService(IModelMgr modelMgr, ICycleService cycleService, IRuleService ruleService) : ITooltipService
{
    const int MaxDescriptionLength = 400;
    const int ShownLinks = 3;

    // The dependency counts per node, for the structure version they were counted at: resting on
    // a large container walks all links inside it once, not on every pause.
    readonly Dictionary<NodeId, (int Uses, int UsedBy)> dependencyCounts = [];
    (int Structure, int Nodes, int Links) countsVersion = (-1, -1, -1);

    public string? GetHtml(string elementId)
    {
        var pointerId = PointerId.Parse(elementId);
        if (pointerId.IsNode)
            return NodeHtml(pointerId.NodeId);
        if (pointerId.IsLine)
            return LineHtml(pointerId.LineId);
        return null;
    }

    string? NodeHtml(NodeId nodeId)
    {
        // The problem services take the model themselves, so they are asked before it is held.
        var cycles = cycleService.GetCycles();
        var brokenRules = ruleService.GetReports().Where(r => r.Violations.Count > 0).ToList();

        using var model = modelMgr.UseModel();
        if (!model.Nodes.TryGetValue(nodeId, out var node) || node.IsRoot)
            return null;

        var html = new StringBuilder();
        html.Append("<div class=\"dep-tt-head\">");
        if (!node.IsNote && !node.IsZone)
            html.Append(
                $"<svg class=\"dep-tt-icon\" width=\"22\" height=\"22\"><use href=\"#{Icon.GetIconName(node)}\" width=\"22\" height=\"22\"/></svg>"
            );
        html.Append($"<div><div class=\"dep-tt-name\">{node.HtmlDisplayName}</div>");
        html.Append($"<div class=\"dep-tt-kind\">{KindLineHtml(node)}</div></div></div>");

        // A zone's label is its alias over a generated name the user never chose.
        if (node.HtmlAlias is not null && !node.IsZone && !node.IsNote)
            html.Append($"<div class=\"dep-tt-kind dep-tt-real\">{node.HtmlLongName}</div>");
        if (!string.IsNullOrEmpty(node.Signature))
            html.Append($"<div class=\"dep-tt-code\">{DeclarationHtml(node)}</div>");
        if (!string.IsNullOrWhiteSpace(node.Description))
        {
            var isUserText = node.IsNote || node.IsZone || node.IsManual;
            var description = isUserText ? node.Description : Flow(node.Description);
            html.Append($"<div class=\"dep-tt-desc\">{HttpUtility.HtmlEncode(Shorten(description))}</div>");
        }

        List<string> chips = [];
        if (!node.IsNote && !node.IsZone)
        {
            // Keyed like ProblemService: the counts too, not only the structure version.
            var version = (model.StructureVersion, model.Nodes.Count, model.Links.Count);
            if (countsVersion != version)
            {
                dependencyCounts.Clear();
                countsVersion = version;
            }
            if (!dependencyCounts.TryGetValue(node.Id, out var counts))
                dependencyCounts[node.Id] = counts = CountDependencies(node);
            var (uses, usedBy) = counts;
            if (uses > 0)
                chips.Add(Chip($"uses <b>{uses}</b>"));
            if (usedBy > 0)
                chips.Add(Chip($"used by <b>{usedBy}</b>"));
            if (node.Children.Count > 0)
                chips.Add(
                    Chip(
                        node.Type.IsType
                            ? $"<b>{node.Children.Count}</b> {Plural(node.Children.Count, "member")}"
                            : $"contains <b>{node.Children.Count}</b>"
                    )
                );
        }
        AddRow(html, chips);

        AddRow(html, ProblemChips(model, node, cycles, brokenRules));

        return html.ToString();
    }

    string? LineHtml(LineId lineId)
    {
        var isCyclic = cycleService.GetCyclicLineIds().Contains(lineId);
        var isViolation = ruleService.GetViolatingLineIds().Contains(lineId);

        using var model = modelMgr.UseModel();
        if (!model.Lines.TryGetValue(lineId, out var line))
            return null;

        var html = new StringBuilder();
        var count = line.Links.Count;
        var isManual = count > 0 && line.Links.All(l => l.IsManual);
        // A line between a container's edge and one of its children (drawn as a stub at the
        // child) stands for the links that cross the container's border, so the container's end
        // reads "outside X"; the links listed below name the real ends.
        var crossesBorder = LineStub.CrossesBorder(line);
        var sourceName =
            crossesBorder && LineStub.IsIncoming(line)
                ? $"<span class=\"dep-tt-outside\">outside</span> {line.Source.HtmlDisplayName}"
                : line.Source.HtmlDisplayName;
        var targetName =
            crossesBorder && LineStub.IsOutgoing(line)
                ? $"<span class=\"dep-tt-outside\">outside</span> {line.Target.HtmlDisplayName}"
                : line.Target.HtmlDisplayName;
        html.Append(
            $"<div class=\"dep-tt-name\">{sourceName}<span class=\"dep-tt-arrow\">→</span><wbr>{targetName}</div>"
        );
        var parent = line.Source.Parent == line.Target.Parent && !line.Source.Parent.IsRoot ? line.Source.Parent : null;
        var kind =
            (isManual || count == 0 ? "drawn by hand" : $"{count} {Plural(count, "link")}")
            + (parent is null ? "" : $" in {Breakable(parent.LongName)}");
        html.Append($"<div class=\"dep-tt-kind\">{kind}</div>");

        if (line.HtmlLabel is not null)
            html.Append($"<div class=\"dep-tt-desc\"><b>{line.HtmlLabel}</b></div>");
        if (!string.IsNullOrWhiteSpace(line.LabelDescription))
            html.Append($"<div class=\"dep-tt-desc\">{HttpUtility.HtmlEncode(Shorten(line.LabelDescription))}</div>");
        if (!string.IsNullOrWhiteSpace(line.Description))
            html.Append($"<div class=\"dep-tt-desc\">{HttpUtility.HtmlEncode(Shorten(line.Description))}</div>");

        // A hand-drawn line's one link would only repeat its two ends.
        if (count > 0 && !isManual)
        {
            var links = line
                .Links.Select(l => (Source: LinkEndName(l.Source), Target: LinkEndName(l.Target)))
                .Distinct()
                .OrderBy(l => l.Source, StringComparer.Ordinal)
                .ThenBy(l => l.Target, StringComparer.Ordinal)
                .ToList();
            html.Append("<div class=\"dep-tt-sec\">");
            foreach (var (source, target) in links.Take(ShownLinks))
                html.Append(
                    $"<div class=\"dep-tt-link\"><code>{HttpUtility.HtmlEncode(source)}</code><span>→&nbsp;{HttpUtility.HtmlEncode(target)}</span></div>"
                );
            if (links.Count > ShownLinks)
                html.Append($"<div class=\"dep-tt-more\">+ {links.Count - ShownLinks} more</div>");
            html.Append("</div>");
        }

        List<string> chips = [];
        if (isCyclic)
            chips.Add(Chip("⚠ part of a cycle", "dep-tt-warn"));
        if (isViolation)
            chips.Add(Chip("✕ breaks a rule", "dep-tt-err"));
        AddRow(html, chips);

        return html.ToString();
    }

    // "Class in Demo.UI.Modeling": the node's kind, and the container it is in (none at the top).
    static string KindLineHtml(Node node)
    {
        var kind = HttpUtility.HtmlEncode(Kind(node));
        return node.Parent.IsRoot ? kind : $"{kind} in {Breakable(node.Parent.LongName)}";
    }

    // A member as it is declared in code, its name in bold: "Task<Rect?> SelectAreaAsync(Purpose
    // purpose)", "int Count", "param location string" in Bicep. The parsers give the signature
    // the way a member row shows it after the name, UML style: "(Purpose purpose): Task<Rect?>",
    // ": int" (MemberParser.Signature). One that is not of that form is shown as it is.
    internal static string DeclarationHtml(Node node)
    {
        var signature = node.Signature ?? "";
        var name = $"<b>{node.HtmlShortName}</b>";

        // An enum's member is declared by its name alone
        if (node.Parent.Type == Parsing.NodeType.EnumType)
            return name;

        // TypeScript and JavaScript declare a member the way its row shows it, the type last:
        // "start(port?: number): Promise<void>", "limit: number"
        if (node.FileSpan is { } span && ModelPaths.IsScriptFile(span.Path))
            return signature.StartsWith('(') ? $"{name}{ParametersAndTypeHtml(signature)}" : $"{name}{Code(signature)}";

        if (signature.StartsWith(": "))
        {
            var type = Code(signature[2..]);
            return node.Type switch
            {
                Parsing.NodeType.ParameterMember => $"param&nbsp;{name}&nbsp;{type}",
                Parsing.NodeType.OutputMember => $"output&nbsp;{name}&nbsp;{type}",
                Parsing.NodeType.EventMember => $"event&nbsp;{type}&nbsp;{name}",
                _ => $"{type}&nbsp;{name}",
            };
        }

        if (signature.StartsWith('(') || signature.StartsWith('['))
        {
            var end = ClosingBracketIndex(signature);
            var rest = signature[(end + 1)..];
            if (end > 0 && rest.StartsWith(": "))
            {
                // An indexer is declared "int this[int index]"
                var declared = signature[0] == '[' ? "this" : name;
                return $"{Code(rest[2..])}&nbsp;{declared}{ParametersHtml(signature[..(end + 1)])}";
            }
            if (end > 0 && rest == "" && signature[0] == '(')
            {
                var parameters = ParametersHtml(signature);
                // A constructor or finalizer ("~Type") has no return type
                return node.Type == Parsing.NodeType.ConstructorMember || node.ShortName.StartsWith('~')
                    ? $"{name}{parameters}"
                    : $"void&nbsp;{name}{parameters}";
            }
        }

        return HttpUtility.HtmlEncode(signature);
    }

    // "(port?: number): Promise<void>": the parameters, wrapping between them, then the rest.
    static string ParametersAndTypeHtml(string signature)
    {
        var end = ClosingBracketIndex(signature);
        return end > 0
            ? $"{ParametersHtml(signature[..(end + 1)])}{Code(signature[(end + 1)..])}"
            : HttpUtility.HtmlEncode(signature);
    }

    // A long declaration wraps like formatted code: after the opening bracket and between the
    // parameters, never between a type and its name or inside a generic or tuple type.
    static string ParametersHtml(string parameters)
    {
        if (parameters.Length <= 2)
            return HttpUtility.HtmlEncode(parameters);

        var html = new StringBuilder($"{parameters[0]}<wbr>");
        var depth = 0;
        var start = 1;
        for (var i = 1; i < parameters.Length; i++)
        {
            var c = parameters[i];
            if (c is '(' or '[' or '<')
                depth++;
            else if (c is ')' or ']' or '>')
                depth--;
            else if (c == ',' && depth == 0 && i + 1 < parameters.Length && parameters[i + 1] == ' ')
            {
                html.Append(Code(parameters[start..(i + 1)])).Append(' ');
                start = i + 2;
            }
        }
        return html.Append(Code(parameters[start..])).ToString();
    }

    static string Code(string text) => HttpUtility.HtmlEncode(text).Replace(" ", "&nbsp;");

    // Where the bracket that opens the text closes: tuple, array and generic types nest inside
    // a parameter list. -1 when it never closes.
    static int ClosingBracketIndex(string text)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '(' or '[')
                depth++;
            else if (text[i] is ')' or ']' && --depth == 0)
                return i;
        }
        return -1;
    }

    // A dotted name that wraps after a dot, not in the middle of a word.
    static string Breakable(string name) => HttpUtility.HtmlEncode(name).Replace(".", ".<wbr>");

    static string Kind(Node node)
    {
        if (node.IsNote)
            return "Note";
        if (node.IsZone)
            return "Zone";
        return node.Type switch
        {
            // The Bicep parser makes the entry .bicep file the solution node ("main*bicep"), and
            // the TypeScript parser the project, which points at its tsconfig.json or package.json
            Parsing.NodeType.Solution => node.Name.EndsWith("*bicep", StringComparison.OrdinalIgnoreCase)
                ? "Bicep deployment"
            : node.FileSpan is { } span && ModelPaths.IsTypeScript(span.Path)
                ? Capitalized(ModelPaths.KindName(span.Path))
            : "Solution",
            Parsing.NodeType.SolutionFolder => "Solution folder",
            Parsing.NodeType.Assembly or Parsing.NodeType.Dll => "Project",
            Parsing.NodeType.Exe => "Executable project",
            Parsing.NodeType.Externals => "External references",
            Parsing.NodeType.Namespace => "Namespace",
            Parsing.NodeType.ClassType => "Class",
            Parsing.NodeType.InterfaceType => "Interface",
            Parsing.NodeType.EnumType => "Enum",
            Parsing.NodeType.StructType => "Struct",
            Parsing.NodeType.RecordType => "Record",
            Parsing.NodeType.Type => "Type",
            Parsing.NodeType.FieldMember => "Field",
            Parsing.NodeType.ConstructorMember => "Constructor",
            Parsing.NodeType.EventMember => "Event",
            Parsing.NodeType.PropertyMember => "Property",
            Parsing.NodeType.MethodMember => "Method",
            Parsing.NodeType.Module => "Module",
            Parsing.NodeType.Resource => "Resource",
            Parsing.NodeType.ParameterMember => "Parameter",
            Parsing.NodeType.VariableMember => "Variable",
            Parsing.NodeType.OutputMember => "Output",
            Parsing.NodeType.ResourceMember => "Resource setting",
            Parsing.NodeType.Folder => "Folder",
            Parsing.NodeType.SourceFile => "File",
            Parsing.NodeType.FunctionMember => "Function",
            _ => node.IsManual ? "Node" : "Group",
        };
    }

    static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // How many other nodes the node (or anything inside it) uses and is used by, outside itself.
    // A member counts as its type, so calls into five methods of one class count as one use, and a
    // member's calls to the other members of its own type are no uses at all.
    internal static (int Uses, int UsedBy) CountDependencies(Node node)
    {
        var self = Unit(node);
        HashSet<Node> uses = [];
        HashSet<Node> usedBy = [];
        Stack<Node> pending = new([node]);
        while (pending.TryPop(out var current))
        {
            foreach (var link in current.SourceLinks)
                if (!IsInside(link.Target, node) && Unit(link.Target) is var unit && unit != self)
                    uses.Add(unit);
            foreach (var link in current.TargetLinks)
                if (!IsInside(link.Source, node) && Unit(link.Source) is var unit && unit != self)
                    usedBy.Add(unit);
            foreach (var child in current.Children)
                pending.Push(child);
        }
        return (uses.Count, usedBy.Count);
    }

    // UnitGraph.UnitOf, but a member at the top stands for itself, not for the root.
    static Node Unit(Node node) => UnitGraph.UnitOf(node) is { IsRoot: false } unit ? unit : node;

    static bool IsInside(Node node, Node ancestor)
    {
        for (var n = node; ; n = n.Parent)
        {
            if (n == ancestor)
                return true;
            if (n.IsRoot)
                return false;
        }
    }

    // The node's own problems, and those inside it: compile errors, cycles it is part of or that
    // are inside it, and the rules its dependencies break.
    static List<string> ProblemChips(
        IModel model,
        Node node,
        IReadOnlyList<Cycle> cycles,
        IReadOnlyList<RuleReport> brokenRules
    )
    {
        List<string> chips = [];

        var errors = model
            .ParseProblems.Where(p =>
                p.NodeName is not null
                && model.Nodes.TryGetValue(NodeId.FromName(p.NodeName), out var problemNode)
                && IsCounted(problemNode, node)
            )
            .Sum(p => p.ErrorCount);
        if (errors > 0)
            chips.Add(Chip($"✕ {errors} compile {Plural(errors, "error")}", "dep-tt-err"));

        var inCycles = cycles.Count(c => c.Members.Any(m => m.Id == node.Id));
        if (inCycles > 0)
            chips.Add(Chip($"⚠ in {inCycles} {Plural(inCycles, "cycle")}", "dep-tt-warn"));
        var cyclesInside = cycles.Count(c =>
            model.Nodes.TryGetValue(c.ContainerId, out var container) && IsCounted(container, node)
        );
        if (cyclesInside > 0)
            chips.Add(Chip($"⚠ {cyclesInside} {Plural(cyclesInside, "cycle")} inside", "dep-tt-warn"));

        var rules = brokenRules.Count(r =>
            r.Violations.Any(v => model.Nodes.TryGetValue(v.SourceId, out var source) && IsCounted(source, node))
        );
        if (rules > 0)
            chips.Add(Chip($"✕ breaks {rules} {Plural(rules, "rule")}", "dep-tt-err"));

        return chips;
    }

    // A problem counts where it is, like on the node's badge (ProblemService): not in a hidden
    // node, since there is nothing visible to lead to (the card agrees with the badge).
    static bool IsCounted(Node problemNode, Node node) =>
        problemNode.Visibility != NodeVisibility.Hidden && IsInside(problemNode, node);

    // A link end as "Type.Member" for a member, else its short name.
    static string LinkEndName(Node node) =>
        node.Type.IsMember && !node.Parent.IsRoot ? $"{node.Parent.ShortName}.{node.ShortName}" : node.ShortName;

    static void AddRow(StringBuilder html, List<string> chips)
    {
        if (chips.Count > 0)
            html.Append($"<div class=\"dep-tt-row\">{string.Concat(chips)}</div>");
    }

    static string Chip(string content, string cssClass = "") =>
        $"<span class=\"dep-tt-chip {cssClass}\">{content}</span>";

    static string Plural(int count, string word) => count == 1 ? word : word + "s";

    // A code comment's line breaks are those of the source file, so its lines flow together
    // here; a blank line still starts a new paragraph and a list item its own line. (What the
    // user wrote keeps its lines.)
    internal static string Flow(string text)
    {
        var paragraphs = ParagraphBreaks()
            .Split(text.Replace("\r", "").Trim())
            .Select(paragraph =>
            {
                var flowed = new StringBuilder();
                foreach (var line in paragraph.Split('\n', StringSplitOptions.TrimEntries))
                {
                    if (flowed.Length > 0)
                        flowed.Append(ListItem().IsMatch(line) ? '\n' : ' ');
                    flowed.Append(line);
                }
                return flowed.ToString();
            });
        return string.Join("\n\n", paragraphs);
    }

    static string Shorten(string text)
    {
        text = text.Trim();
        if (text.Length <= MaxDescriptionLength)
            return text;
        var wordEnd = text.LastIndexOf(' ', MaxDescriptionLength);
        return text[..(wordEnd > 0 ? wordEnd : MaxDescriptionLength)] + "…";
    }

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex ParagraphBreaks();

    [GeneratedRegex(@"^([-*•]|\d+[.)])\s")]
    private static partial Regex ListItem();
}
