namespace Dependinator.Models;

class Node : IItem
{
    readonly List<Node> children = new();
    readonly List<Link> sourceLinks = new();
    readonly List<Link> targetLinks = new();
    readonly Model model;
    string typeName = "";
    string cachedSvg = "";
    bool isCached = false;

    public string ContentSvg => isCached ? cachedSvg : GenerateAndCacheSvg();

    public string Name { get; }
    public Node Parent { get; private set; }
    public NodeType Type { get; set; } = NodeType.None;

    public Rect Rect { get; set; } = new(0, 0, 0, 0);
    public Rect TotalRect { get; set; } = new(0, 0, 0, 0);
    public string Description { get; set; } = "";
    public int RX { get; set; } = 5;
    public string Color { get; set; } = "";
    public string Background { get; set; } = "green";

    public IReadOnlyList<Node> Children => children;
    public IReadOnlyList<Link> SourceLinks => sourceLinks;
    public IReadOnlyList<Link> TargetLinks => targetLinks;


    public Node(string name, Node parent, Model model)
    {
        this.Name = name;
        Parent = parent;
        this.model = model;

        Color = RandomColor();
        Background = RandomColor();
    }

    public void AddChild(Node child)
    {
        AdjustChildPosition(child);

        children.Add(child);
        child.Parent = this;
        TotalRect = TotalRect with
        {
            X = Math.Min(TotalRect.X, child.Rect.X),
            Y = Math.Min(TotalRect.Y, child.Rect.Y),
            Width = Math.Max(TotalRect.Width, child.Rect.X + child.Rect.Width),
            Height = Math.Max(TotalRect.Height, child.Rect.Y + child.Rect.Height)
        };

        Updated();
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
        isCached = false;
        Parent?.Updated();
    }

    string GenerateAndCacheSvg()
    {
        var svg = $"""<rect x="{Rect.X}" y="{Rect.Y}" width="{Rect.Width}" height="{Rect.Height}" rx="{RX}" fill="{Background}" fill-opacity="0.2" stroke="{Color}" stroke-width="2"/>""";

        cachedSvg = children.Select(n => n.ContentSvg).Prepend(svg).Join("\n");
        isCached = true;
        return cachedSvg;
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

    void AdjustChildPosition(Node child)
    {
        child.Rect = new Rect(
          X: model.Random.Next(0, 1000),
          Y: model.Random.Next(0, 1000),
          Width: model.Random.Next(20, 100),
          Height: model.Random.Next(20, 100));
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

    string RandomColor() => $"#{model.Random.Next(0, 256):x2}{model.Random.Next(0, 256):x2}{model.Random.Next(0, 256):x2}";
}

