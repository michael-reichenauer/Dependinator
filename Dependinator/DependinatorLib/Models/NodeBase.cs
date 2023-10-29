
namespace Dependinator.Models;

abstract class NodeBase : IItem
{
    readonly List<Node> children = new();
    readonly List<Link> sourceLinks = new();
    readonly List<Link> targetLinks = new();
    protected readonly ModelBase model;
    string typeName = "";


    public NodeBase(string name, Node parent, ModelBase model)
    {
        Name = name;
        Parent = parent;
        this.model = model;
    }

    public bool IsModified { get; protected set; } = true;
    public string Name { get; }
    public Node Parent { get; private set; }
    public bool IsRoot => Type == NodeType.Root;
    public NodeType Type { get; set; } = NodeType.None;
    public string Description { get; set; } = "";

    public IReadOnlyList<Node> Children => children;
    public IReadOnlyList<Link> SourceLinks => sourceLinks;
    public IReadOnlyList<Link> TargetLinks => targetLinks;

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
        if (!sourceLinks.Contains(link)) sourceLinks.Add(link);
    }

    public void AddTargetLink(Link link)
    {
        if (!targetLinks.Contains(link)) targetLinks.Add(link);
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

        typeName = node.Type;
        Type = ToNodeType(typeName);
        Description = node.Description;
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
        typeName == n.Type &&
        Description == n.Description;

    static NodeType ToNodeType(string nodeTypeName) => nodeTypeName switch
    {
        "" => NodeType.None,
        "Solution" => NodeType.Solution,
        "SolutionFolder" => NodeType.SolutionFolder,
        "Assembly" => NodeType.Assembly,
        "Group" => NodeType.Group,
        "Dll" => NodeType.Dll,
        "Exe" => NodeType.Exe,
        "NameSpace" => NodeType.NameSpace,
        "Type" => NodeType.Type,
        "Member" => NodeType.Member,
        "PrivateMember" => NodeType.PrivateMember,
        _ => throw Asserter.FailFast($"Unexpected type {nodeTypeName}")
    };
}

