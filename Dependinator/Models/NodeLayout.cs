namespace Dependinator.Models;

class NodeLayout
{
    const double DefaultWidth = 100;
    const double DefaultHeight = 100;
    public static readonly Size DefaultSize = new(DefaultWidth, DefaultHeight);
    const int margin = 10;

    public static Rect GetNextChildRect(Node node)
    {
        var childSize = DefaultSize;
        var b = node.Boundary;
        int columns = (int)Math.Floor((b.Width / node.ContainerZoom) / (childSize.Width + margin));

        var x = margin + (childSize.Width + margin) * (node.Children.Count % columns);
        var y = margin + (childSize.Height + margin) * (node.Children.Count / columns);
        return new Rect(x, y, childSize.Width, childSize.Height);

        // while (true)
        // {
        //     double x = 0;
        //     double y = 0;
        //     for (var i = 0; i < 7; i++)
        //     {
        //         var r = new Rect(x + i * size.Width, y, size.Width, size.Height);
        //         if (Children.All(c => !IsOverlap(c.Boundary, r)))
        //         {
        //             return r;
        //         }
        //         y += size.Height;
        //     }
        // }
    }


}

