using System.Diagnostics;


namespace Dependinator.Models;



interface IModelService
{
    Task<R> RefreshAsync();
    (Svgs, Rect) GetSvg();
    R<Node> FindNode(Pos offset, Pos point, double zoom);
    void Clear();

}


[Scoped]
class ModelService : IModelService
{
    const int BatchTimeMs = 300;
    readonly Parsing.IParserService parserService;
    readonly IModelProvider modelDb;


    public ModelService(Parsing.IParserService parserService, IModelProvider modelDb)
    {
        this.parserService = parserService;
        this.modelDb = modelDb;
    }

    public (Svgs, Rect) GetSvg()
    {
        using var model = modelDb.GetModel();

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

    public R<Node> FindNode(Pos offset, Pos point, double zoom)
    {
        using var model = modelDb.GetModel();

        // transform point to canvas coordinates
        var canvasPoint = new Pos(
            point.X * zoom + offset.X,
            point.Y * zoom + offset.Y);

        return model.Root.FindNode(Pos.Zero, canvasPoint, zoom);
    }

    public void Clear()
    {
        using var model = modelDb.GetModel();

        model.Clear();
    }
    public async Task<R> RefreshAsync()
    {
        var path = "/workspaces/Dependinator/Dependinator/Dependinator.sln";

        if (!Try(out var reader, out var e, parserService.Parse(path))) return e;

        await Task.Run(async () =>
        {
            using var _ = Timing.Start();
            // AddSpecials();

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
        using var model = modelDb.GetModel();

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





    // public void AddSpecials()
    // {
    //     using var model = modelDb.GetModel();

    //     for (int j = 1; j < 20; j++)
    //     {
    //         var name = $"TestJ";
    //         for (int i = 1; i < 15; i++)
    //         {
    //             var parentName = name;
    //             name = $"{name}.Test-{j}-{i}";
    //             var node = new Parsing.Node(name, parentName, Parsing.NodeType.Assembly, "");
    //             model.AddOrUpdateNode(node);
    //         }
    //     }
    // }
}