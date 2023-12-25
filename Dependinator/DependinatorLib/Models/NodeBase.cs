
namespace Dependinator.Models;

abstract class NodeBase : IItem
{
    public readonly List<Node> children = new();
    public readonly List<Link> sourceLinks = new();
    public readonly List<Link> targetLinks = new();
    public readonly List<Line> sourceLines = new();
    public readonly List<Line> targetLines = new();

    protected NodeBase(NodeId id, string name, Node parent)
    {
        Id = id;
        Name = name;
        Parent = parent;
    }


    public NodeId Id { get; }
    public bool IsModified { get; protected set; } = true;
    public string Name { get; }
    public Node Parent { get; private set; }
    public bool IsRoot => Type == Parsing.NodeType.Root;
    public Parsing.NodeType Type { get; set; } = Parsing.NodeType.None;
    public string Description { get; set; } = "";


    public abstract string GetSvg(Pos parentCanvasPos, double parentZoom);

    public IEnumerable<IItem> AllItems()
    {
        foreach (var child in children)
        {
            yield return child;
        }
        foreach (var child in children)
        {
            foreach (var line in child.sourceLines)
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

    public bool AddSourceLink(Link link)
    {
        if (sourceLinks.Contains(link)) return false;
        sourceLinks.Add(link);
        return true;
    }

    public void AddTargetLink(Link link)
    {
        if (targetLinks.Contains(link)) return;
        targetLinks.Add(link);
    }


    public bool Update(Parsing.Node node)
    {
        if (IsEqual(node)) return false;
        Type = node.Type;
        Description = node.Description;
        return true;

    }


    public IEnumerable<Node> Ancestors()
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

