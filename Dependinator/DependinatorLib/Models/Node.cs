namespace Dependinator.Models;

class Node : IItem
{
    readonly List<Node> children = new();
    readonly List<Link> sourceLinks = new();
    readonly List<Link> targetLinks = new();
    readonly Model model;
    string typeName = "";
    string svg = "";

    public string Name { get; }
    public Node Parent { get; private set; }
    public NodeType Type { get; set; } = NodeType.None;


    public string Description { get; set; } = "";

    public IReadOnlyList<Node> Children => children;
    public IReadOnlyList<Link> SourceLinks => sourceLinks;
    public IReadOnlyList<Link> TargetLinks => targetLinks;



    public Node(string name, Node parent, Model model)
    {
        this.Name = name;
        Parent = parent;
        this.model = model;
    }

    public void AddChild(Node child)
    {
        children.Add(child);
        child.Parent = this;
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

        if (Parent.Name != node.Parent)
        {   // The node has changed parent, remove it from the old parent and add it to the new parent
            Parent.RemoveChild(this);
            Parent = model.Node(node.Parent);
            Parent.AddChild(this);
        }

        typeName = node.Type;
        Type = ToNodeType(typeName);
        Description = node.Description;

        Updated();
    }


    public void Updated()
    {
        svg = "";

    }

    bool IsEqual(Parsing.Node n) =>
        Parent.Name == n.Parent &&
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

