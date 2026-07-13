using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling;

// Marks namespace nodes that mirror their assembly's name (e.g. the "Dependinator" -> "Core"
// chain inside "Dependinator.Core.dll") as pass-through: invisible containers that exactly fill
// their parent, so their contents appear to be shown directly in the assembly node. A namespace
// only qualifies while it is the sole child at its level; when e.g. a second base namespace
// appears, the flag is cleared and the node renders normally again.
static class PassThroughService
{
    public static void UpdatePassThroughFlags(IModel model)
    {
        var previous = model.Nodes.Values.Where(n => n.IsPassThrough).ToHashSet();

        foreach (var node in model.Nodes.Values)
            node.IsPassThrough = false;

        foreach (var moduleNode in model.Nodes.Values.Where(IsModuleNode))
        {
            var chain = MarkPassThroughChain(moduleNode);
            if (chain.Count > 0 && !chain.All(previous.Contains))
                NormalizeChainTransforms(moduleNode, chain);
        }

        // Nodes that reverted to normal rendering have a container transform fitted to the large
        // pass-through boundary; re-fit it to their own (small) stored boundary.
        foreach (var node in previous.Where(n => !n.IsPassThrough))
            NodeLayout.FitContainerTransform(node);
    }

    // Solution assemblies are parsed with NodeType.Assembly; external assemblies under the
    // $Externals node are only synthesized from link targets and get NodeType.Parent.
    static bool IsModuleNode(Models.Node node) =>
        node.Type == NodeType.Assembly || node.Parent is { Type: NodeType.Externals };

    static List<Models.Node> MarkPassThroughChain(Models.Node moduleNode)
    {
        List<Models.Node> chain = [];
        var baseNameSegments = GetModuleBaseNameSegments(moduleNode);
        var namePrefix = $"{moduleNode.Name}.";

        var current = moduleNode;
        while (current.Children.Count == 1)
        {
            var child = current.Children[0];
            if (child.Type is not (NodeType.Namespace or NodeType.Parent))
                break;
            if (!child.Name.StartsWith(namePrefix, StringComparison.Ordinal))
                break;

            var relativeSegments = child.Name[namePrefix.Length..].Split('.');
            if (relativeSegments.Length > baseNameSegments.Length)
                break;
            if (!relativeSegments.SequenceEqual(baseNameSegments.Take(relativeSegments.Length)))
                break;

            child.IsPassThrough = true;
            chain.Add(child);
            current = child;
        }

        return chain;
    }

    // The transforms of the module node and intermediate pass-through nodes are compensated by
    // the derived pass-through boundaries, so reset them to identity to keep the effective
    // boundary at the module's own size; then fit the deepest node's transform, which is what
    // actually positions the visible content.
    static void NormalizeChainTransforms(Models.Node moduleNode, IReadOnlyList<Models.Node> chain)
    {
        foreach (var node in new[] { moduleNode }.Concat(chain.SkipLast(1)))
        {
            node.ContainerZoom = 1;
            node.ContainerOffset = Pos.None;
        }

        NodeLayout.FitContainerTransform(chain[^1]);
    }

    // E.g. "Dependinator.Core (dll)" => ["Dependinator", "Core"]
    static string[] GetModuleBaseNameSegments(Models.Node moduleNode)
    {
        var baseName = moduleNode.LongName;
        if (baseName.EndsWith(" (dll)", StringComparison.OrdinalIgnoreCase))
            baseName = baseName[..^6];
        else if (baseName.EndsWith(" (exe)", StringComparison.OrdinalIgnoreCase))
            baseName = baseName[..^6];
        else if (
            baseName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || baseName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        )
        {
            baseName = baseName[..^4];
        }

        return baseName.Split('.');
    }
}
