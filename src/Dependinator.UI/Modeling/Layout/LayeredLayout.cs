using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Layout;

// Arranges a container's children as a layered dependency flow (see LayeredEngine): captures
// the children and their lines, computes the layout and applies it. The thorough pass
// (ThoroughLayoutRun) runs when its estimated work fits the given budget, otherwise the fast one.
static class LayeredLayout
{
    // The fast pass only. False when the container has fewer than two children to arrange or
    // none has a relation, so the caller can pack the children instead.
    public static bool TryArrange(Node parent, LayoutMetrics metrics, Func<Node, Size> sizeOf) =>
        TryArrange(parent, metrics, sizeOf, thoroughWorkBudget: 0, out _);

    public static bool TryArrange(
        Node parent,
        LayoutMetrics metrics,
        Func<Node, Size> sizeOf,
        long thoroughWorkBudget,
        out bool isThorough
    )
    {
        isThorough = false;
        if (LayoutSnapshot.Capture(parent, metrics, sizeOf) is not { } snapshot)
            return false;
        if (LayeredEngine.Prepare(snapshot.Graph) is not { } plan)
            return false;

        isThorough = thoroughWorkBudget > 0 && ThoroughLayoutRun.EstimateWork(plan) <= thoroughWorkBudget;
        var fast = LayeredEngine.RunFast(plan);
        snapshot.Apply(
            isThorough ? Better(snapshot.Graph, fast, new ThoroughLayoutRun(plan, thoroughWorkBudget)) : fast
        );
        return true;
    }

    // The thorough result, unless the fast one reads better after all: the thorough pass orders
    // by the crossings it can model, but e.g. lines pointing backward (in cycles) are drawn from
    // their source's right edge and cross more than modeled.
    static LayoutResult Better(LayoutGraph graph, LayoutResult fast, ThoroughLayoutRun thoroughRun)
    {
        var thorough = thoroughRun.RunToCompletion();
        return LayoutQuality.Evaluate(graph, thorough).Score <= LayoutQuality.Evaluate(graph, fast).Score
            ? thorough
            : fast;
    }
}
