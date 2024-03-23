namespace Dependinator.Models;

class NodeLayout
{
    const double DefaultWidth = 100;
    const double DefaultHeight = 100;
    public static readonly Size DefaultSize = new(DefaultWidth, DefaultHeight);
    const int margin = 10;

    public static Rect GetNextChildRect(Node node)
    {
        if (!node.IsRoot) return Rect.None;

        var childSize = DefaultSize;
        var b = node.Boundary;
        int columns = (int)Math.Floor((b.Width / node.ContainerZoom) / (childSize.Width + margin));

        var x = margin + (childSize.Width + margin) * (node.Children.Count % columns);
        var y = margin + (childSize.Height + margin) * (node.Children.Count / columns);
        return new Rect(x, y, childSize.Width, childSize.Height);
    }

    public static void AdjustChildren(Node parent)
    {
        parent.IsChildrenLayoutRequired = false;

        var childrenCount = parent.Children.Count;
        parent.ContainerZoom = childrenCount switch
        {
            <= 1 => 1 / 3.0,
            <= 4 => 1 / 4.0,
            <= 9 => 1 / 5.0,
            <= 16 => 1 / 6.0,
            <= 25 => 1 / 7.0,
            <= 36 => 1 / 8.0,
            _ => Node.DefaultContainerZoom
        };

        int columnsCount = (int)Math.Floor(parent.Boundary.Width / parent.ContainerZoom / DefaultWidth) - 2;
        int rowsCount = (int)Math.Floor(parent.Boundary.Height / parent.ContainerZoom / DefaultHeight) - 2;

        var marginX = (DefaultWidth / parent.ContainerZoom - (DefaultWidth * columnsCount)) / (columnsCount + 1);
        var marginY = (DefaultHeight / parent.ContainerZoom - (DefaultHeight * rowsCount)) / (rowsCount + 1);

        var columnsNeeded = Math.Min(columnsCount, Math.Max(1, (int)Math.Ceiling(childrenCount / (double)rowsCount)));
        var startColumn = Math.Min(columnsCount - 1, Math.Max(0, (columnsCount + 1) / 2 - columnsNeeded));
        int midRow = rowsCount / 2;

        var index = 0;
        for (int column = startColumn; index < parent.Children.Count; column++)
        {
            for (int offset = 0; offset <= (rowsCount + 1) / 2; offset++)
            {
                var row = midRow - offset;
                if (row < 0 || index >= parent.Children.Count) break;
                var child = parent.Children[index++];
                AdjustChild(child, column, row, marginX, marginY);
                if (offset == 0) continue;

                row = midRow + offset;
                if (row >= rowsCount || index >= parent.Children.Count) break;
                child = parent.Children[index++];
                AdjustChild(child, column, row, marginX, marginY);
            }
        }
    }

    private static void AdjustChild(Node child, int column, int row, double marginX, double marginY)
    {
        var x = column * (DefaultWidth + marginX) + marginX;
        var y = row * (DefaultHeight + marginY) + marginY;
        child.Boundary = new Rect(x, y, DefaultWidth, DefaultHeight);
    }
}

