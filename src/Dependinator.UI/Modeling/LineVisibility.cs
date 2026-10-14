using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling;

// Which lines are hidden follows from which nodes are: a line whose links all touch a hidden
// node at either end is hidden with it. Recomputed after a load, a re-parse and every hide or
// show (also when one is undone).
static class LineVisibility
{
    public static void Update(IModel model)
    {
        foreach (var line in model.Lines.Values)
        {
            if (line.IsDirect)
            {
                line.IsHidden = false;
                continue;
            }

            line.IsHidden = line.Links.All(link => link.Source.IsHidden || link.Target.IsHidden);
        }
    }
}
