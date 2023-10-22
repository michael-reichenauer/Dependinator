namespace Dependinator.Models;

interface IModelDb
{
    IReadOnlyList<IItem> AddOrUpdate(IReadOnlyList<Parsing.IItem> items);
}


[Singleton]
class ModelDb : IModelDb
{
    static readonly char[] PartsSeparators = "./".ToCharArray();

    readonly object root = new object();
    readonly IDictionary<string, IItem> items = new Dictionary<string, IItem>();


    public IReadOnlyList<IItem> AddOrUpdate(IReadOnlyList<Parsing.IItem> parsedItems)
    {
        lock (root)
        {
            var updatedItems = new List<IItem>();
            foreach (var parsedItem in parsedItems)
            {
                switch (parsedItem)
                {
                    case Parsing.Node parsedNode:
                        HandleNode(parsedNode, updatedItems);
                        //Log.Info($"Node: {parsedNode}");
                        break;

                    case Parsing.Link parsedLink:
                        HandleLink(parsedLink, updatedItems);
                        // Log.Info($"Link: {parsedLink}");
                        break;
                }
            }

            return updatedItems;
        }
    }

    private void HandleNode(Parsing.Node parsedNode, List<IItem> updatedItems)
    {
        var nodeId = parsedNode.Name;
        if (!TryGetNode(nodeId, out var node))
        {
            EnsureParentExists(parsedNode, updatedItems);

            var newNode = new Node(parsedNode.Name, parsedNode.Parent, parsedNode.Type, parsedNode.Description);
            this.items[nodeId] = newNode;
            updatedItems.Add(newNode);
            return;
        }

        if (!NeedsUpdating(node, parsedNode)) return;

        var updatedNode = node with
        {
            Name = parsedNode.Name,
            Parent = parsedNode.Parent,
            Type = parsedNode.Type,
            Description = parsedNode.Description
        };

        this.items[nodeId] = updatedNode;
        updatedItems.Add(updatedNode);
    }



    void HandleLink(Parsing.Link parsedLink, List<IItem> updatedItems)
    {
        var linkId = parsedLink.Source + parsedLink.Target;

        EnsureSourceAndTargetExists(parsedLink, updatedItems);

        if (!TryGetLink(linkId, out var link))
        {
            var newLink = new Link(parsedLink.Source, parsedLink.Target);
            this.items[linkId] = newLink;
            updatedItems.Add(newLink);
            return;
        }

        if (!NeedsUpdating(link, parsedLink)) return;

        var updatedLink = link with
        {
            Source = parsedLink.Source,
            Target = parsedLink.Target,
        };

        this.items[linkId] = updatedLink;
        updatedItems.Add(updatedLink);
    }

    void EnsureParentExists(Parsing.Node parsedNode, List<IItem> updatedItems)
    {
        if (parsedNode.Name == "") return; // Root node has no parent

        var parentName = parsedNode.Parent != "" ? parsedNode.Parent : GetParentName(parsedNode.Name);
        if (!items.ContainsKey(parentName))
        {
            HandleNode(DefaultNode(parentName), updatedItems);
        }
    }

    void EnsureSourceAndTargetExists(Parsing.Link parsedLink, List<IItem> updatedItems)
    {
        if (!items.ContainsKey(parsedLink.Source))
        {
            HandleNode(DefaultNode(parsedLink.Source), updatedItems);
        }

        if (!items.ContainsKey(parsedLink.Target))
        {
            HandleNode(DefaultNode(parsedLink.Target), updatedItems);
        }
    }

    bool TryGetNode(string id, out Node node)
    {
        if (!items.TryGetValue(id, out var item))
        {
            node = null!;
            return false;

        }

        node = (Node)item;
        return true;
    }

    bool TryGetLink(string id, out Link link)
    {
        if (!items.TryGetValue(id, out var item))
        {
            link = null!;
            return false;

        }

        link = (Link)item;
        return true;
    }


    static Parsing.Node DefaultNode(string name) => new Parsing.Node(name, "", "", "");

    static string GetParentName(string name)
    {
        // Split full name in name and parent name,
        int index = name.LastIndexOfAny(PartsSeparators);
        return index > -1 ? name[..index] : "";
    }


    static bool NeedsUpdating(Node node, Parsing.Node n) =>
        node.Name != n.Name
        || node.Parent != n.Parent
        || node.Type != n.Type
        || node.Description != n.Description;


    static bool NeedsUpdating(Link link, Parsing.Link l) =>
        link.Source != l.Source
        || link.Target != l.Target;
}
