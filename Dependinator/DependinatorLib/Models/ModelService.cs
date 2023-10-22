using System.Diagnostics;
using System.Threading.Channels;


namespace Dependinator.Models;


interface IItem { }

record Source(string Path, string Text, int LineNumber);



interface IModelService
{
    Task<R> RefreshAsync();
}



[Scoped]
class ModelService : IModelService
{
    const int BatchTimeMs = 300;
    readonly Parsing.IParserService parserService;
    private readonly IModelDb modelDb;


    public ModelService(Parsing.IParserService parserService, IModelDb modelDb)
    {
        this.parserService = parserService;
        this.modelDb = modelDb;
    }


    public async Task<R> RefreshAsync()
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

    public void AddOrUpdate(IReadOnlyList<Parsing.IItem> parsedItems)
    {
        using var context = modelDb.GetModel();

        foreach (var parsedItem in parsedItems)
        {
            switch (parsedItem)
            {
                case Parsing.Node parsedNode:
                    context.Model.AddOrUpdateNode(parsedNode);
                    //Log.Info($"Node: {parsedNode}");
                    break;

                case Parsing.Link parsedLink:
                    context.Model.AddOrUpdateLink(parsedLink);
                    // Log.Info($"Link: {parsedLink}");
                    break;
            }
        }
    }
}