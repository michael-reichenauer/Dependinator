using DependinatorCore;
using DependinatorCore.Shared;

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
    Task<R<Source>> GetSourceAsync(NodeId nodeId);
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
    Task LayoutNode(NodeId nodeId, bool recursively = false);
}

// Model service
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
    readonly IProgressService progressService;
    readonly ICommandService commandService;
    readonly IHost host;

    public ModelService(
        IModel model,
        Parsing.IParserService parserService,
        IStructureService modelStructureService,
        IPersistenceService persistenceService,
        IApplicationEvents applicationEvents,
        IProgressService progressService,
        ICommandService commandService,
        IHost host
    )
    {
        this.model = model;
        this.parserService = parserService;
        this.modelStructureService = modelStructureService;
        this.persistenceService = persistenceService;
        this.applicationEvents = applicationEvents;
        this.progressService = progressService;
        this.commandService = commandService;
        this.host = host;
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

        RefreshAsync().RunInBackground();
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
        using var progress = progressService.Start("Loading ...");
        if (!Try(out var model, out var e, await persistenceService.ReadAsync(path)))
            return e;

        var modelInfo = await LoadCachedModelDataAsync(path, model);
        CheckLineVisibility();

        return modelInfo;
    }

    public async Task<R> RefreshAsync()
    {
        var path = "";
        lock (model.Lock)
        {
            path = model.Path;
        }

        if (!Try(out var e, await ParseAndUpdateAsync(path, true)))
            return e;
        lock (model.Lock)
        {
            model.ClearNotUpdated();
        }

        TriggerSave();
        applicationEvents.TriggerUIStateChanged();
        return R.Ok;
    }

    public async Task<R<Source>> GetSourceAsync(NodeId nodeId)
    {
        string modelPath;
        string nodeName;

        lock (model.Lock)
        {
            modelPath = model.Path;
            if (string.IsNullOrEmpty(modelPath))
                return R.Error("Model is not loaded");

            if (!model.TryGetNode(nodeId, out var node))
                return R.Error($"Failed to locate node '{nodeId.Value}' in the current model");

            nodeName = node.Name;
        }

        if (
            !Try(
                out DependinatorCore.Parsing.Source? source,
                out var e,
                await parserService.GetSourceAsync(modelPath, nodeName)
            )
        )
            return e;

        var sourceText = source.Text.Replace("\t", "  "); // The auto formatter removes this in Blazor code.
        return new Source(sourceText, new FileLocation(source.Location.Path, source.Location.Line));
    }

    public async Task LayoutNode(NodeId nodeId, bool recursively = false)
    {
        using (var model = UseModel())
        {
            if (!model.TryGetNode(nodeId, out Node node))
                return;

            LayoutNode(node, recursively);
        }

        applicationEvents.TriggerUIStateChanged();
        applicationEvents.TriggerSaveNeeded();
    }

    void LayoutNode(Node node, bool recursively)
    {
        NodeLayout.AdjustChildren(node, forceAllChildren: true);
        if (recursively)
            node.Children.ForEach(n => LayoutNode(n, recursively));
    }

    async Task<R<ModelInfo>> ParseNewModelAsync(string path)
    {
        if (!Try(out var e, await ParseAndUpdateAsync(path)))
            return e;

        return new ModelInfo(path, Rect.None, 0);
    }

    async Task<R> ParseAndUpdateAsync(string path, bool isRefresh = false)
    {
        using var _ = Timing.Start($"Parsed and added model items {path}");
        using (var progress = isRefresh ? progressService.StartDiscreet() : progressService.Start("Parsing"))
        {
            // Let the renderer process the progress state before potentially CPU-heavy parse work starts.
            await Task.Yield();

            Log.Info("Parsing ...");

            if (!Try(out var items, out var e, await ParseAsync(path)))
                return e;

            lock (model.Lock)
            {
                model.Path = path;
                model.UpdateStamp = DateTime.UtcNow;
                model.ClearCachedSvg();
            }

            await AddOrUpdateAllItems(items);
        }

        CheckLineVisibility();

        // ParseSourceAndUpdateAsync(path).RunInBackground();
        return R.Ok;
    }

    async Task<R> ParseSourceAndUpdateAsync(string path)
    {
        if (!host.IsVscExtWasm && Build.IsWasm) // Parse source currently only supported when running as VS Code extension
            return R.Ok;

        using var __ = progressService.StartDiscreet();
        await Task.Yield();
        using var _ = Timing.Start($"Parsed source and added model items {path}");

        Log.Info("Parsing source ...");

        if (!Try(out var items, out var e, await ParseAsync(path)))
            return e;

        lock (model.Lock)
        {
            model.ClearCachedSvg();
        }

        await AddOrUpdateAllItems(items);

        applicationEvents.TriggerUIStateChanged();
        applicationEvents.TriggerSaveNeeded();

        return R.Ok;
    }

    async Task<R<IReadOnlyList<Parsing.Item>>> ParseAsync(string path)
    {
        using var _ = Timing.Start($"Parsed {path}");
        return await parserService.ParseAsync(path);
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

    async Task AddOrUpdateAllItems(IReadOnlyList<Parsing.Item> items)
    {
        using var _ = Timing.Start($"Added or updated {items.Count} items");
        await Task.Run(async () =>
        {
            foreach (var batch in items.Chunk(100))
            {
                await Task.Yield();
                AddOrUpdateItems(batch);
            }
        });
    }

    void AddOrUpdateItems(IReadOnlyList<Parsing.Item> parsedItems)
    {
        lock (model.Lock)
        {
            foreach (var parsedItem in parsedItems)
            {
                if (parsedItem.Node is not null)
                    modelStructureService.AddOrUpdateNode(parsedItem.Node);
                if (parsedItem.Link is not null)
                    modelStructureService.AddOrUpdateLink(parsedItem.Link);
            }
        }
    }

    async Task UpdateAllItems(IReadOnlyList<Parsing.Item> items)
    {
        using var _ = Timing.Start($"Updated {items.Count} items");
        await Task.Run(async () =>
        {
            foreach (var batch in items.Chunk(100))
            {
                await Task.Yield();
                UpdateItems(batch);
            }
        });
    }

    void UpdateItems(IReadOnlyList<Parsing.Item> parsedItems)
    {
        lock (model.Lock)
        {
            foreach (var parsedItem in parsedItems)
            {
                if (parsedItem.Node is not null)
                    modelStructureService.TryUpdateNode(parsedItem.Node);
            }
        }
    }

    void SetNodeAndLinkDtos(ModelDto modelDto)
    {
        lock (model.Lock)
        {
            modelDto.Nodes.ForEach(modelStructureService.SetNodeDto);
            modelDto.Links.ForEach(modelStructureService.SetLinkDto);
            modelDto.Lines.ForEach(modelStructureService.SetLineLayoutDto);
        }
    }
}
