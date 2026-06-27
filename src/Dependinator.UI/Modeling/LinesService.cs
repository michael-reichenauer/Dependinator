using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling;

interface ILineService
{
    void AddLinesFromSourceToTarget(IModel model, Link link);
}

[Transient]
class LineService() : ILineService
{
    public void AddLinesFromSourceToTarget(IModel model, Link link)
    {
        Node commonAncestor = GetCommonAncestor(link);

        // Add lines from source and target nodes upp to its parent for all ancestors until just before the common ancestor
        var sourceAncestor = AddAncestorLines(model, link, link.Source, commonAncestor);
        var targetAncestor = AddDescendantLines(model, link, link.Target, commonAncestor);

        // Connect 'sibling' nodes that are ancestors to source and target (or are source/target if they are siblings)
        AddDirectLine(model, sourceAncestor, targetAncestor, link);
    }

    static Node GetCommonAncestor(Link link)
    {
        var targetAncestors = link.Target.Ancestors().ToList();
        return link.Source.Ancestors().First(targetAncestors.Contains);
    }

    Node AddAncestorLines(IModel model, Link link, Node source, Node commonAncestor)
    {
        // Add lines from source node upp to all ancestors until just before common ancestors
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

        var line = source.SourceLines.FirstOrDefault(l => l.Target == target);
        if (line == null)
        { // First line between these source and target
            line = new Line(source, target);
            source.SourceLines.Add(line);
            target.TargetLines.Add(line);

            model.TryAddLine(line);
        }

        line.Add(link);
        link.AddLine(line);
    }
}
