using System.Diagnostics;


namespace Dependinator.Models;


interface IModelService
{
    Task<R> RefreshAsync();
    LevelSvg GetSvg(Rect viewRect, double zoom);
    bool TryGetNode(string id, out Node node);
    void Clear();
}


[Transient]
class ModelService : IModelService
{
    const int BatchTimeMs = 300;

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

    public LevelSvg GetSvg(Rect viewRect, double zoom)
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
        var path = "/workspaces/Dependinator/Dependinator/Dependinator.sln";

        if (!Try(out var reader, out var e, parserService.Parse(path))) return e;

        await Task.Run(async () =>
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

                AddOrUpdate(batchItems);
            }
        });

        return R.Ok;
    }


    void AddOrUpdate(IReadOnlyList<Parsing.IItem> parsedItems)
    {
        lock (model.SyncRoot)
        {
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
}