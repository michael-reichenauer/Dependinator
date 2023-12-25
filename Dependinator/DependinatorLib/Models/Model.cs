namespace Dependinator.Models;


interface IModel : IDisposable
{
    Node Root { get; }
    object SyncRoot { get; }

    void AddOrUpdateLink(Parsing.Link parsedLink);
    void AddOrUpdateNode(Parsing.Node parsedNode);
    void AddLine(Line line);
    Node GetOrCreateNode(string name);
    void Clear();
}


class ModelBase : IModel
{
    readonly object syncRoot = new();
    readonly Dictionary<Id, IItem> items = new();


    public ModelBase()
    {
        Root = DefaultRootNode(this);
        items.Add(Root.Id, Root);
    }


    public object SyncRoot => syncRoot;
    public Node Root { get; internal set; }


    public void AddNode(Node node)
    {
        if (items.ContainsKey(node.Id)) return;
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
        items.Clear();
        Root = DefaultRootNode(this);
        items.Add(Root.Id, Root);
    }


    public void AddOrUpdateNode(Parsing.Node parsedNode)
    {
        if (!TryGetNode(new NodeId(parsedNode.Name), out var node))
        {   // New node, add it to the model and parent
            var parentName = parsedNode.ParentName;
            var parent = GetOrCreateParent(parentName);

            var boundary = NodeLayout.GetNextChildRect(parent);
            node = new Node(parsedNode.Name, parent)
            {
                Type = parsedNode.Type,
                Description = parsedNode.Description,
                Boundary = boundary,
            };

            AddNode(node);
            parent.AddChild(node);

            return;
        }

        if (node.Update(parsedNode))
        {
            var parentName = parsedNode.ParentName;
            if (node.Parent.Name != parentName)
            {   // The node has changed parent, remove it from the old parent and add it to the new parent
                node.Parent.RemoveChild(node);
                var parent = GetOrCreateNode(parentName);
                parent.AddChild(node);
            }
        }
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
        target.AddTargetLink(link);
        if (source.AddSourceLink(link))
        {
            AddLinesFromSourceToTarget(link);
        }
        return;
    }


    void AddLinesFromSourceToTarget(Link link)
    {
        Node commonAncestor = GetCommonAncestor(link);

        // Add lines from source and target nodes upp to its parent for all ancestors until just before the common ancestor
        var sourceAncestor = AddAncestorLines(link, link.Source, commonAncestor);
        var targetAncestor = AddAncestorLines(link, link.Target, commonAncestor);

        // Connect 'sibling' nodes that are ancestors to source and target (or are source/target if they are siblings)
        AddDirectLine(sourceAncestor, targetAncestor, link);
    }


    static Node GetCommonAncestor(Link link)
    {
        var targetAncestors = link.Target.Ancestors().ToList();
        return link.Source.Ancestors().First(targetAncestors.Contains);
    }

    Node AddAncestorLines(Link link, Node source, Node commonAncestor)
    {
        // Add lines from source node upp to all ancestors until just before common ancestors
        Node currentSource = source;
        foreach (var parent in source.Ancestors())
        {
            if (parent == commonAncestor) break;
            AddDirectLine(currentSource, parent, link);
            currentSource = parent;
        }

        return currentSource;
    }


    void AddDirectLine(Node source, Node target, Link link)
    {
        var line = source.SourceLines.FirstOrDefault(l => l.Target == target);
        if (line == null)
        {   // First line between these source and target
            line = new Line(source, target);
            source.SourceLines.Add(line);
            target.TargetLines.Add(line);

            AddLine(line);
        }

        line.Add(link);
        link.AddLine(line);
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

    static Node DefaultRootNode(IModel model) => new("", null!)
    {
        Type = Parsing.NodeType.Root,
        Boundary = new Rect(0, 0, 1000, 1000),
        ContainerZoom = 1
    };

    public void Dispose()
    {
        // Implemented in ModelTransaction
    }
}