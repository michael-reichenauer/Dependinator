namespace Dependinator.Models;


interface IModel
{
    object SyncRoot { get; }
    Node Root { get; }
    Tiles Tiles { get; }
    IDictionary<Id, IItem> Items { get; }   // Ta bort
    bool IsSaving { get; set; }

    bool TryGetNode(NodeId id, out Node node);
    void AddNode(Node node);
    Node GetNode(NodeId id);
    void AddLink(Link link);
    Link GetLink(NodeId id);
    bool TryGetLink(NodeId id, out Link link);
    void AddLine(Line line);
    void Clear();
    void ClearCachedSvg();
    bool ContainsKey(Id linkId);
}


[Singleton]
class Model : IModel
{
    private readonly object syncRoot = new();

    public Model()
    {
        InitModel();
    }

    public object SyncRoot => syncRoot;
    public bool IsSaving { get; set; }

    public Tiles Tiles { get; } = new();

    public IDictionary<Id, IItem> Items { get; } = new Dictionary<Id, IItem>();

    public Node Root { get; private set; } = null!;

    public bool ContainsKey(Id id) => Items.ContainsKey(id);

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
        if (Items.ContainsKey(node.Id)) return;
        Items[node.Id] = node;
    }

    public Node GetNode(NodeId id) => (Node)Items[id];



    public void AddLink(Link link)
    {
        if (Items.ContainsKey(link.Id)) return;
        Items[link.Id] = link;
    }

    public Link GetLink(NodeId id) => (Link)Items[id];

    public bool TryGetLink(NodeId id, out Link link)
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
        if (Items.ContainsKey(line.Id)) return;
        Items[line.Id] = line;
    }


    public void Clear()
    {
        Items.Clear();
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

    static Node DefaultRootNode() => new("", null!)
    {
        Type = NodeType.Root,
        Boundary = new Rect(0, 0, 1000, 1000),
        ContainerZoom = 1
    };
}