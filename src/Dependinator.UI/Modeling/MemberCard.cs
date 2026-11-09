using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using NodeType = Dependinator.Core.Parsing.NodeType;

namespace Dependinator.UI.Modeling;

// The member card: a container's member rows (C# type members; Bicep parameters, variables,
// outputs and auxiliary resources) grouped into titled sections, like the compartments of a UML
// class box, and flowed top to bottom into as many columns as fit the container's shape. Rows
// and headings take one step (RowPitch) each, tighter than the 20-unit snap grid: lines to rows
// are slanted anyway, and a list reads better close. A section's heading sits above its first row. The positions are the layout: the headings and the card behind the rows
// are derived from them when rendering (Sections, Headings, Bounds), so nothing extra is stored.
static class MemberCard
{
    public const double RowPitch = 16;
    public const double RowHeight = 15;
    public const double HeadingHeight = RowPitch;

    // Space between the rows and the card's edge, and between columns.
    public const double Padding = 10;

    // An open card node (NodeLayout.ArrangeCardNode, MemberCardSvg.GetCardNodeSvg) in its child units:
    // the header (icon, name, kind) at the top, then the first section heading.
    public const double CardHeaderHeight = 36;
    public const double CardRowsTop = CardHeaderHeight + 4;

    // The narrowest open card, so a type of one short member still has room for its name.
    public const double MinCardWidth = 140;
    const double ColumnGap = 2 * NodeGrid.SnapSize;

    // A column is as wide as its widest row (estimated from the name and signature), in grid steps.
    const double MinColumnWidth = 100;
    const double MaxColumnWidth = 300;
    const double IconAndGapWidth = 12; // The 8-unit member icon and the gap to its name
    const double NameFontSize = 8; // SvgText.FontSize
    const double CharWidthFactor = 0.6; // SvgText.AverageCharWidthFactor
    const double RowEndPadding = 6;

    // A section of the card: the members of one kind, public before private.
    public sealed record Section(string Title, IReadOnlyList<Node> Rows);

    // A section's heading in the container's child coordinates: drawn above the section's first row.
    public readonly record struct Heading(string Title, double X, double Y, double Width, bool IsColumnTop);

    static readonly (NodeType Type, string Title)[] SectionOrder =
    [
        (NodeType.FieldMember, "Fields"),
        (NodeType.ConstructorMember, "Constructors"),
        (NodeType.PropertyMember, "Properties"),
        (NodeType.EventMember, "Events"),
        (NodeType.MethodMember, "Methods"),
        (NodeType.ParameterMember, "Parameters"),
        (NodeType.VariableMember, "Variables"),
        (NodeType.ResourceMember, "Resources"),
        (NodeType.OutputMember, "Outputs"),
    ];

    public static bool HasMembers(Node parent) => parent.Children.Any(child => child.Type.IsMember);

    // A card of more rows than this, with public and private rows, folds its private rows into one
    // "+ N private" row at its end (the private toggle), until the user opens it
    // (Node.IsPrivateMembersShown); then the row reads "hide N private". Folded rows share the
    // toggle row's place, so their lines start and end there.
    public const int FoldMinRows = 12;

    public static bool HasPrivateToggle(Node parent)
    {
        var (publicCount, privateCount) = (0, 0);
        foreach (var child in parent.Children)
        {
            if (!child.Type.IsMember)
                continue;
            if (child.IsPrivate ?? false)
                privateCount++;
            else
                publicCount++;
        }
        return publicCount > 0 && privateCount > 0 && publicCount + privateCount > FoldMinRows;
    }

    // The card was laid out with its private rows folded.
    public static bool IsPrivateFolded(Node parent) => parent.LaidOutToggle == PrivateToggle.Folded;

    // The row is folded into its card's "+ N private" row: not drawn on its own.
    public static bool IsFoldedRow(Node row) => row.IsFoldedAway;

    // The private rows the toggle stands for (only those shown when given isShown).
    public static int PrivateCount(Node parent, Func<Node, bool>? isShown = null) =>
        parent.Children.Count(c =>
            c.Type.IsMember
            && (IsPrivateFolded(parent) ? c.IsFoldedAway : c.IsPrivate ?? false)
            && (isShown?.Invoke(c) ?? true)
        );

    // The toggle row is drawn: the card was laid out with one, and some of its private rows are
    // shown (not all hidden by the user or View › Rows).
    public static bool IsToggleShown(Node parent, Func<Node, bool>? isShown = null) =>
        parent.LaidOutToggle != PrivateToggle.None && PrivateCount(parent, isShown) > 0;

    // The private toggle row's place, as laid out: below the last row (the flow keeps it in the
    // last row's column). Folded rows share it. Rect.None without a toggle.
    public static Rect PrivateToggleRect(Node parent) =>
        parent.LaidOutToggle == PrivateToggle.None ? Rect.None : parent.LaidOutToggleRect;

    // The members changed since the card was laid out (a re-parse crossed FoldMinRows or changed
    // a member's access, or the fold was toggled): the rows' boundaries no longer match the fold.
    public static bool IsLayoutStale(Node parent)
    {
        if (ToggleOf(parent) != parent.LaidOutToggle)
            return true;
        var isFolded = IsPrivateFolded(parent);
        return parent.Children.Any(c => c.Type.IsMember && c.IsFoldedAway != (isFolded && (c.IsPrivate ?? false)));
    }

    // Sets the layout state Arrange keeps (Node.LaidOutToggle, IsFoldedAway, LaidOutToggleRect) from
    // a loaded model's saved boundaries. They were laid out with the saved fold, unless a re-parse
    // changed the members and the model was saved before the card was laid out again: then the
    // folded rows do not share one place (or other rows do), and the card is laid out again.
    public static void RestoreLayoutState(Node parent)
    {
        if (!HasMembers(parent))
            return;
        var toggle = ToggleOf(parent);
        var rows = parent.Children.Where(c => c.Type.IsMember).ToList();
        rows.ForEach(r => r.IsFoldedAway = toggle == PrivateToggle.Folded && (r.IsPrivate ?? false));
        var folded = rows.Where(r => r.IsFoldedAway).Select(r => r.Boundary).Distinct().ToList();
        var placed = rows.Where(r => !r.IsFoldedAway).Select(r => r.Boundary).ToList();
        var isConsistent =
            rows.All(r => r.Boundary != Rect.None)
            && folded.Count <= 1
            && placed.Distinct().Count() == placed.Count
            && !folded.Any(placed.Contains);
        if (!isConsistent)
        {
            rows.ForEach(r => r.IsFoldedAway = false);
            parent.LaidOutToggle = PrivateToggle.None;
            parent.LaidOutToggleRect = Rect.None;
            parent.IsChildrenLayoutRequired = true;
            return;
        }

        parent.LaidOutToggle = toggle;
        if (toggle == PrivateToggle.Folded)
        {
            parent.LaidOutToggleRect = folded[0];
        }
        else if (toggle == PrivateToggle.Open)
        {
            var last = rows.MaxBy(r => (r.Boundary.X, r.Boundary.Y))!.Boundary;
            parent.LaidOutToggleRect = last with { Y = last.Y + RowPitch };
        }
        else
        {
            parent.LaidOutToggleRect = Rect.None;
        }
    }

    // The plural title of a member kind ("Methods"), also used for counts in the summary card.
    public static string SectionTitle(NodeType type) =>
        SectionOrder.FirstOrDefault(section => section.Type == type).Title ?? "Members";

    // The members grouped by kind in section order; within a section public members come first,
    // each group in the given comparison's order.
    public static IReadOnlyList<Section> Sections(IEnumerable<Node> members, Comparison<Node> compare)
    {
        var byType = members.Where(m => m.Type.IsMember).GroupBy(m => m.Type).ToDictionary(g => g.Key, g => g.ToList());
        var sections = new List<Section>();
        foreach (var (type, title) in SectionOrder)
        {
            if (!byType.TryGetValue(type, out var rows))
                continue;
            var publicRows = rows.Where(m => !(m.IsPrivate ?? false)).ToList();
            var privateRows = rows.Where(m => m.IsPrivate ?? false).ToList();
            publicRows.Sort(compare);
            privateRows.Sort(compare);
            sections.Add(new Section(title, [.. publicRows, .. privateRows]));
        }
        return sections;
    }

    // Lays the members out as a card whose top-left row area starts at (startX, startY), choosing
    // the column count whose card shape is closest to the target aspect (width / height). Returns
    // the card's bounds, padding included.
    // With a private toggle, the card ends with the toggle row, and folded private rows are given
    // its place.
    public static Rect Arrange(
        IReadOnlyList<Section> sections,
        double startX,
        double startY,
        double targetAspect,
        PrivateToggle toggle = PrivateToggle.None
    )
    {
        if (sections.Count == 0)
            return Rect.None;

        var columnWidth = ColumnWidth(sections, toggle);
        var (items, folded) = Items(sections, toggle);
        var best = BestFlow(items, columnWidth, targetAspect);
        var toggleRect = Rect.None;

        for (var index = 0; index < items.Count; index++)
        {
            var (column, step) = best.Places[index];
            var rect = new Rect(
                startX + column * (columnWidth + ColumnGap),
                startY + step * RowPitch,
                columnWidth,
                RowHeight
            );
            if (ReferenceEquals(items[index], ToggleItem))
                toggleRect = rect;
            else if (items[index] is { } row)
                row.Boundary = rect;
        }

        // Keep the fold the boundaries were made for (see Node.LaidOutToggle).
        foreach (var row in sections.SelectMany(section => section.Rows))
            row.IsFoldedAway = false;
        folded.ForEach(row =>
        {
            row.Boundary = toggleRect;
            row.IsFoldedAway = true;
        });
        if (sections[0].Rows.FirstOrDefault()?.Parent is { } parent)
        {
            parent.LaidOutToggle = toggle;
            parent.LaidOutToggleRect = toggleRect;
        }

        var size = CardSize(best, columnWidth);
        return new Rect(startX - Padding, startY - Padding, size.Width, size.Height);
    }

    public enum PrivateToggle
    {
        None,
        Folded,
        Open,
    }

    public static PrivateToggle ToggleOf(Node parent) =>
        !HasPrivateToggle(parent) ? PrivateToggle.None
        : parent.IsPrivateMembersShown ? PrivateToggle.Open
        : PrivateToggle.Folded;

    static FlowResult BestFlow(List<Node?> items, double columnWidth, double targetAspect)
    {
        var best = Flow(items, 1);
        var bestError = double.MaxValue;
        var maxColumns = Math.Max(1, (int)Math.Ceiling(items.Count / 3.0));
        for (var columns = 1; columns <= maxColumns; columns++)
        {
            var flow = Flow(items, columns);
            if (flow.Columns < columns)
                break; // More columns would only leave columns empty
            var size = CardSize(flow, columnWidth);
            var error = Math.Abs(Math.Log(size.Width / size.Height / targetAspect));
            if (error >= bestError)
                continue;
            bestError = error;
            best = flow;
        }
        return best;
    }

    static Size CardSize(FlowResult flow, double columnWidth) =>
        new(
            flow.Columns * columnWidth + (flow.Columns - 1) * ColumnGap + 2 * Padding,
            flow.Steps * RowPitch - (RowPitch - RowHeight) + 2 * Padding
        );

    // The card's bounds around the parent's member rows and their headings, padding included, in
    // the parent's child coordinates; Rect.None when the parent has no laid-out members.
    public static Rect Bounds(Node parent, Func<Node, bool>? isShown = null)
    {
        var rows = LaidOutRows(parent, isShown).Select(r => r.Boundary).ToList();
        if (IsToggleShown(parent, isShown))
            rows.Add(PrivateToggleRect(parent));
        if (rows.Count == 0)
            return Rect.None;
        var headings = Headings(parent, isShown);
        var left = rows.Min(r => r.X);
        var top = Math.Min(rows.Min(r => r.Y), headings.Count > 0 ? headings.Min(h => h.Y) : double.MaxValue);
        var right = rows.Max(r => r.X + r.Width);
        var bottom = rows.Max(r => r.Y + r.Height);
        return new Rect(left - Padding, top - Padding, right - left + 2 * Padding, bottom - top + 2 * Padding);
    }

    // The section headings, each one step above its section's first row (the top-most row of the
    // left-most column the section occupies). IsColumnTop: no row above it in its column, so no
    // divider line is needed there.
    public static IReadOnlyList<Heading> Headings(Node parent, Func<Node, bool>? isShown = null)
    {
        var rows = LaidOutRows(parent, isShown);
        var headings = new List<Heading>();
        foreach (var group in rows.GroupBy(r => r.Type))
        {
            var first = group.MinBy(r => (r.Boundary.X, r.Boundary.Y))!;
            var y = first.Boundary.Y - HeadingHeight;
            var isColumnTop = !rows.Any(r => r.Boundary.X == first.Boundary.X && r.Boundary.Y < y);
            headings.Add(new Heading(SectionTitle(group.Key), first.Boundary.X, y, first.Boundary.Width, isColumnTop));
        }
        return headings;
    }

    // The laid-out rows, only those shown when given isShown (rows the user hid or View › Rows
    // leaves out get no heading and no room on the card).
    // Rows folded into the private toggle are left out too.
    static List<Node> LaidOutRows(Node parent, Func<Node, bool>? isShown)
    {
        return parent
            .Children.Where(c =>
                c.Type.IsMember && c.Boundary != Rect.None && !c.IsFoldedAway && (isShown?.Invoke(c) ?? true)
            )
            .ToList();
    }

    // The width every column gets: the widest row name, on the snap grid (folded rows not counted).
    static double ColumnWidth(IReadOnlyList<Section> sections, PrivateToggle toggle)
    {
        // The name and its signature (MemberRowSvg.MemberDetailsSvg); a comment gets what is left.
        var rows = sections.SelectMany(s => s.Rows).ToList();
        if (toggle == PrivateToggle.Folded)
            rows = [.. rows.Where(r => !(r.IsPrivate ?? false))];
        var widest = rows.Max(row => row.DisplayName.Length + (row.Signature?.Length ?? 0));
        var width = IconAndGapWidth + widest * NameFontSize * CharWidthFactor + RowEndPadding;
        return Math.Clamp(Math.Ceiling(width / NodeGrid.SnapSize) * NodeGrid.SnapSize, MinColumnWidth, MaxColumnWidth);
    }

    // The card's items top to bottom, one step each: a section's heading (null), then its rows,
    // and the private toggle (ToggleItem) last. Folded private rows are left out (and returned).
    static (List<Node?> Items, List<Node> Folded) Items(IReadOnlyList<Section> sections, PrivateToggle toggle)
    {
        var items = new List<Node?>();
        var folded = new List<Node>();
        foreach (var section in sections)
        {
            var rows = section.Rows;
            if (toggle == PrivateToggle.Folded)
            {
                folded.AddRange(rows.Where(r => r.IsPrivate ?? false));
                rows = rows.Where(r => !(r.IsPrivate ?? false)).ToList();
            }
            if (rows.Count == 0)
                continue;
            items.Add(null);
            items.AddRange(rows);
        }
        if (toggle != PrivateToggle.None)
            items.Add(ToggleItem);
        return (items, folded);
    }

    // A stand-in for the private toggle row in the flow.
    static readonly Node ToggleItem = new("$privateToggle", null!);

    readonly record struct FlowResult(int Columns, int Steps, (int Column, int Step)[] Places);

    // Flows the items into at most the given number of columns: the lowest column height that
    // fits, never leaving a heading as the last item of a column. Steps is the tallest column.
    static FlowResult Flow(List<Node?> items, int columns)
    {
        for (var height = Math.Max(2, (int)Math.Ceiling(items.Count / (double)columns)); ; height++)
        {
            if (TryFlow(items, columns, height, out var result))
                return result;
        }
    }

    static bool TryFlow(List<Node?> items, int columns, int height, out FlowResult result)
    {
        var places = new (int, int)[items.Count];
        var column = 0;
        var step = 0;
        for (var index = 0; index < items.Count; index++)
        {
            // A heading needs its first row below it in the same column, and the private toggle
            // stays below the last row (PrivateToggleRect finds it there).
            var needed = items[index] is null ? 2 : 1;
            var isGlued = ReferenceEquals(items[index], ToggleItem);
            if (step + needed > height && step > 0 && !isGlued)
            {
                column++;
                step = 0;
            }
            if (column >= columns)
            {
                result = default;
                return false;
            }
            places[index] = (column, step);
            step++;
        }
        result = new FlowResult(column + 1, places.Max(place => place.Item2) + 1, places);
        return true;
    }
}
