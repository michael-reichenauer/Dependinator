using System.Diagnostics;


namespace Dependinator.Models;


interface IModelService
{
    Task<R> RefreshAsync();
    Tile GetTile(Rect viewRect, double zoom);
    bool TryGetNode(string id, out Node node);
    void Clear();
}


[Transient]
class ModelService : IModelService
{
    readonly IModel model;
    readonly Parsing.IParserService parserService;
    readonly IModelStructureService modelStructureService;
    readonly IModelSvgService modelSvgService;

    public ModelService(
        IModel model,
        Parsing.IParserService parserService,
        IModelStructureService modelStructureService,
        IModelSvgService modelSvgService)
    {
        this.model = model;
        this.parserService = parserService;
        this.modelStructureService = modelStructureService;
        this.modelSvgService = modelSvgService;
    }

    public bool TryGetNode(string id, out Node node)
    {
        lock (model.SyncRoot)
        {
            return model.TryGetNode(NodeId.FromId(id), out node);
        }
    }

    public Tile GetTile(Rect viewRect, double zoom)
    {
        lock (model.SyncRoot)
        {
            return modelSvgService.GetSvg(viewRect, zoom);
        }
    }


    public void Clear()
    {
        lock (model.SyncRoot)
        {
            model.Clear();
        }
    }

    public async Task<R> RefreshAsync()
    {
        return await Parse();
    }


    async Task<R> Parse()
    {
        using var _ = Timing.Start();
        var path = "/workspaces/Dependinator/Dependinator/Dependinator.sln";

        if (!Try(out var reader, out var e, parserService.Parse(path))) return e;

        await Task.Run(async () =>
        {
            var batchItems = new List<Parsing.IItem>();
            while (await reader.WaitToReadAsync())
            {
                while (reader.TryRead(out var item))
                {
                    batchItems.Add(item);
                }
            }
            AddOrUpdate(batchItems);
        });

        return R.Ok;
    }


    void AddOrUpdate(IReadOnlyList<Parsing.IItem> parsedItems)
    {
        lock (model.SyncRoot)
        {
            AddSpecials();
            foreach (var parsedItem in parsedItems)
            {
                switch (parsedItem)
                {
                    case Parsing.Node parsedNode:
                        modelStructureService.AddOrUpdateNode(parsedNode);
                        break;

                    case Parsing.Link parsedLink:
                        modelStructureService.AddOrUpdateLink(parsedLink);
                        break;
                }
            }
        }
    }

    void AddSpecials()
    {
        for (int j = 1; j < 35; j++)
        {
            var name = $"TestJ";
            for (int i = 1; i < 20; i++)
            {
                var parentName = name;
                name = $"{name}.Test-{j}-{i}";
                var node = new Parsing.Node(name, parentName, Parsing.NodeType.Assembly, "");
                modelStructureService.AddOrUpdateNode(node);
            }
        }
    }
}
