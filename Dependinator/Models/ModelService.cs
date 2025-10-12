using System.Reflection;

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
    bool TryGetNode(string id, out Node node);
    bool UseNode(string id, Action<Node> useAction);
    bool UseNodeN(NodeId id, Action<Node> updateAction);
    bool UseNode(string id, Func<Node, bool> useAction);
    bool UseNodeN(NodeId id, Func<Node, bool> updateAction);
    bool UseLine(string id, Func<Line, bool> useAction);
    bool UseLineN(LineId id, Func<Line, bool> updateAction);
    bool UseLine(string id, Action<Line> useAction);
    bool UseLineN(LineId id, Action<Line> updateAction);
    void Clear();
    void ClearCache();
    Rect GetBounds();
    void CheckLineVisibility();
}

[Transient]
class ModelService : IModelService
{
    static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(0.5);
    static readonly TimeSpan MaxSaveDelay = TimeSpan.FromSeconds(10) - SaveDelay;
    readonly IModel model;
    readonly Parsing.IParserService parserService;
    readonly IStructureService modelStructureService;
    readonly IPersistenceService persistenceService;
    readonly IApplicationEvents applicationEvents;
    readonly ICommandService commandService;

    public ModelService(
        IModel model,
        Parsing.IParserService parserService,
        IStructureService modelStructureService,
        IPersistenceService persistenceService,
        IApplicationEvents applicationEvents,
        ICommandService commandService
    )
    {
        this.model = model;
        this.parserService = parserService;
        this.modelStructureService = modelStructureService;
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

    public void ClearCache()
    {
        lock (model.Lock)
        {
            model.ClearCachedSvg();
        }
        TriggerSave();
    }

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

    public bool UseNodeN(NodeId id, Func<Node, bool> updateAction)
    {
        lock (model.Lock)
        {
            if (!model.TryGetNode(id, out var node))
                return false;

            if (!updateAction(node))
                return false;
            model.ClearCachedSvg();
        }

        TriggerSave();
        return true;
    }

    public bool UseLineN(LineId id, Func<Line, bool> updateAction)
    {
        lock (model.Lock)
        {
            if (!model.TryGetLine(id, out var line))
                return false;

            if (!updateAction(line))
                return false;
            model.ClearCachedSvg();
        }

        TriggerSave();
        return true;
    }

    public bool UseLineN(LineId id, Action<Line> updateAction)
    {
        lock (model.Lock)
        {
            if (!model.TryGetLine(id, out var line))
                return false;

            updateAction(line);
            model.ClearCachedSvg();
        }

        TriggerSave();
        return true;
    }

    public bool UseNode(string id, Action<Node> updateAction) => UseNodeN(NodeId.FromId(id), updateAction);

    public bool UseNode(string id, Func<Node, bool> updateAction) => UseNodeN(NodeId.FromId(id), updateAction);

    public bool UseLine(string id, Action<Line> updateAction) => UseLineN(LineId.FromId(id), updateAction);

    public bool UseLine(string id, Func<Line, bool> updateAction) => UseLineN(LineId.FromId(id), updateAction);

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

        Log.Info("Loading ...", path);
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

    public void CheckLineVisibility()
    {
        lock (model.Lock)
        {
            foreach (var line in model.Items.Values.OfType<Line>())
            {
                if (line.IsDirect)
                {
                    line.IsHidden = false;
                    continue;
                }

                line.IsHidden = line.Links.All(link => link.Source.IsHidden || link.Target.IsHidden);
            }
        }
    }

    async Task<R<ModelInfo>> ReadCachedModelAsync(string path)
    {
        if (!Try(out var model, out var e, await persistenceService.ReadAsync(path)))
            return e;

        var modelInfo = await LoadCachedModelDataAsync(path, model);
        CheckLineVisibility();

        return modelInfo;
    }

    public async Task<R> RefreshAsync()
    {
        if (Build.IsWebAssembly) // Not yet supported
            return R.Ok;

        var path = "";
        lock (model.Lock)
        {
            path = model.Path;
        }

        if (!Try(out var e, await ParseAsync(path)))
            return e;
        lock (model.Lock)
        {
            model.ClearNotUpdated();
        }

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

        Log.Info("Adding...");
        lock (model.Lock)
        {
            model.UpdateStamp = DateTime.UtcNow;
            model.ClearCachedSvg();
        }

        await AddOrUpdateAllItems(reader);

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

        ModelDto modelData;
        string modelPath = model.Path;
        lock (model.Lock)
        {
            modelPath = model.Path;
            modelData = model.ToDto();
            model.IsSaving = false;
        }
        if (model.Path == "")
            return;

        persistenceService.WriteAsync(modelPath, modelData).RunInBackground();
    }

    async Task<ModelInfo> LoadCachedModelDataAsync(string path, ModelDto modelDto)
    {
        lock (model.Lock)
        {
            model.SetFromDto(path, modelDto);
        }

        await Task.Run(() => SetNodeAndLinkDtos(modelDto));
        return new ModelInfo(path, modelDto.ViewRect, modelDto.Zoom);
    }

    private async Task AddOrUpdateAllItems(System.Threading.Channels.ChannelReader<Parsing.IItem> reader)
    {
        var itemsCount = 0;
        var t = Timing.Start();
        await Task.Run(async () =>
        {
            while (await reader.WaitToReadAsync())
            {
                var batchItems = new List<Parsing.IItem>();
                while (reader.TryRead(out var item))
                {
                    batchItems.Add(item);
                    itemsCount++;
                    if (batchItems.Count >= 500)
                        break;
                }
                AddOrUpdateItems(batchItems);
            }
        });
        t.Log($"Added or updated {itemsCount} items");
    }

    void AddOrUpdateItems(IReadOnlyList<Parsing.IItem> parsedItems)
    {
        lock (model.Lock)
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

    void SetNodeAndLinkDtos(ModelDto modelDto)
    {
        lock (model.Lock)
        {
            modelDto.Nodes.ForEach(modelStructureService.SetNodeDto);
            modelDto.Links.ForEach(modelStructureService.SetLinkDto);
        }
    }
}
