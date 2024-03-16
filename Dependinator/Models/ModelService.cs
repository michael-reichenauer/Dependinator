using Dependinator.Shared;
using Dependinator.Utils.UI;

namespace Dependinator.Models;


interface IModelService
{
    string ModelName { get; }

    Task<R> LoadAsync(string path);
    (Rect, double) GetLatestView();
    Task<R> RefreshAsync();
    Tile GetTile(Rect viewRect, double zoom);
    bool TryGetNode(string id, out Node node);
    bool TryUpdateNode(string id, Action<Node> updateAction);
    void Clear();
    void TriggerSave();
    Rect GetBounds();
}


[Transient]
class ModelService : IModelService
{
    static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(0.5);
    static readonly TimeSpan MaxSaveDelay = TimeSpan.FromSeconds(10) - SaveDelay;

    readonly IModel model;
    readonly Parsing.IParserService parserService;
    readonly IStructureService modelStructureService;
    readonly ISvgService modelSvgService;
    readonly Parsing.IPersistenceService persistenceService;
    readonly IUIService uiService;
    readonly IConfigService configService;

    public ModelService(
        IModel model,
        Parsing.IParserService parserService,
        IStructureService modelStructureService,
        ISvgService modelSvgService,
        Parsing.IPersistenceService persistenceService,
        IUIService uiService,
        IConfigService configService)
    {
        this.model = model;
        this.parserService = parserService;
        this.modelStructureService = modelStructureService;
        this.modelSvgService = modelSvgService;
        this.persistenceService = persistenceService;
        this.uiService = uiService;
        this.configService = configService;
    }

    public string ModelName => ReadModel(m => Path.GetFileNameWithoutExtension(m.Path));


    T ReadModel<T>(Func<IModel, T> readFunc)
    {
        lock (model.Lock)
        {
            return readFunc(model);
        }
    }

    public Rect GetBounds()
    {
        lock (model.Lock)
        {
            return model.Root.GetTotalBounds();
        }
    }

    public bool TryGetNode(string id, out Node node)
    {
        lock (model.Lock)
        {
            return model.TryGetNode(NodeId.FromId(id), out node);
        }
    }

    public bool TryUpdateNode(string id, Action<Node> updateAction)
    {
        lock (model.Lock)
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
        //Log.Info("Get tile", zoom, viewRect.X, viewRect.Y);
        lock (model.Lock)
        {
            if (model.Root.Children.Any())
            {
                model.ViewRect = viewRect;
                model.Zoom = zoom;
            }
            return modelSvgService.GetTile(model, viewRect, zoom);
        }
    }

    public (Rect, double) GetLatestView()
    {
        lock (model.Lock)
        {
            return (model.ViewRect, model.Zoom);
        }
    }

    public void Clear()
    {
        lock (model.Lock)
        {
            model.Clear();
        }
    }

    public async Task<R> LoadAsync(string path)
    {
        Clear();
        if (path == "")
        {
            path = (await configService.GetAsync()).LastUsedPath;
            path = path == "" ? ExampleModel.Path : path;
        }

        await configService.SetAsync(c => c.LastUsedPath = path);
        using var _ = Timing.Start("Load model", path);

        // Try read cached model (with ui layout)
        if (!Try(out var model, out var e, await persistenceService.ReadAsync(path)))
        {
            return await ParseAsync(path, Rect.Zero, 0);
        }

        // Load the cached mode
        await Task.Run(() => Load(model));

        if (path == ExampleModel.Path) return R.Ok;

        // Trigger parse to get latest data
        ParseAsync(model.Path, model.ViewRect, model.Zoom).RunInBackground();
        return R.Ok;
    }

    public async Task<R> RefreshAsync()
    {
        var path = "";
        lock (model.Lock) path = model.Path;

        (Rect viewRect, double zoom) = GetLatestView();
        return await ParseAsync(path, viewRect, zoom);
    }


    async Task<R> ParseAsync(string path, Rect viewRect, double zoom)
    {
        using var _ = Timing.Start();
        //var path = "/workspaces/Dependinator/Dependinator.sln";


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
            AddOrUpdate(path, viewRect, zoom, batchItems);
        });

        uiService.TriggerUIStateChange();

        TriggerSave();

        return R.Ok;
    }


    public void TriggerSave()
    {
        CancellationToken ct;
        lock (model.Lock)
        {
            if (!model.IsSaving)
            {
                model.IsSaving = true;
                model.ModifiedTime = DateTime.Now;
                model.SaveCancelSource = new CancellationTokenSource();
                ct = model.SaveCancelSource.Token;
            }
            else
            {
                if (DateTime.Now - model.ModifiedTime > MaxSaveDelay) return; // Time to save (not postoning more)

                model.SaveCancelSource.Cancel(); // Pospone the save a bit more
                model.SaveCancelSource = new CancellationTokenSource();
                ct = model.SaveCancelSource.Token;
            }
        }

        Task.Delay(SaveDelay, ct).ContinueWith(t =>
        {
            if (!t.IsCanceled) Task.Run(() => Save(ct)).RunInBackground();
        });
    }

    void Save(CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        Parsing.Model modelData;
        lock (model.Lock)
        {
            modelData = persistenceService.ModelToData(model);
            model.IsSaving = false;
        }

        persistenceService.WriteAsync(modelData).RunInBackground();
    }

    private void Load(Parsing.Model modelData)
    {
        var batchItems = new List<Parsing.IItem>();
        modelData.Nodes.ForEach(batchItems.Add);
        modelData.Links.ForEach(batchItems.Add);

        AddOrUpdate(modelData.Path, modelData.ViewRect, modelData.Zoom, batchItems);
    }

    void AddOrUpdate(string path, Rect viewRect, double zoom, IReadOnlyList<Parsing.IItem> parsedItems)
    {
        using var _ = Timing.Start($"Add {parsedItems.Count} items for {path}");
        lock (model.Lock)
        {
            model.Path = path;
            model.ViewRect = viewRect;
            model.Zoom = zoom;
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
