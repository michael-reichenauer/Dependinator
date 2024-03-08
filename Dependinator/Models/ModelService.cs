using Dependinator.Utils.UI;

namespace Dependinator.Models;


interface IModelService
{
    Task<R> LoadAsync();
    Task<R> RefreshAsync();
    Tile GetTile(Rect viewRect, double zoom);
    bool TryGetNode(string id, out Node node);
    bool TryUpdateNode(string id, Action<Node> updateAction);
    void Clear();
}


[Transient]
class ModelService : IModelService
{
    readonly IModel model;
    readonly Parsing.IParserService parserService;
    readonly IStructureService modelStructureService;
    readonly ISvgService modelSvgService;
    readonly Parsing.IPersistenceService persistenceService;
    readonly IUIService uiService;

    public ModelService(
        IModel model,
        Parsing.IParserService parserService,
        IStructureService modelStructureService,
        ISvgService modelSvgService,
        Parsing.IPersistenceService persistenceService,
        IUIService uiService)
    {
        this.model = model;
        this.parserService = parserService;
        this.modelStructureService = modelStructureService;
        this.modelSvgService = modelSvgService;
        this.persistenceService = persistenceService;
        this.uiService = uiService;
    }

    public bool TryGetNode(string id, out Node node)
    {
        lock (model.SyncRoot)
        {
            return model.TryGetNode(NodeId.FromId(id), out node);
        }
    }

    public bool TryUpdateNode(string id, Action<Node> updateAction)
    {
        lock (model.SyncRoot)
        {
            if (!model.TryGetNode(NodeId.FromId(id), out var node)) return false;

            updateAction(node);
            model.ClearCachedSvg();
        }

        TriggerSave();
        return true;
    }

    public Tile GetTile(Rect viewRect, double zoom)
    {
        lock (model.SyncRoot)
        {
            return modelSvgService.GetTile(model, viewRect, zoom);
        }
    }


    public void Clear()
    {
        lock (model.SyncRoot)
        {
            model.Clear();
        }
    }

    public async Task<R> LoadAsync()
    {
        var path = "Example.exe";

        // Try read cached model (with ui layout)
        if (!Try(out var model, out var e, await persistenceService.LoadAsync("")))
        {
            if (path == "Example.exe")
            {
                if (!Try(out model, out e, await persistenceService.LoadAsync(path))) return e;
            }
            else
            {
                return await ParseAsync();
            }
        }

        // Load the cached mode
        await Task.Run(() => Load(model));

        // Trigger parse to get latest data
        ParseAsync().RunInBackground();
        return R.Ok;
    }

    public async Task<R> RefreshAsync()
    {
        return await ParseAsync();
    }


    async Task<R> ParseAsync()
    {
        using var _ = Timing.Start();
        //var path = "/workspaces/Dependinator/Dependinator.sln";


        // if (!Try(out var reader, out var e, parserService.Parse(path))) return e;

        await Task.Run(async () =>
        {
            await Task.CompletedTask;

            var batchItems = new List<Parsing.IItem>();
            // while (await reader.WaitToReadAsync())
            // {
            //     while (reader.TryRead(out var item))
            //     {
            //         batchItems.Add(item);
            //     }
            // }
            AddOrUpdate(batchItems);
        });

        uiService.TriggerUIStateChange();

        TriggerSave();

        return R.Ok;
    }

    void TriggerSave()
    {
        lock (model.SyncRoot)
        {
            if (model.IsSaving) return;
            model.IsSaving = true;
        }

        Task.Delay(TimeSpan.FromSeconds(60)).ContinueWith(_ =>
            Task.Run(() => Save()).RunInBackground());
    }

    void Save()
    {
        Parsing.Model modelData;
        lock (model.SyncRoot)
        {
            modelData = persistenceService.ModelToData(model);
            model.IsSaving = false;
        }

        persistenceService.SaveAsync(modelData).RunInBackground();
    }

    private void Load(Parsing.Model modelData)
    {
        Log.Info("Loading model ...", modelData.Nodes.Count, modelData.Links.Count);

        var batchItems = new List<Parsing.IItem>();
        modelData.Nodes.ForEach(batchItems.Add);
        modelData.Links.ForEach(batchItems.Add);

        AddOrUpdate(batchItems);
    }

    void AddOrUpdate(IReadOnlyList<Parsing.IItem> parsedItems)
    {
        using var _ = Timing.Start();
        lock (model.SyncRoot)
        {
            // AddSpecials();
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

            model.ClearCachedSvg();
        }

        Log.Info($"Added or updated {parsedItems.Count} items");
    }

    void AddSpecials()
    {
        for (int j = 1; j < 2; j++)
        {
            var name = $"Test-1-1";
            for (int i = 1; i < 20; i++)
            {
                var parentName = name;
                name = $"{name}.Test-{j}-{i + 1}";
                var node = new Parsing.Node(name, parentName, Parsing.NodeType.Assembly, "");
                modelStructureService.AddOrUpdateNode(node);
            }
        }
    }
}
