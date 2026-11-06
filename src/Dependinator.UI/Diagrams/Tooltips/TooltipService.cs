using System.Text;
using System.Web;
using Dependinator.UI.Diagrams.Cycles;
using Dependinator.UI.Diagrams.Icons;
using Dependinator.UI.Diagrams.Interaction;
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
class TooltipService(IModelMgr modelMgr, ICycleService cycleService, IRuleService ruleService) : ITooltipService
{
    const int MaxDescriptionLength = 400;
    const int ShownLinks = 3;

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
        html.Append($"<div class=\"dep-tt-kind\">{HttpUtility.HtmlEncode(KindLine(node))}</div></div></div>");

        if (node.HtmlAlias is not null)
            html.Append($"<div class=\"dep-tt-kind dep-tt-real\">{node.HtmlLongName}</div>");
        if (!string.IsNullOrEmpty(node.Signature))
            html.Append($"<div class=\"dep-tt-code\">{HttpUtility.HtmlEncode(node.Signature)}</div>");
        if (!string.IsNullOrWhiteSpace(node.Description))
            html.Append($"<div class=\"dep-tt-desc\">{HttpUtility.HtmlEncode(Shorten(node.Description))}</div>");

        List<string> chips = [];
        if (!node.IsNote && !node.IsZone)
        {
            var (uses, usedBy) = CountDependencies(node);
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
        html.Append(
            $"<div class=\"dep-tt-name\">{line.Source.HtmlDisplayName}<span class=\"dep-tt-arrow\">→</span>{line.Target.HtmlDisplayName}</div>"
        );
        var parent = line.Source.Parent == line.Target.Parent && !line.Source.Parent.IsRoot ? line.Source.Parent : null;
        var kind =
            (parent is null ? "" : $"{parent.LongName} · ")
            + (count == 0 ? "drawn by hand" : $"{count} {Plural(count, "link")}");
        html.Append($"<div class=\"dep-tt-kind\">{HttpUtility.HtmlEncode(kind)}</div>");

        if (line.HtmlLabel is not null)
            html.Append($"<div class=\"dep-tt-desc\"><b>{line.HtmlLabel}</b></div>");
        if (!string.IsNullOrWhiteSpace(line.HtmlDescription))
            html.Append($"<div class=\"dep-tt-desc\">{line.HtmlDescription}</div>");

        if (count > 0)
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
                    $"<div class=\"dep-tt-link\"><code>{HttpUtility.HtmlEncode(source)}</code><span>→ {HttpUtility.HtmlEncode(target)}</span></div>"
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

    // "Class · Demo.UI.Modeling": the node's kind, and the container it is in (none at the top).
    static string KindLine(Node node)
    {
        var kind = Kind(node);
        return node.Parent.IsRoot ? kind : $"{kind} · {node.Parent.LongName}";
    }

    static string Kind(Node node)
    {
        if (node.IsNote)
            return "Note";
        if (node.IsZone)
            return "Zone";
        return node.Type switch
        {
            Parsing.NodeType.Solution => "Solution",
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
            _ => node.IsManual ? "Node" : "Group",
        };
    }

    // How many other nodes the node (or anything inside it) uses and is used by, outside itself.
    // A member counts as its type, so calls into five methods of one class count as one use.
    internal static (int Uses, int UsedBy) CountDependencies(Node node)
    {
        HashSet<Node> uses = [];
        HashSet<Node> usedBy = [];
        Stack<Node> pending = new([node]);
        while (pending.TryPop(out var current))
        {
            foreach (var link in current.SourceLinks)
                if (!IsInside(link.Target, node))
                    uses.Add(Owner(link.Target));
            foreach (var link in current.TargetLinks)
                if (!IsInside(link.Source, node))
                    usedBy.Add(Owner(link.Source));
            foreach (var child in current.Children)
                pending.Push(child);
        }
        return (uses.Count, usedBy.Count);
    }

    static Node Owner(Node node) => node.Type.IsMember && !node.Parent.IsRoot ? node.Parent : node;

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
                && IsInside(problemNode, node)
            )
            .Sum(p => p.ErrorCount);
        if (errors > 0)
            chips.Add(Chip($"✕ {errors} compile {Plural(errors, "error")}", "dep-tt-err"));

        var inCycles = cycles.Count(c => c.Members.Any(m => m.Id == node.Id));
        if (inCycles > 0)
            chips.Add(Chip($"⚠ in {inCycles} {Plural(inCycles, "cycle")}", "dep-tt-warn"));
        var cyclesInside = cycles.Count(c =>
            model.Nodes.TryGetValue(c.ContainerId, out var container) && IsInside(container, node)
        );
        if (cyclesInside > 0)
            chips.Add(Chip($"⚠ {cyclesInside} {Plural(cyclesInside, "cycle")} inside", "dep-tt-warn"));

        var rules = brokenRules.Count(r =>
            r.Violations.Any(v => model.Nodes.TryGetValue(v.SourceId, out var source) && IsInside(source, node))
        );
        if (rules > 0)
            chips.Add(Chip($"✕ breaks {rules} {Plural(rules, "rule")}", "dep-tt-err"));

        return chips;
    }

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

    static string Shorten(string text)
    {
        text = text.Trim();
        if (text.Length <= MaxDescriptionLength)
            return text;
        var wordEnd = text.LastIndexOf(' ', MaxDescriptionLength);
        return text[..(wordEnd > 0 ? wordEnd : MaxDescriptionLength)] + "…";
    }
}
