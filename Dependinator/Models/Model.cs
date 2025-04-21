namespace Dependinator.Models;

interface IModel : IDisposable
{
    string Path { get; set; }
    object Lock { get; }
    Node Root { get; }
    Rect ViewRect { get; set; }
    double Zoom { get; set; }
    Pos Offset { get; set; }
    Tiles Tiles { get; }
    IDictionary<Id, IItem> Items { get; } // Ta bort
    bool IsSaving { get; set; }
    DateTime ModifiedTime { get; set; }
    CancellationTokenSource SaveCancelSource { get; set; }

    bool TryGetNode(string id, out Node node);
    bool TryGetNode(NodeId id, out Node node);
    void AddNode(Node node);
    Node GetNode(NodeId id);
    void AddLink(Link link);
    Link GetLink(LinkId id);
    bool TryGetLink(LinkId id, out Link link);
    void AddLine(Line line);
    Line GetLine(LineId id);
    bool TryGetLine(LineId id, out Line line);
    bool TryGetLine(string id, out Line line);
    void Clear();
    void ClearCachedSvg();
    bool ContainsKey(Id linkId);
}

[Scoped]
class Model : IModel
{
    private readonly object syncRoot = new();

    public Model()
    {
        InitModel();
    }

    public string Path { get; set; } = "";
    public object Lock => syncRoot;
    public bool IsSaving { get; set; } = false;

    public Rect ViewRect { get; set; } = Rect.None;
    double zoom = 0;
    Pos offset = Pos.None;

    public double Zoom
    {
        get => zoom;
        set
        {
            // Log.Info($"Set model zoom to {value}\n{Util.CallStack(4)}");
            zoom = value;
        }
    }
    public Pos Offset
    {
        get => offset;
        set
        {
            // Log.Info($"Set model offset to {value}\n{Util.CallStack(4)}");
            offset = value;
        }
    }

    public Tiles Tiles { get; } = new();

    public IDictionary<Id, IItem> Items { get; } = new Dictionary<Id, IItem>();

    public Node Root { get; private set; } = null!;
    public DateTime ModifiedTime { get; set; } = DateTime.MinValue;
    public CancellationTokenSource SaveCancelSource { get; set; } = new();

    public void Dispose()
    {
        try
        {
            Monitor.Exit(Lock);
        }
        catch
        {
            // Ignore, already released (when shutting down and DI calls dispose)
        }
    }

    public bool ContainsKey(Id id) => Items.ContainsKey(id);

    public bool TryGetNode(string id, out Node node)
    {
        return TryGetNode(NodeId.FromId(id), out node);
    }

    public bool TryGetNode(NodeId id, out Node node)
    {
        if (!Items.TryGetValue(id, out var item))
        {
            node = null!;
            return false;
        }
        node = (Node)item;
        return true;
    }

    public void AddNode(Node node)
    {
        if (Items.ContainsKey(node.Id))
            return;
        Items[node.Id] = node;
    }

    public Node GetNode(NodeId id) => (Node)Items[id];

    public void AddLink(Link link)
    {
        if (Items.ContainsKey(link.Id))
            return;
        Items[link.Id] = link;
    }

    public Link GetLink(LinkId id) => (Link)Items[id];

    public bool TryGetLink(LinkId id, out Link link)
    {
        if (!Items.TryGetValue(id, out var item))
        {
            link = null!;
            return false;
        }
        link = (Link)item;
        return true;
    }

    public void AddLine(Line line)
    {
        if (Items.ContainsKey(line.Id))
            return;
        Items[line.Id] = line;
    }

    public Line GetLine(LineId id) => (Line)Items[id];

    public bool TryGetLine(string id, out Line line)
    {
        return TryGetLine(LineId.FromId(id), out line);
    }

    public bool TryGetLine(LineId id, out Line link)
    {
        if (!Items.TryGetValue(id, out var item))
        {
            link = null!;
            return false;
        }
        link = (Line)item;
        return true;
    }

    public void Clear()
    {
        Items.Clear();
        Path = "";
        IsSaving = false;
        ModifiedTime = DateTime.MinValue;
        ViewRect = Rect.None;
        Zoom = 0;
        Offset = Pos.None;
        ClearCachedSvg();

        InitModel();
    }

    public void ClearCachedSvg()
    {
        Tiles.Clear();
    }

    void InitModel()
    {
        Root = DefaultRootNode();
        Items[Root.Id] = Root;
    }

    static Node DefaultRootNode() =>
        new("", null!)
        {
            Type = NodeType.Root,
            Boundary = new Rect(0, 0, 1000, 1000),
            ContainerZoom = 1,
        };
}
