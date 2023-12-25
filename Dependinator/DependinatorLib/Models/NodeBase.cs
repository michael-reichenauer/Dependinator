
namespace Dependinator.Models;

abstract class NodeBase : IItem
{
    readonly List<Node> children = new();
    readonly List<Link> sourceLinks = new();
    readonly List<Link> targetLinks = new();
    readonly List<Line> sourceLines = new();
    readonly List<Line> targetLines = new();
    protected readonly IModel model;


    protected NodeBase(NodeId id, string name, Node parent, IModel model)
    {
        Id = id;
        Name = name;
        Parent = parent;
        this.model = model;
    }


    public NodeId Id { get; }
    public bool IsModified { get; protected set; } = true;
    public string Name { get; }
    public Node Parent { get; private set; }
    public bool IsRoot => Type == Parsing.NodeType.Root;
    public Parsing.NodeType Type { get; set; } = Parsing.NodeType.None;
    public string Description { get; set; } = "";

    public IReadOnlyList<Node> Children => children;
    public IReadOnlyList<Link> SourceLinks => sourceLinks;
    public IReadOnlyList<Link> TargetLinks => targetLinks;
    public IReadOnlyList<Line> SourceLines => sourceLines;
    public IReadOnlyList<Line> TargetLines => targetLines;

    public abstract string GetSvg(Pos parentCanvasPos, double parentZoom);

    public IEnumerable<IItem> AllItems()
    {
        foreach (var child in Children)
        {
            yield return child;
        }
        foreach (var child in Children)
        {
            foreach (var line in child.SourceLines)
            {
                yield return line;
            }
        }
    }

    public void AddChild(Node child)
    {
        children.Add(child);
        child.Parent = (Node)this;
    }


    public void RemoveChild(Node child)
    {
        children.Remove(child);
        child.Parent = null!;
    }

    public void AddSourceLink(Link link)
    {
        if (sourceLinks.Contains(link)) return;
        sourceLinks.Add(link);

        AddLinesFromSourceToTarget(link);
    }

    public void AddTargetLink(Link link)
    {
        if (targetLinks.Contains(link)) return;
        targetLinks.Add(link);
    }


    public void Update(Parsing.Node node)
    {
        if (IsEqual(node)) return;

        var parentName = node.ParentName;
        if (Parent.Name != parentName)
        {   // The node has changed parent, remove it from the old parent and add it to the new parent
            Parent.RemoveChild((Node)this);
            Parent = model.GetOrCreateNode(parentName);
            Parent.AddChild((Node)this);
        }

        Type = node.Type;
        Description = node.Description;
    }


    void AddLinesFromSourceToTarget(Link link)
    {
        Node commonAncestor = GetCommonAncestor(link);

        // Add lines from source and target nodes upp to its parent for all ancestors until just before the common ancestor
        var sourceAncestor = AddAncestorLines(link, link.Source, commonAncestor);
        var targetAncestor = AddAncestorLines(link, link.Target, commonAncestor);

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
            if (parent == commonAncestor) break;
            AddDirectLine(currentSource, parent, link);
            currentSource = parent;
        }

        return currentSource;
    }

    void AddDirectLine(Node source, Node target, Link link)
    {
        var line = source.sourceLines.FirstOrDefault(l => l.Target == target);
        if (line == null)
        {   // First line between these source and target
            line = new Line(source, target);
            source.sourceLines.Add(line);
            target.targetLines.Add(line);

            model.AddLine(line);
        }

        line.Add(link);
        link.AddLine(line);

    }


    IEnumerable<Node> Ancestors()
    {
        var node = this;
        while (node.Parent != null)
        {
            yield return node.Parent;
            node = node.Parent;
        }
    }


    bool IsEqual(Parsing.Node n) =>
        Parent.Name == n.ParentName &&
        Type == n.Type &&
        Description == n.Description;

}

