using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling;

// Which lines are dimmed or hidden follows from which nodes are: a line whose links all touch a
// dimmed or hidden node at either end is dimmed, and hidden when they all touch a hidden one.
// Recomputed after a load, a re-parse and every dim, hide or show (also when one is undone).
static class LineVisibility
{
    public static void Update(IModel model)
    {
        foreach (var line in model.Lines.Values)
            Update(line);
    }

    public static void Update(Line line) => line.Visibility = Of(line);

    static NodeVisibility Of(Line line)
    {
        if (line.IsDirect)
            return NodeVisibility.Visible;

        // The least affected link decides: one visible link keeps the whole line visible.
        var visibility = NodeVisibility.Hidden;
        foreach (var link in line.Links)
        {
            var linkVisibility =
                link.Source.Visibility > link.Target.Visibility ? link.Source.Visibility : link.Target.Visibility;
            if (linkVisibility < visibility)
                visibility = linkVisibility;
        }
        return visibility;
    }
}
