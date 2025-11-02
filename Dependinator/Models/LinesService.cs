namespace Dependinator.Models;

interface ILineService
{
    void AddLinesFromSourceToTarget(Link link);
}

[Transient]
class LineService(IModel model) : ILineService
{
    public void AddLinesFromSourceToTarget(Link link)
    {
        Node commonAncestor = GetCommonAncestor(link);

        // Add lines from source and target nodes upp to its parent for all ancestors until just before the common ancestor
        var sourceAncestor = AddAncestorLines(link, link.Source, commonAncestor);
        var targetAncestor = AddDescendantLines(link, link.Target, commonAncestor);

        // Connect 'sibling' nodes that are ancestors to source and target (or are source/target if they are siblings)
        AddDirectLine(sourceAncestor, targetAncestor, link);
    }

    static Node GetCommonAncestor(Link link)
    {
        var targetAncestors = link.Target.Ancestors().ToList();
        return link.Source.Ancestors().First(targetAncestors.Contains);
    }

    Node AddAncestorLines(Link link, Node source, Node commonAncestor)
    {
        // Add lines from source node upp to all ancestors until just before common ancestors
        Node currentSource = source;
        foreach (var parent in source.Ancestors())
        {
            if (parent == commonAncestor)
                break;
            AddDirectLine(currentSource, parent, link);
            currentSource = parent;
        }

        return currentSource;
    }

    Node AddDescendantLines(Link link, Node target, Node commonAncestor)
    {
        // Add lines from just below commonAncestor node down to all descendants until target
        Node currentTarget = target;
        foreach (var parent in target.Ancestors())
        {
            if (parent == commonAncestor)
                break;
            AddDirectLine(parent, currentTarget, link);
            currentTarget = parent;
        }

        return currentTarget;
    }

    void AddDirectLine(Node source, Node target, Link link)
    {
        if (source.Name == target.Name)
            return;

        var line = source.SourceLines.FirstOrDefault(l => l.Target == target);
        if (line == null)
        { // First line between these source and target
            line = new Line(source, target);
            source.SourceLines.Add(line);
            target.TargetLines.Add(line);

            model.AddLine(line);
        }

        line.Add(link);
        link.AddLine(line);
    }
}
