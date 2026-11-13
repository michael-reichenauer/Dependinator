using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

// How strongly each line is drawn: dimmed, lit or at its link-count width by the selection, the
// picked cycle and the shown path, and whether the user's line and row filters leave it out.
// Decisions only, no SVG: NodeLinesSvg and LineSvg draw what these say.
namespace Dependinator.UI.Diagrams.Svg;

readonly record struct RenderSelection(Node? Node, Line? Line, IReadOnlyList<Node>? OtherNodes = null)
{
    public bool IsEmpty => Node is null && Line is null;
}

static class LineEmphasis
{
    // The selected node(s) or line, for selection dimming; empty when nothing is selected.
    internal static RenderSelection FindSelection(IModel model)
    {
        var nodes = model.Nodes.Values.Where(n => n.IsSelected).ToList();
        var node = nodes.FirstOrDefault();
        var line = node is null ? model.Lines.Values.FirstOrDefault(l => l.IsSelected) : null;
        return new RenderSelection(node, line, nodes.Count > 1 ? nodes.Skip(1).ToList() : null);
    }

    // What dims the lines: the selection, or with nothing selected the nodes of the cycle picked
    // in the cycles panel, so the lines that do not touch the cycle fade too. Only dimming: the
    // line widths still follow the real selection (see IsLineWeighted).
    internal static RenderSelection FindDimSelection(
        IModel model,
        RenderSelection selection,
        IReadOnlyList<NodeId>? cycleMembers
    )
    {
        if (!selection.IsEmpty || cycleMembers is null)
            return selection;
        var nodes = cycleMembers.Select(id => model.Nodes.GetValueOrDefault(id)).OfType<Node>().ToList();
        return new RenderSelection(nodes.FirstOrDefault(), null, nodes.Count > 1 ? nodes.Skip(1).ToList() : null);
    }

    // A line is dimmed while something else is selected: for a selected node, every line that
    // is not the node's own (see IsNodeLine); for a selected line, every other line but the ones
    // drawn for it (a selected stub's lines, Line.IsRevealed). Explorer focus lines are what the
    // user asked to see and are never dimmed.
    internal static bool IsLineDimmed(Line line, RenderSelection selection)
    {
        if (line.IsFocused)
            return false;
        if (selection.Line is { } selectedLine)
            return line != selectedLine && !(line.IsRevealed && LineStub.IsEdgeLine(selectedLine));
        if (selection.Node is not { } node)
            return false;
        if (IsNodeLine(line, node))
            return false;
        // With several nodes selected, a line of any of them stays bright.
        return selection.OtherNodes is not { } others || !others.Any(other => IsNodeLine(line, other));
    }

    // A node's own lines: those with one end at or inside the node and the other end outside it,
    // i.e. the lines drawn out of the node. Every line inside it stays dimmed, the lines between
    // its children and their segments to its edge alike, so selecting an open container lights
    // what it depends on and what depends on it, not its inner wiring; selecting or hovering a
    // child shows that.
    static bool IsNodeLine(Line line, Node node) => line.Source.IsAtOrInside(node) != line.Target.IsAtOrInside(node);

    // A line shows its link-count width only when the user asked for the widths (View › Line
    // Widths by Link Count, off by default) and only while it is what the user is looking at: the
    // lines of the selection (the ones dimming keeps bright) and explorer focus lines. Otherwise
    // every line is drawn at the same thin width (see LineSvg), whatever the Dim Unrelated Lines
    // toggle says, so selecting a node still sets its lines apart with dimming off.
    internal static bool IsLineWeighted(Line line, RenderSelection selection, bool linkCountWidths) =>
        linkCountWidths && (line.IsFocused || (!selection.IsEmpty && !IsLineDimmed(line, selection)));

    internal static bool IsLineWeighted(Line line, RenderContext context) =>
        !IsOtherCycleLine(line, context)
        && IsLineWeighted(line, context.Flags.Selection, context.Flags.LinkCountWidths);

    // A line the user is looking at: one of the selection's own lines (those dimming keeps
    // bright), whatever the Dim Unrelated Lines toggle says. Soft lines (Canvas.razor) draw these
    // at full strength and the resting lines faint.
    internal static bool IsLineLit(Line line, RenderSelection selection) =>
        !selection.IsEmpty && !IsLineDimmed(line, selection);

    // The real selection only: a cycle picked in the cycles panel lights nothing more than its
    // own lines, which are cycle lines and full strength anyway.
    internal static bool IsLineLit(Line line, RenderContext context) =>
        !IsOtherCycleLine(line, context) && IsLineLit(line, context.Flags.Selection);

    // While a dependency path is shown, everything off the path fades so the chain stands out.
    // While one cycle is picked in the cycles panel, the other cycles' lines fade.
    internal static bool IsLineDimmed(Line line, RenderContext context)
    {
        if (context.Flags.PathLines is { } pathLines)
            return !pathLines.Contains(line.Id);
        if (IsOtherCycleLine(line, context))
            return true;
        return context.Flags.DimUnrelated && IsLineDimmed(line, context.Flags.DimSelection);
    }

    // A cyclic line that is not part of the cycle picked in the cycles panel.
    static bool IsOtherCycleLine(Line line, RenderContext context) =>
        context.Flags.SelectedCycleLines is { } cycleLines
        && IsLineCyclic(line, context)
        && !cycleLines.Contains(line.Id);

    internal static bool IsLineCyclic(Line line, RenderContext context) =>
        context.Flags.CyclicLines?.Contains(line.Id) == true;

    internal static bool IsLineOnPath(Line line, RenderContext context) =>
        context.Flags.PathLines?.Contains(line.Id) == true;

    internal static bool IsLineViolating(Line line, RenderContext context) =>
        context.Flags.ViolatingLines?.Contains(line.Id) == true;

    internal static bool IsLineFilteredOut(Line line, LineFilter filter) =>
        IsLineFilteredOut(line, filter, RowFilter.None);

    // The user's line filter (View › Lines) and row filter (View › Rows). A line to a row that
    // is left out goes with the row, whatever the line is; among the drawn rows, explorer focus
    // lines and user-requested direct lines are explicit asks and are always drawn.
    internal static bool IsLineFilteredOut(Line line, LineFilter filter, RowFilter rows)
    {
        if (rows.Hides(line.Source.Type) || rows.Hides(line.Target.Type))
            return true;
        if (line.IsFocused || line.IsDirect)
            return false;
        if (filter.HideInheritance && line.IsInheritance)
            return true;
        if (filter.HideConfiguration && line.IsConfiguration)
            return true;
        if (filter.HideMember && (line.Source.Type.IsMember || line.Target.Type.IsMember))
            return true;
        if (filter.HideExternal && (line.Source.IsExternal() || line.Target.IsExternal()))
            return true;

        // The selection's own lines are drawn end to end, one per node at the far end: a line
        // of several links becomes several thin lines, which the link count must not drop.
        return !line.IsRevealed && line.Links.Count < filter.MinLinkCount;
    }
}
