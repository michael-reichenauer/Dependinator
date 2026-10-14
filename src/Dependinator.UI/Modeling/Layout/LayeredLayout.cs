using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Layout;

// Arranges a container's children as a layered dependency flow (see LayeredEngine): captures
// the children and their lines, computes the layout and applies it.
static class LayeredLayout
{
    // False when the container has fewer than two children to arrange or none has a relation,
    // so the caller can pack the children instead.
    public static bool TryArrange(Node parent, LayoutMetrics metrics, Func<Node, Size> sizeOf)
    {
        if (LayoutSnapshot.Capture(parent, metrics, sizeOf) is not { } snapshot)
            return false;
        if (LayeredEngine.Prepare(snapshot.Graph) is not { } plan)
            return false;

        snapshot.Apply(LayeredEngine.RunFast(plan));
        return true;
    }
}
