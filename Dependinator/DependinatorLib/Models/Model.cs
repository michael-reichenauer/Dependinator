namespace Dependinator.Models;


class Model
{
    static readonly char[] NamePartsSeparators = "./".ToCharArray();

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

    readonly Random random = new Random();


    public object SyncRoot => syncRoot;
    public Node Root { get; internal set; }
    public Random Random => random;
    public IReadOnlyDictionary<string, IItem> Items => items;


    public void AddNode(Node node) => items[node.Name] = node;
    public Node Node(string name) => (Node)items[name];
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

    public void AddLink(string id, Link link) => items[id] = link;
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


    public Model()
    {
        Root = new Node("", null!, this) { Type = NodeType.Root };
        itemsDictionary.Add(Root.Name, Root);
    }


    public void AddOrUpdateNode(Parsing.Node parsedNode)
    {
        if (!TryGetNode(parsedNode.Name, out var node))
        {   // New node, add it to the model and parent
            var parent = GetParent(parsedNode);
            node = new Node(parsedNode.Name, parent, this)
            {
                Description = parsedNode.Description
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

        var source = Node(parsedLink.Source);
        var target = Node(parsedLink.Target);
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
            AddOrUpdateNode(DefaultNode(parsedLink.Source));
        }

        if (!items.ContainsKey(parsedLink.Target))
        {
            AddOrUpdateNode(DefaultNode(parsedLink.Target));
        }
    }


    Node GetParent(Parsing.Node parsedNode)
    {
        var parentName = GetParentName(parsedNode);
        if (!items.TryGetValue(parentName, out var item))
        {
            var parent = DefaultNode(parentName);
            AddOrUpdateNode(parent);
            return (Node)items[parentName];
        }

        return (Node)item;
    }


    static string GetParentName(Parsing.Node node) =>
        node.Parent != "" ? node.Parent : GetParentName(node.Name);


    static string GetParentName(string name)
    {
        // Split full name in name and parent name,
        int index = name.LastIndexOfAny(NamePartsSeparators);
        return index > -1 ? name[..index] : "";
    }

    static Parsing.Node DefaultNode(string name) => new Parsing.Node(name, "", "", "");
}



