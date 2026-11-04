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
    const double ColumnGap = 2 * NodeGrid.SnapSize;

    // A column is as wide as its widest row (estimated from the name and signature), in grid steps.
    const double MinColumnWidth = 100;
    const double MaxColumnWidth = 300;
    const double IconAndGapWidth = 12; // The 8-unit member icon and the gap to its name
    const double NameFontSize = 8; // NodeSvg.FontSize
    const double CharWidthFactor = 0.6; // NodeSvg.AverageCharWidthFactor
    const double RowEndPadding = 6;
    const double SignatureGap = 3; // NodeSvg.MemberSignatureGap

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
    public static Rect Arrange(IReadOnlyList<Section> sections, double startX, double startY, double targetAspect)
    {
        if (sections.Count == 0)
            return Rect.None;

        var columnWidth = ColumnWidth(sections);
        var items = Items(sections);
        var best = BestFlow(items, columnWidth, targetAspect);

        for (var index = 0; index < items.Count; index++)
        {
            var (column, step) = best.Places[index];
            if (items[index] is not { } row)
                continue;
            row.Boundary = new Rect(
                startX + column * (columnWidth + ColumnGap),
                startY + step * RowPitch,
                columnWidth,
                RowHeight
            );
        }

        var size = CardSize(best, columnWidth);
        return new Rect(startX - Padding, startY - Padding, size.Width, size.Height);
    }

    // The aspect (width / height) of the card closest to a square that the members can make: the
    // shape a container of only these members is given, so its card fills it.
    public static double NaturalAspect(IReadOnlyList<Section> sections)
    {
        if (sections.Count == 0)
            return 1;
        var columnWidth = ColumnWidth(sections);
        var size = CardSize(BestFlow(Items(sections), columnWidth, 1), columnWidth);
        return size.Width / size.Height;
    }

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
        var rows = LaidOutRows(parent, isShown);
        if (rows.Count == 0)
            return Rect.None;
        var headings = Headings(parent, isShown);
        var left = rows.Min(r => r.Boundary.X);
        var top = Math.Min(rows.Min(r => r.Boundary.Y), headings.Count > 0 ? headings.Min(h => h.Y) : double.MaxValue);
        var right = rows.Max(r => r.Boundary.X + r.Boundary.Width);
        var bottom = rows.Max(r => r.Boundary.Y + r.Boundary.Height);
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
    static List<Node> LaidOutRows(Node parent, Func<Node, bool>? isShown) =>
        parent.Children.Where(c => c.Type.IsMember && c.Boundary != Rect.None && (isShown?.Invoke(c) ?? true)).ToList();

    // The width every column gets: the widest row name, on the snap grid.
    static double ColumnWidth(IReadOnlyList<Section> sections)
    {
        // The name and its signature (NodeSvg.MemberDetailsSvg); a comment gets what is left.
        var widest = sections.SelectMany(s => s.Rows).Max(row => row.DisplayName.Length + (row.Signature?.Length ?? 0));
        var width = IconAndGapWidth + widest * NameFontSize * CharWidthFactor + SignatureGap + RowEndPadding;
        return Math.Clamp(Math.Ceiling(width / NodeGrid.SnapSize) * NodeGrid.SnapSize, MinColumnWidth, MaxColumnWidth);
    }

    // The card's items top to bottom, one grid step each: a section's heading (null), then its rows.
    static List<Node?> Items(IReadOnlyList<Section> sections)
    {
        var items = new List<Node?>();
        foreach (var section in sections)
        {
            items.Add(null);
            items.AddRange(section.Rows);
        }
        return items;
    }

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
            // A heading needs its first row below it in the same column.
            var needed = items[index] is null ? 2 : 1;
            if (step + needed > height && step > 0)
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
