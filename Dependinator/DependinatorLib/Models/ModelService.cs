using System.Diagnostics;


namespace Dependinator.Models;


interface IModelService
{
    Task<R> RefreshAsync();
    (Svgs, Rect) GetSvg();
    bool TryGetNode(string id, out Node node);
    void Clear();
}


[Singleton]
class ModelService : IModelService
{
    const int BatchTimeMs = 300;
    readonly Parsing.IParserService parserService;
    readonly IModel model = new Model();


    public ModelService(Parsing.IParserService parserService)
    {
        this.parserService = parserService;
    }

    public bool TryGetNode(string id, out Node node)
    {
        lock (model)
        {
            return model.TryGetNode(NodeId.FromId(id), out node);
        }
    }

    public (Svgs, Rect) GetSvg()
    {
        lock (model)
        {
            using var t = Timing.Start();

            var svgs = new List<Level>();

            for (int i = 0; i < 100; i++)
            {
                var zoom = i == 0 ? 1.0 : Math.Pow(2, i);
                var svg = model.Root.GetSvg(Pos.Zero, zoom);
                if (svg == "") break;
                svgs.Add(new Level(svg, 1 / zoom));
                // Log.Info($"Level: #{i} zoom: {zoom} svg: {svg.Length} chars");
            }
            Log.Info($"Levels: {svgs.Count}");

            var totalBoundary = model.Root.TotalBoundary;
            return (new Svgs(svgs), totalBoundary);
        }
    }


    public void Clear()
    {
        lock (model)
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
        lock (model)
        {
            foreach (var parsedItem in parsedItems)
            {
                switch (parsedItem)
                {
                    case Parsing.Node parsedNode:
                        model.AddOrUpdateNode(parsedNode);
                        break;

                    case Parsing.Link parsedLink:
                        model.AddOrUpdateLink(parsedLink);
                        break;
                }
            }
        }
    }
}