namespace Dependinator.Models;

record ModelInfo(string Path, Rect ViewRect, double Zoom);

interface IModelService
{
    string ModelName { get; }
    Pos Offset { get; }
    double Zoom { get; }

    void Do(Command command, bool isClearCache = true);
    bool CanUndo { get; }
    bool CanRedo { get; }
    void Undo();
    void Redo();

    IModel UseModel();

    Task<R<ModelInfo>> LoadAsync(string path);
    (Rect, double) GetLatestView();
    Task<R> RefreshAsync();
    Tile GetTile(Rect viewRect, double zoom);
    bool TryGetNode(string id, out Node node);
    bool UseNode(string id, Action<Node> useAction);
    bool UseNodeN(NodeId id, Action<Node> updateAction);
    void Clear();
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
    readonly IApplicationEvents applicationEvents;
    readonly ICommandService commandService;

    public ModelService(
        IModel model,
        Parsing.IParserService parserService,
        IStructureService modelStructureService,
        ISvgService modelSvgService,
        Parsing.IPersistenceService persistenceService,
        IApplicationEvents applicationEvents,
        ICommandService commandService
    )
    {
        this.model = model;
        this.parserService = parserService;
        this.modelStructureService = modelStructureService;
        this.modelSvgService = modelSvgService;
        this.persistenceService = persistenceService;
        this.applicationEvents = applicationEvents;
        this.commandService = commandService;
        this.applicationEvents.SaveNeeded += TriggerSave;
    }

    public bool CanUndo => commandService.CanUndo;
    public bool CanRedo => commandService.CanRedo;
    public Pos Offset => Use(m => m.Offset);
    public double Zoom => Use(m => m.Zoom);
    public string ModelName => Use(m => Path.GetFileNameWithoutExtension(m.Path));

    public void Do(Command command, bool isClearCache = true)
    {
        using (var model = UseModel())
        {
            commandService.Do(model, command);
            if (isClearCache)
                model.ClearCachedSvg();
        }

        applicationEvents.TriggerUIStateChanged();
        TriggerSave();
    }

    public void Undo()
    {
        commandService.Undo(UseModel);
        TriggerSave();
    }

    public void Redo()
    {
        commandService.Redo(UseModel);
        TriggerSave();
    }

    public IModel UseModel()
    {
        Monitor.Enter(model.Lock);
        return model;
    }

    T Use<T>(Func<IModel, T> readFunc)
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

    public bool TryNode(string id, out Node node)
    {
        lock (model.Lock)
        {
            return model.TryGetNode(NodeId.FromId(id), out node);
        }
    }

    public bool TryGetNode(string id, out Node node)
    {
        lock (model.Lock)
        {
            return model.TryGetNode(NodeId.FromId(id), out node);
        }
    }

    public bool UseNodeN(NodeId id, Action<Node> updateAction)
    {
        lock (model.Lock)
        {
            if (!model.TryGetNode(id, out var node))
                return false;

            updateAction(node);
            model.ClearCachedSvg();
        }

        TriggerSave();
        return true;
    }

    public bool UseNode(string id, Action<Node> updateAction) => UseNodeN(NodeId.FromId(id), updateAction);

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

    public async Task<R<ModelInfo>> LoadAsync(string path)
    {
        Clear();
        if (!Build.IsWebAssembly && path == ExampleModel.Path)
        {
            path = "/workspaces/Dependinator/Dependinator.sln";
        }

        Log.Info("Loading", path);
        using var _ = Timing.Start("Load model", path);

        // Try read cached model (with ui layout)
        if (!Try(out var modelInfo, out var e, await ReadCachedModelAsync(path)))
        {
            Log.Info("Failed to read cached model", e.ErrorMessage);
            var parsedModelInfo = await ParseNewModelAsync(path);
            TriggerSave();
            applicationEvents.TriggerUIStateChanged();
            return parsedModelInfo;
        }
        return modelInfo;
    }

    async Task<R<ModelInfo>> ReadCachedModelAsync(string path)
    {
        if (!Try(out var model, out var e, await persistenceService.ReadAsync(path)))
            return e;

        var modelInfo = await LoadCachedModelDataAsync(model);

        // if (path != ExampleModel.Path) Util.Trigger(RefreshAsync);

        return modelInfo;
    }

    public async Task<R> RefreshAsync()
    {
        var path = "";
        lock (model.Lock)
        {
            path = model.Path;
        }

        if (!Try(out var e, await ParseAsync(path)))
            return e;

        TriggerSave();
        applicationEvents.TriggerUIStateChanged();
        return R.Ok;
    }

    async Task<R<ModelInfo>> ParseNewModelAsync(string path)
    {
        if (!Try(out var e, await ParseAsync(path)))
            return e;

        lock (model.Lock)
        {
            model.Path = path;
        }

        return new ModelInfo(path, Rect.None, 0);
    }

    async Task<R> ParseAsync(string path)
    {
        using var _ = Timing.Start($"Parsed and added model items {path}");
        //var path = "/workspaces/Dependinator/Dependinator.sln";

        if (!Try(out var reader, out var e, parserService.Parse(path)))
            return e;

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
            AddOrUpdateItems(batchItems);
        });

        return R.Ok;
    }

    public void TriggerSave()
    {
        CancellationToken ct;
        lock (model.Lock)
        {
            if (model.Items.Count == 1)
                return;

            if (!model.IsSaving)
            {
                model.IsSaving = true;
                model.ModifiedTime = DateTime.Now;
                model.SaveCancelSource = new CancellationTokenSource();
                ct = model.SaveCancelSource.Token;
            }
            else
            {
                if (DateTime.Now - model.ModifiedTime > MaxSaveDelay)
                    return; // Time to save (not postponing more)

                model.SaveCancelSource.Cancel(); // Postpone the save a bit more
                model.SaveCancelSource = new CancellationTokenSource();
                ct = model.SaveCancelSource.Token;
            }
        }

        Task.Delay(SaveDelay, ct)
            .ContinueWith(t =>
            {
                if (!t.IsCanceled)
                    Task.Run(() => Save(ct)).RunInBackground();
            });
    }

    void Save(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return;

        Parsing.Model modelData;
        lock (model.Lock)
        {
            modelData = persistenceService.ModelToData(model);
            model.IsSaving = false;
        }
        if (model.Path == "")
            return;

        persistenceService.WriteAsync(modelData).RunInBackground();
    }

    async Task<ModelInfo> LoadCachedModelDataAsync(Parsing.Model modelData)
    {
        var batchItems = new List<Parsing.IItem>();
        modelData.Nodes.ForEach(batchItems.Add);
        modelData.Links.ForEach(batchItems.Add);

        lock (model.Lock)
        {
            model.Path = modelData.Path;
            model.ViewRect = modelData.ViewRect;
            model.Zoom = modelData.Zoom;
            model.Offset = modelData.Offset;
        }

        await Task.Run(() => AddOrUpdateItems(batchItems));
        return new ModelInfo(modelData.Path, modelData.ViewRect, modelData.Zoom);
    }

    void AddOrUpdateItems(IReadOnlyList<Parsing.IItem> parsedItems)
    {
        using var _ = Timing.Start($"Added {parsedItems.Count} items");
        lock (model.Lock)
        {
            model.ClearCachedSvg();

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
