using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling;

interface ILineService
{
    void AddLinesFromSourceToTarget(IModel model, Link link);
}

[Transient]
class LineService : ILineService
{
    public void AddLinesFromSourceToTarget(IModel model, Link link)
    {
        Node commonAncestor = GetCommonAncestor(link);

        // Add lines from source and target nodes up to its parent for all ancestors until just before the common ancestor
        var sourceAncestor = AddAncestorLines(model, link, link.Source, commonAncestor);
        var targetAncestor = AddDescendantLines(model, link, link.Target, commonAncestor);

        // Connect 'sibling' nodes that are ancestors to source and target (or are source/target if they are siblings)
        AddDirectLine(model, sourceAncestor, targetAncestor, link);
    }

    // The parents are used (not the endpoints themselves), so a link between a node and one of
    // its ancestors resolves to the container above both, keeping the line chain non-empty.
    static Node GetCommonAncestor(Link link) => link.Source.Parent.LowestCommonAncestor(link.Target.Parent);

    Node AddAncestorLines(IModel model, Link link, Node source, Node commonAncestor)
    {
        // Add lines from source node up to all ancestors until just before common ancestors
        Node currentSource = source;
        foreach (var parent in source.Ancestors())
        {
            if (parent == commonAncestor)
                break;
            AddDirectLine(model, currentSource, parent, link);
            currentSource = parent;
        }

        return currentSource;
    }

    Node AddDescendantLines(IModel model, Link link, Node target, Node commonAncestor)
    {
        // Add lines from just below commonAncestor node down to all descendants until target
        Node currentTarget = target;
        foreach (var parent in target.Ancestors())
        {
            if (parent == commonAncestor)
                break;
            AddDirectLine(model, parent, currentTarget, link);
            currentTarget = parent;
        }

        return currentTarget;
    }

    void AddDirectLine(IModel model, Node source, Node target, Link link)
    {
        if (source.Name == target.Name)
            return;

        // Inheritance links get their own line segments where they touch the real source or
        // target node (special top/bottom anchors and hollow arrow); intermediate segments
        // between ancestors merge with usage lines as usual.
        var isInheritanceSegment = link.IsInheritance && (source == link.Source || target == link.Target);

        var line = source.SourceLines.FirstOrDefault(l =>
            l.Target == target && l.IsInheritance == isInheritanceSegment
        );
        if (line == null)
        { // First line between these source and target
            line = new Line(source, target, isInheritance: isInheritanceSegment);
            source.SourceLines.Add(line);
            target.TargetLines.Add(line);

            model.TryAddLine(line);
        }

        line.Add(link);
        link.AddLine(line);
    }
}
