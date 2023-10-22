using System.Diagnostics;
using System.Threading.Channels;


namespace Dependinator.Model;


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

                var updatedItems = modelDb.AddOrUpdate(batchItems);
                foreach (var item in updatedItems) await channel.Writer.WriteAsync(item);
            }

            channel.Writer.Complete();
        });

        return channel.Reader;
    }
}