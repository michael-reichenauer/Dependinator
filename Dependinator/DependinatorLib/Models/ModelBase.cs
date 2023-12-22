namespace Dependinator.Models;

record Level(string Svg, double Zoom);

record Svgs(IReadOnlyList<Level> levels)
{
    public (string, double, int) Get(double zoom)
    {
        if (levels.Count == 0) return ("", 1.0, 0);

        int level = 0;

        for (int i = 1; i < levels.Count; i++)
        {
            if (zoom >= levels[i].Zoom) break;
            level = i;
        }

        return (levels[level].Svg, levels[level].Zoom, level);
    }
}


class ModelBase
{
    readonly object syncRoot = new();
    readonly Dictionary<Id, IItem> itemsDictionary = new();
    Dictionary<Id, IItem> items
    {
        get
        {
            if (!Monitor.IsEntered(SyncRoot)) throw Asserter.FailFast("Model access outside lock");
            return itemsDictionary;
        }
    }


    public ModelBase()
    {
        Root = DefaultRootNode(this);
        itemsDictionary.Add(Root.Id, Root);
        NodeCount = 1;
    }


    public object SyncRoot => syncRoot;
    public Node Root { get; internal set; }
    public Random Random { get; } = new Random();
    public bool IsModified { get; internal set; }
    public IReadOnlyDictionary<Id, IItem> Items => items;
    public int NodeCount { get; internal set; } = 0;
    public int LinkCount { get; internal set; } = 0;
    public int LineCount { get; internal set; } = 0;

    internal R<Node> FindNode(Pos offset, Pos point, double zoom)
    {
        // transform point to canvas coordinates
        var canvasPoint = new Pos((point.X + offset.X) * zoom, (point.Y + offset.Y) * zoom);
        return Root.FindNode(Pos.Zero, canvasPoint, zoom);
    }


    internal (Svgs, Rect) GetSvg()
    {
        using var t = Timing.Start();

        var svgs = new List<Level>();

        for (int i = 0; i < 100; i++)
        {
            var zoom = i == 0 ? 1.0 : Math.Pow(2, i);
            var svg = Root.GetSvg(Pos.Zero, zoom);
            if (svg == "") break;
            svgs.Add(new Level(svg, 1 / zoom));
            // Log.Info($"Level: #{i} zoom: {zoom} svg: {svg.Length} chars");
        }
        Log.Info($"Levels: {svgs.Count}, Nodes: {NodeCount}, Links: {LinkCount}, Lines: {LineCount}");

        var totalBoundary = Root.TotalBoundary;
        return (new Svgs(svgs), totalBoundary);
    }


    public void AddNode(Node node)
    {
        if (items.ContainsKey(node.Id)) return;
        NodeCount++;
        items[node.Id] = node;
    }

    public Node GetNode(NodeId id) => (Node)items[id];
    public bool TryGetNode(NodeId id, out Node node)
    {
        if (!items.TryGetValue(id, out var item))
        {
            node = null!;
            return false;
        }
        node = (Node)item;
        return true;
    }

    public void AddLink(Link link)
    {
        if (items.ContainsKey(link.Id)) return;
        LinkCount++;
        items[link.Id] = link;
    }

    public Link GetLink(NodeId id) => (Link)items[id];
    public bool TryGetLink(NodeId id, out Link link)
    {
        if (!items.TryGetValue(id, out var item))
        {
            link = null!;
            return false;
        }
        link = (Link)item;
        return true;
    }


    public void AddLine(Line line)
    {
        if (items.ContainsKey(line.Id)) return;
        LineCount++;
        items[line.Id] = line;
    }


    public Node GetOrCreateNode(string name)
    {
        var nodeId = new NodeId(name);
        if (!items.TryGetValue(nodeId, out var item))
        {
            var parent = DefaultParsingNode(name);
            AddOrUpdateNode(parent);
            return (Node)items[nodeId];
        }

        return (Node)item;
    }

    public Node GetOrCreateParent(string name)
    {
        var nodeId = new NodeId(name);
        if (!items.TryGetValue(nodeId, out var item))
        {
            var parent = DefaultParentNode(name);
            AddOrUpdateNode(parent);
            return (Node)items[nodeId];
        }

        return (Node)item;
    }

    public void Clear()
    {
        itemsDictionary.Clear();
        Root = DefaultRootNode(this);
        itemsDictionary.Add(Root.Id, Root);
        NodeCount = 1;
        LinkCount = 0;
    }


    public void AddOrUpdateNode(Parsing.Node parsedNode)
    {
        if (!TryGetNode(new NodeId(parsedNode.Name), out var node))
        {   // New node, add it to the model and parent
            var parentName = parsedNode.ParentName;
            var parent = GetOrCreateParent(parentName);

            var boundary = NodeLayout.GetNextChildRect(parent);
            node = new Node(parsedNode.Name, parent, this)
            {
                Type = parsedNode.Type,
                Description = parsedNode.Description,
                Boundary = boundary,
            };

            AddNode(node);
            parent.AddChild(node);

            return;
        }

        node.Update(parsedNode);
    }

    public void AddOrUpdateLink(Parsing.Link parsedLink)
    {
        var linkId = new LinkId(parsedLink.SourceName, parsedLink.TargetName);
        if (items.ContainsKey(linkId)) return;

        EnsureSourceAndTargetExists(parsedLink);

        var source = GetNode(new NodeId(parsedLink.SourceName));
        var target = GetNode(new NodeId(parsedLink.TargetName));
        var link = new Link(source, target);

        AddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        return;
    }

    void EnsureSourceAndTargetExists(Parsing.Link parsedLink)
    {
        if (!items.ContainsKey(new NodeId(parsedLink.SourceName)))
        {
            AddOrUpdateNode(DefaultParsingNode(parsedLink.SourceName));
        }

        if (!items.ContainsKey(new NodeId(parsedLink.TargetName)))
        {
            AddOrUpdateNode(DefaultParsingNode(parsedLink.TargetName));
        }
    }


    static Parsing.Node DefaultParentNode(string name) =>
        new(name, Parsing.Node.ParseParentName(name), Parsing.NodeType.Parent, "");
    static Parsing.Node DefaultParsingNode(string name) =>
        new(name, Parsing.Node.ParseParentName(name), Parsing.NodeType.None, "");

    static Node DefaultRootNode(ModelBase model) => new("", null!, model)
    {
        Type = Parsing.NodeType.Root,
        Boundary = new Rect(0, 0, 1000, 1000),
        ContainerZoom = 1
    };
}
