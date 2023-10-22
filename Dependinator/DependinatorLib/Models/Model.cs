namespace Dependinator.Models;


class Model
{
    static readonly char[] PartsSeparators = "./".ToCharArray();

    public object SyncRoot { get; } = new object();
    readonly IDictionary<string, IItem> itemsDictionary = new Dictionary<string, IItem>();
    IDictionary<string, IItem> items
    {
        get
        {
            if (!Monitor.IsEntered(SyncRoot)) throw Asserter.FailFast("Model access outside lock");
            return itemsDictionary;
        }
    }

    public Node Root { get; internal set; }

    public bool TryGetNode(string name, out Node node) => (node = (Node)items[name]) != null;
    public Node Node(string name) => (Node)items[name];
    public void AddNode(Node node) => items[node.Name] = node;

    public bool TryGetLink(string id, out Link link) => (link = (Link)items[id]) != null;
    public Link Link(string id) => (Link)items[id];
    public void AddLink(string id, Link link) => items[id] = link;


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
            AddOrUpdateNode(Parsing.Node.Default(parsedLink.Source));
        }

        if (!items.ContainsKey(parsedLink.Target))
        {
            AddOrUpdateNode(Parsing.Node.Default(parsedLink.Target));
        }
    }


    Node GetParent(Parsing.Node parsedNode)
    {
        var parentName = GetParentName(parsedNode);
        if (!items.TryGetValue(parentName, out var item))
        {
            var parent = Parsing.Node.Default(parentName);
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
        int index = name.LastIndexOfAny(PartsSeparators);
        return index > -1 ? name[..index] : "";
    }
}



