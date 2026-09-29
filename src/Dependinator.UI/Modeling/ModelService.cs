using Dependinator.Core.Shared;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

// The diagram model of nodes, links, and lines: loading, refreshing, layout, naming, structure,
// and persistence of the model shown on the canvas.
namespace Dependinator.UI.Modeling;

record ModelInfo(string Path, Rect ViewRect, double Zoom);

interface IModelService
{
    Task<Result<ModelInfo>> LoadAsync(string path);
    Task<Result> RefreshAsync();
    Task<Result> SetIncludeTestProjectsAsync(bool includeTestProjects);
    void Clear();
    void ClearCache();
    void CheckLineVisibility();
    Task LayoutNode(NodeId nodeId, bool recursively = false);
    Result<ModelDto> GetCurrentModelDto();
    Task<Result> WriteModelAsync(string modelPath, ModelDto modelDto);
    Task<Result<ModelInfo>> ReplaceCurrentModelAsync(ModelDto modelDto);
}

[Scoped]
class ModelService : IModelService, IDisposable
{
    static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(0.5);
    static readonly TimeSpan MaxSaveDelay = TimeSpan.FromSeconds(10);

    readonly IModelMgr modelMgr;
    readonly IModelListService modelListService;
    readonly Parsing.IParserService parserService;
    readonly IStructureService modelStructureService;
    readonly IPersistenceService persistenceService;
    readonly IApplicationEvents applicationEvents;
    readonly IProgressService progressService;

    readonly Debouncer saveDebouncer = new();

    public ModelService(
        IModelMgr modelMgr,
        IModelListService modelListService,
        Parsing.IParserService parserService,
        IStructureService modelStructureService,
        IPersistenceService persistenceService,
        IApplicationEvents applicationEvents,
        IProgressService progressService
    )
    {
        this.modelMgr = modelMgr;
        this.modelListService = modelListService;
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
        applicationEvents.TriggerModelChanged();
        TriggerSave();
    }

    public void Clear()
    {
        using (var model = modelMgr.UseModel())
        {
            model.Clear();
        }
        applicationEvents.TriggerModelChanged();
    }

    public Result<ModelDto> GetCurrentModelDto()
    {
        using (var model = modelMgr.UseModel())
        {
            if (string.IsNullOrWhiteSpace(model.Path))
                return new Error("Model is not loaded");

            return model.SerializeToDto();
        }
    }

    public async Task<Result<ModelInfo>> ReplaceCurrentModelAsync(ModelDto modelDto)
    {
        var modelPath = modelMgr.ModelPath;
        if (string.IsNullOrWhiteSpace(modelPath))
            return new Error("Model is not loaded");

        if (await WriteModelAsync(modelPath, modelDto) is Error error)
            return error;

        var modelInfo = await LoadCachedModelDataAsync(modelPath, modelDto);
        RefreshAsync().RunInBackground();
        return modelInfo;
    }

    public Task<Result> WriteModelAsync(string modelPath, ModelDto modelDto)
    {
        return persistenceService.WriteAsync(modelPath, modelDto);
    }

    public async Task<Result<ModelInfo>> LoadAsync(string path)
    {
        Clear();

        Log.Info("Loading ...", path);
        using var _ = Timing.Start($"Load model {path}");

        // Try read cached model (with ui layout)
        var cachedResult = await ReadCachedModelAsync(path);
        if (cachedResult is not ModelInfo modelInfo)
        {
            Log.Info("Failed to read cached model", cachedResult.Error.Message);
            if (ModelPaths.IsDesignModel(path))
                return await CreateEmptyModelAsync(path);

            var parsedModelInfo = await ParseNewModelAsync(path);
            TriggerSave();
            applicationEvents.TriggerModelChanged();
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

    // Creates and persists a new empty design model (root node only). Design models are
    // manually edited and never parsed; the persisted model is their only source of truth,
    // so it is written immediately (not debounced) to survive an instant reload.
    async Task<Result<ModelInfo>> CreateEmptyModelAsync(string path)
    {
        Log.Info("Creating empty design model", path);
        ModelDto modelDto;
        using (var model = modelMgr.UseModel())
        {
            model.Path = path;
            model.UpdateStamp = DateTime.UtcNow;
            modelDto = model.SerializeToDto();
        }

        if (await persistenceService.WriteAsync(path, modelDto) is Error e)
            return e;

        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
        return new ModelInfo(path, Rect.None, 0);
    }

    async Task<Result<ModelInfo>> ReadCachedModelAsync(string path)
    {
        using var progress = progressService.Start("Loading ...");
        var modelResult = await persistenceService.ReadAsync(path);
        if (modelResult is not ModelDto model)
            return modelResult.Error;

        var modelInfo = await LoadCachedModelDataAsync(path, model);

        return modelInfo;
    }

    public async Task<Result> RefreshAsync()
    {
        var path = modelMgr.ModelPath;
        if (ModelPaths.IsDesignModel(path))
        {
            Log.Info("Design model, parsing skipped", path);
            return Result.Ok;
        }

        if (!modelListService.IsLocalPath(path))
        {
            Log.Info("Not a local path", path);
            return Result.Ok;
        }

        if (await ParseAndUpdateAsync(path, true) is Error e)
            return e;
        using (var model = modelMgr.UseModel())
        {
            modelStructureService.ClearNotUpdated(model);
            PassThroughService.UpdatePassThroughFlags(model);
        }

        applicationEvents.TriggerModelChanged();
        TriggerSave();
        applicationEvents.TriggerUIStateChanged();
        return Result.Ok;
    }

    public Task LayoutNode(NodeId nodeId, bool recursively = false)
    {
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out Node? node))
                return Task.CompletedTask;

            LayoutNode(node, recursively);
        }

        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
        applicationEvents.TriggerSaveNeeded();
        return Task.CompletedTask;
    }

    void LayoutNode(Node node, bool recursively)
    {
        NodeLayout.AdjustChildren(node, forceAllChildren: true);
        if (recursively)
            node.Children.ForEach(n => LayoutNode(n, recursively));
    }

    async Task<Result<ModelInfo>> ParseNewModelAsync(string path)
    {
        if (await ParseAndUpdateAsync(path) is Error e)
            return e;

        // Rendering during the progressive parse may have laid out nodes before all their
        // children's links had arrived; re-flag them so the next render lays out each node
        // with the complete dependency graph.
        using (var model = modelMgr.UseModel())
        {
            RequireChildrenLayout(model);
        }

        return new ModelInfo(path, Rect.None, 0);
    }

    internal static void RequireChildrenLayout(IModel model)
    {
        foreach (var node in model.Nodes.Values)
        {
            if (node.Children.Count > 0 && !node.IsChildrenLayoutCustomized)
                node.IsChildrenLayoutRequired = true;
        }
    }

    public async Task<Result> SetIncludeTestProjectsAsync(bool includeTestProjects)
    {
        if (modelMgr.WithModel(m => m.IncludeTestProjects == includeTestProjects))
            return Result.Ok;

        modelMgr.WithModel(m => m.IncludeTestProjects = includeTestProjects);

        // Update the menu checkbox immediately; the re-parse below can take a while.
        applicationEvents.TriggerUIStateChanged();

        // Save before refreshing: RefreshAsync returns early for design models and non-local
        // paths, before it reaches its own TriggerSave.
        TriggerSave();

        return await RefreshAsync();
    }

    async Task<Result> ParseAndUpdateAsync(string path, bool isRefresh = false)
    {
        using var _ = Timing.Start($"Parsed and added model items {path}");
        // Read before any model-lock block is opened; the model lock is thread-affine and must
        // never be held across the parse await.
        var parseOptions = modelMgr.WithModel(m => new Parsing.SolutionParseOptions
        {
            IncludeTestProjects = m.IncludeTestProjects,
        });
        using (var progress = isRefresh ? progressService.StartDiscreet() : progressService.Start("Parsing"))
        {
            // Let the renderer process the progress state before potentially CPU-heavy parse work starts.
            await Task.Yield();

            Log.Info("Parsing ...");

            var parseResult = await ParseAsync(path, parseOptions);
            if (parseResult is not IReadOnlyList<Parsing.Item> items)
            {
                // A failed parse leaves an empty (or unchanged) diagram, which on its own looks
                // like a solution without dependencies, so always tell the user what went wrong.
                Error e = parseResult.Error;
                Log.Warn($"Failed to parse {path}: {e.AllMessages()}");
                applicationEvents.TriggerErrorReported($"Failed to parse '{Path.GetFileName(path)}'. {e.Message}");
                return e;
            }

            using (var model = modelMgr.UseModel())
            {
                model.Path = path;
                model.UpdateStamp = DateTime.UtcNow;
            }

            applicationEvents.TriggerModelChanged();
            await AddOrUpdateAllItems(items);

            using (var model = modelMgr.UseModel())
            {
                PassThroughService.UpdatePassThroughFlags(model);
            }

            // Line descriptions can only be applied once all links (and thus lines) have been added
            ApplyLineDescriptions(items);
        }

        CheckLineVisibility();

        return Result.Ok;
    }

    async Task<Result<IReadOnlyList<Parsing.Item>>> ParseAsync(string path, Parsing.SolutionParseOptions options)
    {
        using var _ = Timing.Start($"Parsed {path}");
        try
        {
            return await parserService.ParseAsync(path, options);
        }
        catch (Exception e)
        {
            // The parser may run in the LSP process, where a lost/failed RPC call throws
            // instead of returning a result.
            Log.Exception(e, $"Failed to call parser for {path}");
            return new Error("The parser could not be reached.", e);
        }
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
            if (modelPath == "")
                return;

            modelData = model.SerializeToDto();
        }

        persistenceService.WriteAsync(modelPath, modelData).RunInBackground();
    }

    async Task<ModelInfo> LoadCachedModelDataAsync(string path, ModelDto modelDto)
    {
        using (var model = modelMgr.UseModel())
        {
            model.Clear();
            model.SetFromDto(path, modelDto);
        }

        await Task.Run(() => SetNodeAndLinkDtos(modelDto));
        // Compute line visibility before triggering the render so the first cached tile
        // uses correct hidden state (lines to hidden nodes must render as hidden).
        CheckLineVisibility();
        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
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
        using var model = modelMgr.UseModel();
        foreach (var parsedItem in parsedItems)
        {
            if (parsedItem.Node is not null)
                modelStructureService.AddOrUpdateNode(model, parsedItem.Node);
            if (parsedItem.Link is not null)
                modelStructureService.AddOrUpdateLink(model, parsedItem.Link);
        }
    }

    void ApplyLineDescriptions(IReadOnlyList<Parsing.Item> parsedItems)
    {
        using var model = modelMgr.UseModel();
        foreach (var parsedItem in parsedItems)
        {
            if (parsedItem.LineDescription is not null)
                modelStructureService.SetLineDescription(model, parsedItem.LineDescription);
        }
    }

    void SetNodeAndLinkDtos(ModelDto modelDto)
    {
        using var model = modelMgr.UseModel();
        modelDto.Nodes.ForEach(n => modelStructureService.SetNodeDto(model, n));
        modelDto.Links.ForEach(l => modelStructureService.SetLinkDto(model, l));
        modelDto.Lines.ForEach(l => modelStructureService.SetLineLayoutDto(model, l));
        PassThroughService.UpdatePassThroughFlags(model);
    }
}
