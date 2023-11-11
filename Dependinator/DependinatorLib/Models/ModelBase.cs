namespace Dependinator.Models;

record Level(string Svg, double Zoom);

record Svgs(IReadOnlyList<Level> levels)
{
    public (string, double) Get(double zoom)
    {
        if (levels.Count == 0) return ("", 1.0);

        var level = levels[0];
        for (int i = 1; i < levels.Count; i++)
        {
            if (zoom >= levels[i].Zoom) break;
            level = levels[i];
        }

        return (level.Svg, level.Zoom);
    }
}

class ModelBase
{
    readonly object syncRoot = new();
    readonly Dictionary<string, IItem> itemsDictionary = new();
    Dictionary<string, IItem> items
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
        itemsDictionary.Add(Root.Name, Root);
        NodeCount = 1;
    }


    public object SyncRoot => syncRoot;
    public Node Root { get; internal set; }
    public Random Random { get; } = new Random();
    public bool IsModified { get; internal set; }
    public IReadOnlyDictionary<string, IItem> Items => items;
    public int NodeCount { get; internal set; } = 0;
    public int LinkCount { get; internal set; } = 0;


    internal (Svgs, Rect) GetSvg()
    {
        using var t = Timing.Start();

        var svgs = new List<Level>();

        for (int i = 0; i < 100; i++)
        {
            var zoom = i == 0 ? 1.0 : Math.Pow(2, i);
            var svg = Root.Children.Select(n => n.GetSvg(Pos.Zero, zoom)).Join("\n").Trim();
            if (svg == "") break;
            svgs.Add(new Level(svg, 1 / zoom));
            //Log.Info($"Level: #{i} zoom: {zoom} svg: {svg.Length} chars");
        }
        Log.Info($"Levels: {svgs.Count}");

        var totalBoundary = Root.TotalBoundary;
        return (new Svgs(svgs), totalBoundary);
    }


    public void AddNode(Node node)
    {
        if (!items.ContainsKey(node.Name)) NodeCount++;
        items[node.Name] = node;
    }

    public Node GetNode(string name) => (Node)items[name];
    public bool TryGetNode(string name, out Node node)
    {
        if (!items.TryGetValue(name, out var item))
        {
            node = null!;
            return false;
        }
        node = (Node)item;
        return true;
    }

    public void AddLink(string id, Link link)
    {
        if (!items.ContainsKey(id)) LinkCount++;
        items[id] = link;
    }

    public Link Link(string id) => (Link)items[id];
    public bool TryGetLink(string id, out Link link)
    {
        if (!items.TryGetValue(id, out var item))
        {
            link = null!;
            return false;
        }
        link = (Link)item;
        return true;
    }


    public Node GetOrCreateNode(string name)
    {
        if (!items.TryGetValue(name, out var item))
        {
            var parent = DefaultParsingNode(name);
            AddOrUpdateNode(parent);
            return (Node)items[name];
        }

        return (Node)item;
    }

    public Node GetOrCreateParent(string name)
    {
        if (!items.TryGetValue(name, out var item))
        {
            var parent = DefaultParentNode(name);
            AddOrUpdateNode(parent);
            return (Node)items[name];
        }

        return (Node)item;
    }

    public void Clear()
    {
        itemsDictionary.Clear();
        Root = DefaultRootNode(this);
        itemsDictionary.Add(Root.Name, Root);
        NodeCount = 1;
        LinkCount = 0;
    }


    public void AddOrUpdateNode(Parsing.Node parsedNode)
    {
        if (!TryGetNode(parsedNode.Name, out var node))
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
        var linkId = parsedLink.Source + parsedLink.Target;
        if (items.ContainsKey(linkId)) return;

        EnsureSourceAndTargetExists(parsedLink);

        var source = GetNode(parsedLink.Source);
        var target = GetNode(parsedLink.Target);
        var link = new Link(source, target);

        AddLink(linkId, link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        return;
    }

    void EnsureSourceAndTargetExists(Parsing.Link parsedLink)
    {
        if (!items.ContainsKey(parsedLink.Source))
        {
            AddOrUpdateNode(DefaultParsingNode(parsedLink.Source));
        }

        if (!items.ContainsKey(parsedLink.Target))
        {
            AddOrUpdateNode(DefaultParsingNode(parsedLink.Target));
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