namespace Dependinator.Models;

class Node : IItem
{
    readonly List<Node> children = new();
    readonly List<Link> sourceLinks = new();
    readonly List<Link> targetLinks = new();
    readonly RootModel model;
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
    public double FillOpacity { get; set; } = 0.2;
    public double StrokeWidth { get; set; } = 1;
    public bool IsRoot => Type == NodeType.Root;

    public IReadOnlyList<Node> Children => children;
    public IReadOnlyList<Link> SourceLinks => sourceLinks;
    public IReadOnlyList<Link> TargetLinks => targetLinks;


    public Node(string name, Node parent, RootModel model)
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

        SetIsModified();
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
        //if (IsEqual(node)) return;
        Color = RandomColor();
        Background = RandomColor();

        var parentName = node.ParentName;
        if (Parent.Name != parentName)
        {   // The node has changed parent, remove it from the old parent and add it to the new parent
            Parent.RemoveChild(this);
            Parent = model.GetOrCreateNode(parentName);
            Parent.AddChild(this);
        }

        typeName = node.Type;
        Type = ToNodeType(typeName);
        Description = node.Description;

        SetIsModified();
    }


    public void SetIsModified()
    {
        isCached = false;
        Parent?.SetIsModified();
    }

    string GenerateAndCacheSvg()
    {
        Timing t = IsRoot ? Timing.Start() : null!;
        try
        {
            var svg = IsRoot ? "" :
                $"""<rect x="{Rect.X}" y="{Rect.Y}" width="{Rect.Width}" height="{Rect.Height}" rx="{RX}" fill="{Background}" fill-opacity="{FillOpacity}" stroke="{Color}" stroke-width="{StrokeWidth}"/>""";

            cachedSvg = children.Select(n => n.ContentSvg).Prepend(svg).Join("\n");
            // cachedSvg = $"""<rect x="{100}" y="{100}" width="{100}" height="{100}" rx="{RX}" fill="{Background}" fill-opacity="0.2" stroke="{Color}" stroke-width="0.001"/>""";

            isCached = true;
            return cachedSvg;
        }
        finally
        {
            t?.Dispose();
        }
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

    string RandomColor() => $"#{model.Random.Next(0, 256):x2}{model.Random.Next(0, 256):x2}{model.Random.Next(0, 256):x2}";
}

