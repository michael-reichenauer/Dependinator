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
    readonly IModelDb modelDb;


    public ModelService(Parsing.IParserService parserService, IModelDb modelDb)
    {
        this.parserService = parserService;
        this.modelDb = modelDb;
    }

    public (Svgs, Rect) GetSvg()
    {
        using var model = modelDb.GetModel();
        return model.GetSvg();
    }

    public R<Node> FindNode(Pos offset, Pos point, double zoom)
    {
        using var model = modelDb.GetModel();
        return model.FindNode(offset, point, zoom);
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

    public void AddOrUpdate(IReadOnlyList<Parsing.IItem> parsedItems)
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

    public void AddSpecials()
    {
        using var model = modelDb.GetModel();

        for (int j = 1; j < 20; j++)
        {
            var name = $"TestJ";
            for (int i = 1; i < 15; i++)
            {
                var parentName = name;
                name = $"{name}.Test-{j}-{i}";
                var node = new Parsing.Node(name, parentName, Parsing.NodeType.Assembly, "");
                model.AddOrUpdateNode(node);
            }
        }
    }
}