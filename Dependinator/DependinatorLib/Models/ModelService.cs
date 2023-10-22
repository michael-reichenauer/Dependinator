using System.Diagnostics;
using System.Threading.Channels;


namespace Dependinator.Models;


interface IItem { }
record Node(string Name, string Parent, string Type, string Description) : IItem;
record Link(string Source, string Target) : IItem;
record Source(string Path, string Text, int LineNumber);

interface IModelService
{
    R<ChannelReader<IItem>> Refresh();
}



[Scoped]
class ModelService : IModelService
{
    static readonly char[] PartsSeparators = "./".ToCharArray();
    const int BatchTimeMs = 300;
    readonly Parsing.IParserService parserService;
    private readonly IModelDb modelDb;


    public ModelService(Parsing.IParserService parserService, IModelDb modelDb)
    {
        this.parserService = parserService;
        this.modelDb = modelDb;
    }


    public R<ChannelReader<IItem>> Refresh()
    {
        var path = "/workspaces/Dependinator/Dependinator/Dependinator.sln";
        Channel<IItem> channel = Channel.CreateUnbounded<IItem>();

        if (!Try(out var reader, out var e, parserService.Parse(path))) return e;

        Task.Run(async () =>
        {
            using var _ = Timing.Start();

            while (await reader.WaitToReadAsync())
            {
                var batchStart = Stopwatch.StartNew();
                var batchItems = new List<Parsing.IItem>();
                while (batchStart.ElapsedMilliseconds < BatchTimeMs && reader.TryRead(out var item))
                {
                    batchItems.Add(item);
                }

                var updatedItems = AddOrUpdate(batchItems);
                foreach (var item in updatedItems) await channel.Writer.WriteAsync(item);
            }

            channel.Writer.Complete();
        });

        return channel.Reader;
    }

    public IReadOnlyList<IItem> AddOrUpdate(IReadOnlyList<Parsing.IItem> parsedItems)
    {
        using var model = modelDb.GetModel();

        var updatedItems = new List<IItem>();
        foreach (var parsedItem in parsedItems)
        {
            switch (parsedItem)
            {
                case Parsing.Node parsedNode:
                    HandleNode(model, parsedNode, updatedItems);
                    //Log.Info($"Node: {parsedNode}");
                    break;

                case Parsing.Link parsedLink:
                    HandleLink(model, parsedLink, updatedItems);
                    // Log.Info($"Link: {parsedLink}");
                    break;
            }
        }

        return updatedItems;

    }

    void HandleNode(Model model, Parsing.Node parsedNode, List<IItem> updatedItems)
    {
        var nodeId = parsedNode.Name;
        if (!TryGetNode(model, nodeId, out var node))
        {
            EnsureParentExists(model, parsedNode, updatedItems);

            var newNode = new Node(parsedNode.Name, parsedNode.Parent, parsedNode.Type, parsedNode.Description);
            model.Items[nodeId] = newNode;
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

        model.Items[nodeId] = updatedNode;
        updatedItems.Add(updatedNode);
    }


    void HandleLink(Model model, Parsing.Link parsedLink, List<IItem> updatedItems)
    {
        var linkId = parsedLink.Source + parsedLink.Target;

        EnsureSourceAndTargetExists(model, parsedLink, updatedItems);

        if (!TryGetLink(model, linkId, out var link))
        {
            var newLink = new Link(parsedLink.Source, parsedLink.Target);
            model.Items[linkId] = newLink;
            updatedItems.Add(newLink);
            return;
        }

        if (!NeedsUpdating(link, parsedLink)) return;

        var updatedLink = link with
        {
            Source = parsedLink.Source,
            Target = parsedLink.Target,
        };

        model.Items[linkId] = updatedLink;
        updatedItems.Add(updatedLink);
    }

    void EnsureParentExists(Model model, Parsing.Node parsedNode, List<IItem> updatedItems)
    {
        if (parsedNode.Name == "") return; // Root node has no parent

        var parentName = parsedNode.Parent != "" ? parsedNode.Parent : GetParentName(parsedNode.Name);
        if (!model.Items.ContainsKey(parentName))
        {
            HandleNode(model, DefaultNode(parentName), updatedItems);
        }
    }

    void EnsureSourceAndTargetExists(Model model, Parsing.Link parsedLink, List<IItem> updatedItems)
    {
        if (!model.Items.ContainsKey(parsedLink.Source))
        {
            HandleNode(model, DefaultNode(parsedLink.Source), updatedItems);
        }

        if (!model.Items.ContainsKey(parsedLink.Target))
        {
            HandleNode(model, DefaultNode(parsedLink.Target), updatedItems);
        }
    }

    bool TryGetNode(Model model, string id, out Node node)
    {
        if (!model.Items.TryGetValue(id, out var item))
        {
            node = null!;
            return false;

        }

        node = (Node)item;
        return true;
    }

    bool TryGetLink(Model model, string id, out Link link)
    {
        if (!model.Items.TryGetValue(id, out var item))
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