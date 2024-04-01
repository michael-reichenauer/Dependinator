namespace Dependinator.Models;


record ModelInfo(string Path, Rect ViewRect, double Zoom);

interface IModelService
{
    string ModelName { get; }
    Pos Offset { get; }
    double Zoom { get; }

    void Do(Command command);
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
        ICommandService commandService)
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


    public void Do(Command command)
    {
        using (var model = UseModel())
        {
            commandService.Do(model, command);
            model.ClearCachedSvg();
        }

        applicationEvents.TriggerUIStateChanged();
    }


    public void Undo()
    {
        if (!commandService.CanUndo) return;
        using (var model = UseModel())
        {
            commandService.Undo(model);
            model.ClearCachedSvg();
        }

        applicationEvents.TriggerUnselected();
        applicationEvents.TriggerUIStateChanged();
    }


    public void Redo()
    {
        if (!commandService.CanRedo) return;
        using (var model = UseModel())
        {
            commandService.Redo(model);
            model.ClearCachedSvg();
        }

        applicationEvents.TriggerUIStateChanged();
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

    public bool UseNode(string id, Action<Node> updateAction)
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

    public async Task<R<ModelInfo>> LoadAsync(string path)
    {
        Clear();

        using var _ = Timing.Start("Load model", path);

        // Try read cached model (with ui layout)
        if (!Try(out var model, out var e, await persistenceService.ReadAsync(path)))
        {
            return await ParseAsync(path, Rect.None, 0, Pos.None);
        }

        // Load the cached mode
        var modelInfo = await Task.Run(() => Load(model));

        if (path == ExampleModel.Path) return modelInfo;

        // Trigger parse to get latest data
        ParseAsync(model.Path, model.ViewRect, model.Zoom, model.Offset)
            .ContinueWith(t => applicationEvents.TriggerUIStateChanged())
            .RunInBackground();

        return modelInfo;
    }



    public async Task<R> RefreshAsync()
    {
        var path = "";
        var offset = Pos.None;
        lock (model.Lock) path = model.Path;
        lock (model.Lock) offset = model.Offset;

        (Rect viewRect, double zoom) = GetLatestView();
        return await ParseAsync(path, viewRect, zoom, offset);
    }


    async Task<R<ModelInfo>> ParseAsync(string path, Rect viewRect, double zoom, Pos offset)
    {
        using var _ = Timing.Start();
        //var path = "/workspaces/Dependinator/Dependinator.sln";

        if (!Try(out var reader, out var e, parserService.Parse(path))) return e;

        var modelInfo = await Task.Run(async () =>
        {
            var batchItems = new List<Parsing.IItem>();
            while (await reader.WaitToReadAsync())
            {
                while (reader.TryRead(out var item))
                {
                    batchItems.Add(item);
                }
            }
            return AddOrUpdate(path, viewRect, zoom, offset, batchItems);
        });

        return modelInfo;
    }


    public void TriggerSave()
    {
        CancellationToken ct;
        lock (model.Lock)
        {
            if (model.Items.Count == 1) return;

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
        if (model.Path == "") return;

        persistenceService.WriteAsync(modelData).RunInBackground();
    }

    private ModelInfo Load(Parsing.Model modelData)
    {
        var batchItems = new List<Parsing.IItem>();
        modelData.Nodes.ForEach(batchItems.Add);
        modelData.Links.ForEach(batchItems.Add);

        return AddOrUpdate(modelData.Path, modelData.ViewRect, modelData.Zoom, model.Offset, batchItems);
    }

    ModelInfo AddOrUpdate(string path, Rect viewRect, double zoom, Pos offset, IReadOnlyList<Parsing.IItem> parsedItems)
    {
        using var _ = Timing.Start($"Add {parsedItems.Count} items for {path}");
        lock (model.Lock)
        {
            model.Path = path;
            model.ViewRect = viewRect;
            model.Zoom = zoom;
            model.Offset = offset;
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
            return new ModelInfo(path, viewRect, zoom);
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
