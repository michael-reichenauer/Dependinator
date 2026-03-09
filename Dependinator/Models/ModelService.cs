using Dependinator.Diagrams.Tiles;
using Dependinator.Shared.Types;

namespace Dependinator.Models;

record ModelInfo(string Path, Rect ViewRect, double Zoom);

interface IModelService
{
    Task<R<ModelInfo>> LoadAsync(string path);
    Task<R> RefreshAsync();
    void Clear();
    void ClearCache();
    void CheckLineVisibility();
    Task LayoutNode(NodeId nodeId, bool recursively = false);
    R<ModelDto> GetCurrentModelDto();
    Task<R> WriteModelAsync(string modelPath, ModelDto modelDto);
    Task<R<ModelInfo>> ReplaceCurrentModelAsync(ModelDto modelDto);
}

[Transient]
class ModelService : IModelService, IDisposable
{
    static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(0.5);
    static readonly TimeSpan MaxSaveDelay = TimeSpan.FromSeconds(10);

    readonly ITilesMgr tilesMgr;
    readonly IModelMgr modelMgr;
    readonly Parsing.IParserService parserService;
    readonly IStructureService modelStructureService;
    readonly IPersistenceService persistenceService;
    readonly IApplicationEvents applicationEvents;
    readonly IProgressService progressService;

    readonly Debouncer saveDebouncer = new();

    public ModelService(
        ITilesMgr tilesMgr,
        IModelMgr modelMgr,
        Parsing.IParserService parserService,
        IStructureService modelStructureService,
        IPersistenceService persistenceService,
        IApplicationEvents applicationEvents,
        IProgressService progressService
    )
    {
        this.tilesMgr = tilesMgr;
        this.modelMgr = modelMgr;
        this.parserService = parserService;
        this.modelStructureService = modelStructureService;
        this.persistenceService = persistenceService;
        this.applicationEvents = applicationEvents;
        this.progressService = progressService;
        this.applicationEvents.SaveNeeded += TriggerSave;
    }

    public void Dispose()
    {
        applicationEvents.SaveNeeded -= TriggerSave;
        saveDebouncer.Dispose();
    }

    public void ClearCache()
    {
        tilesMgr.ClearCache();
        TriggerSave();
    }

    public bool TryNode(string id, out Node node)
    {
        using var model = modelMgr.UseModel();
        return model.Nodes.TryGetValue(NodeId.FromId(id), out node!);
    }

    public void Clear()
    {
        using (var model = modelMgr.UseModel())
        {
            model.Clear();
        }
        tilesMgr.ClearCache();
    }

    public R<ModelDto> GetCurrentModelDto()
    {
        using (var model = modelMgr.UseModel())
        {
            if (string.IsNullOrWhiteSpace(model.Path))
                return R.Error("Model is not loaded");

            return model.SerializeToDto();
        }
    }

    public async Task<R<ModelInfo>> ReplaceCurrentModelAsync(ModelDto modelDto)
    {
        string modelPath;
        using (var model = modelMgr.UseModel())
        {
            modelPath = model.Path;
        }

        if (string.IsNullOrWhiteSpace(modelPath))
            return R.Error("Model is not loaded");

        if (!Try(out var error, await WriteModelAsync(modelPath, modelDto)))
            return error;

        var modelInfo = await LoadCachedModelDataAsync(modelPath, modelDto);
        CheckLineVisibility();
        return modelInfo;
    }

    public Task<R> WriteModelAsync(string modelPath, ModelDto modelDto)
    {
        return persistenceService.WriteAsync(modelPath, modelDto);
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
            tilesMgr.ClearCache();
            applicationEvents.TriggerUIStateChanged();
            return parsedModelInfo;
        }

        RefreshAsync().RunInBackground();
        return modelInfo;
    }

    public void CheckLineVisibility()
    {
        using var model = modelMgr.UseModel();
        foreach (var line in model.Lines.Values)
        {
            if (line.IsDirect)
            {
                line.IsHidden = false;
                continue;
            }

            line.IsHidden = line.Links.All(link => link.Source.IsHidden || link.Target.IsHidden);
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
        using (var model = modelMgr.UseModel())
        {
            path = model.Path;
        }

        if (!Try(out var e, await ParseAndUpdateAsync(path, true)))
            return e;
        using (var model = modelMgr.UseModel())
        {
            modelStructureService.ClearNotUpdated(model);
        }
        tilesMgr.ClearCache();

        TriggerSave();
        applicationEvents.TriggerUIStateChanged();
        return R.Ok;
    }

    public async Task LayoutNode(NodeId nodeId, bool recursively = false)
    {
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out Node? node))
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

            using (var model = modelMgr.UseModel())
            {
                model.Path = path;
                model.UpdateStamp = DateTime.UtcNow;
            }

            tilesMgr.ClearCache();

            await AddOrUpdateAllItems(items);
        }

        CheckLineVisibility();

        return R.Ok;
    }

    async Task<R<IReadOnlyList<Parsing.Item>>> ParseAsync(string path)
    {
        using var _ = Timing.Start($"Parsed {path}");
        return await parserService.ParseAsync(path);
    }

    public void TriggerSave()
    {
        saveDebouncer.Debounce(SaveDelay, MaxSaveDelay, Save);
    }

    void Save()
    {
        ModelDto modelData;
        string modelPath;
        using (var model = modelMgr.UseModel())
        {
            modelPath = model.Path;
            modelData = model.SerializeToDto();
        }

        if (modelPath == "")
            return;

        persistenceService.WriteAsync(modelPath, modelData).RunInBackground();
    }

    async Task<ModelInfo> LoadCachedModelDataAsync(string path, ModelDto modelDto)
    {
        using (var model = modelMgr.UseModel())
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
        using (var model = modelMgr.UseModel())
        {
            foreach (var parsedItem in parsedItems)
            {
                if (parsedItem.Node is not null)
                    modelStructureService.AddOrUpdateNode(model, parsedItem.Node);
                if (parsedItem.Link is not null)
                    modelStructureService.AddOrUpdateLink(model, parsedItem.Link);
            }
        }
    }

    void SetNodeAndLinkDtos(ModelDto modelDto)
    {
        using (var model = modelMgr.UseModel())
        {
            modelDto.Nodes.ForEach(n => modelStructureService.SetNodeDto(model, n));
            modelDto.Links.ForEach(l => modelStructureService.SetLinkDto(model, l));
            modelDto.Lines.ForEach(l => modelStructureService.SetLineLayoutDto(model, l));
        }
    }
}
