using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Cycles;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
// The diagram model of nodes, links, and lines: loading, refreshing, layout, naming, structure,
// and persistence of the model shown on the canvas.
using Dependinator.UI.Shared.VsCode;

namespace Dependinator.UI.Modeling;

record ModelInfo(string Path, Rect ViewRect, double Zoom);

interface IModelService
{
    // Loads the model at the path (cached layout if any, else a fresh parse). includeTestProjects
    // sets the parse flag for a model that has no persisted value yet, i.e. the retry after a
    // first parse failed because the solution only has test projects. sourceChoices likewise is
    // the choice of sources for a workspace model that has none saved yet.
    Task<Result<ModelInfo>> LoadAsync(
        string path,
        bool? includeTestProjects = null,
        IReadOnlyList<SourceChoice>? sourceChoices = null
    );
    Task<Result> RefreshAsync();
    Task<Result> SetIncludeTestProjectsAsync(bool includeTestProjects);

    // Whether the model has been opened here before, so that its layout and settings are saved.
    Task<bool> HasSavedModelAsync(string path);

    // The sources found in the workspace, each with whether it is in the workspace model at the
    // path: its saved choice when that model is the open one, else the choice that is proposed.
    IReadOnlyList<SourceChoice> GetSourceChoices(string workspaceModelPath);

    // Changes which sources the open workspace model is made of, and parses it again.
    Task<Result> SetSourceChoicesAsync(IReadOnlyList<SourceChoice> choices);

    // Source files changed since the last successful parse, as reported while automatic refresh
    // is off (VS Code); the Re-parse button shows a dot until a parse succeeds.
    // The open model is parsed from files of one of the kinds, as the VS Code extension names
    // the files that changed: "script" (TypeScript and JavaScript projects) or "code" (the others).
    bool IsParsedFrom(string sourceKinds);

    bool HasSourceChanges { get; }
    void MarkSourceChanged();
    void Clear();
    void ClearCache();
    void CheckLineVisibility();
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
    readonly IVsCodeSendService vsCodeSendService;
    readonly ISaveStatusService saveStatusService;

    readonly Debouncer saveDebouncer = new();
    readonly Debouncer viewSaveDebouncer = new();

    // Save requests and the last request each finished save covered, for the save status (a
    // request after a save captured the model is still pending). Guarded by saveStatusLock, which
    // also orders the status changes.
    readonly object saveStatusLock = new();
    long saveRequests;
    long savedRequests;
    long viewSaveRequests;
    long savedViewRequests;

    // Reported source changes, and how many of them the last successful parse covered (it covers
    // those reported before it started, so a change saved during the parse keeps the dot).
    int sourceChanges;
    int parsedSourceChanges;

    public ModelService(
        IModelMgr modelMgr,
        IModelListService modelListService,
        Parsing.IParserService parserService,
        IStructureService modelStructureService,
        IPersistenceService persistenceService,
        IApplicationEvents applicationEvents,
        IProgressService progressService,
        IVsCodeSendService vsCodeSendService,
        ISaveStatusService saveStatusService
    )
    {
        this.vsCodeSendService = vsCodeSendService;
        this.saveStatusService = saveStatusService;
        this.modelMgr = modelMgr;
        this.modelListService = modelListService;
        this.parserService = parserService;
        this.modelStructureService = modelStructureService;
        this.persistenceService = persistenceService;
        this.applicationEvents = applicationEvents;
        this.progressService = progressService;
        this.applicationEvents.SaveNeeded += TriggerSave;
        this.applicationEvents.ViewSaveNeeded += TriggerViewSave;
    }

    public void Dispose()
    {
        applicationEvents.SaveNeeded -= TriggerSave;
        applicationEvents.ViewSaveNeeded -= TriggerViewSave;
        saveDebouncer.Dispose();
        viewSaveDebouncer.Dispose();
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

    public async Task<Result<ModelInfo>> LoadAsync(
        string path,
        bool? includeTestProjects = null,
        IReadOnlyList<SourceChoice>? sourceChoices = null
    )
    {
        Clear();
        if (includeTestProjects is { } include)
            modelMgr.WithModel(m => m.IncludeTestProjects = include);
        if (sourceChoices is not null)
            modelMgr.WithModel(m => m.WorkspaceSources = SourceChoices.ToSaved(sourceChoices, []));

        Log.Info("Loading ...", path);
        using var _ = Timing.Start($"Load model {path}");

        // Try read cached model (with ui layout)
        var cachedResult = await ReadCachedModelAsync(path);
        if (cachedResult is not ModelInfo modelInfo)
        {
            Log.Info("Failed to read cached model", cachedResult.Error.Message);
            if (ModelPaths.IsDesignModel(path))
            {
                // A design model has no source to re-parse: an unreadable cache means its content
                // is gone, which the user must hear rather than silently get an empty model.
                if (cachedResult.Error.Message.Contains("format version", StringComparison.OrdinalIgnoreCase))
                {
                    applicationEvents.TriggerErrorReported(
                        $"The saved model '{path}' was written by an older version and could not be read, so it was opened empty."
                    );
                }
                return await CreateEmptyModelAsync(path);
            }

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
        LineVisibility.Update(model);
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

        // Panning and zooming save only the view record, so it can be newer than the model's
        // view (a missing record is fine: models saved before it existed have none).
        if (await persistenceService.ReadViewAsync(path) is ViewDto view)
            model = view.ApplyTo(model);

        var modelInfo = await LoadCachedModelDataAsync(path, model);

        return modelInfo;
    }

    public bool HasSourceChanges => sourceChanges != parsedSourceChanges;

    public void MarkSourceChanged()
    {
        bool hadSourceChanges = HasSourceChanges;
        sourceChanges++;
        if (!hadSourceChanges)
            applicationEvents.TriggerUIStateChanged();
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

        var before = ModelChangeSummary.Capture(modelMgr);
        var cyclesBefore = CycleChangeSummary.Capture(modelMgr);
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

        // A refresh runs in the background, so say what it changed (nothing: stay quiet), and
        // whether that broke or made a cycle.
        string[] summaries =
        [
            .. new[]
            {
                ModelChangeSummary.Describe(before, ModelChangeSummary.Capture(modelMgr)),
                CycleChangeSummary.Describe(cyclesBefore, CycleChangeSummary.Capture(modelMgr)),
            }.OfType<string>(),
        ];
        if (summaries.Length > 0)
            applicationEvents.TriggerInfoReported($"Model refreshed: {string.Join("; ", summaries)}.");
        return Result.Ok;
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

    public bool IsParsedFrom(string sourceKinds)
    {
        var path = modelMgr.ModelPath;
        var kinds = sourceKinds.Split(',');
        if (!ModelPaths.IsWorkspace(path))
            return kinds.Contains(WorkspaceSources.ChangeKind(path));

        // A workspace model is parsed from the files of every source in it.
        return GetSourceChoices(path)
            .Where(choice => choice.IsIncluded)
            .Any(choice => kinds.Contains(WorkspaceSources.ChangeKind(choice.Path)));
    }

    public Task<bool> HasSavedModelAsync(string path) => persistenceService.ExistsAsync(path);

    public IReadOnlyList<SourceChoice> GetSourceChoices(string workspaceModelPath)
    {
        IReadOnlyList<WorkspaceSource> saved = modelMgr.WithModel(m =>
            IsSamePath(m.Path, workspaceModelPath) ? m.WorkspaceSources : []
        );
        return SourceChoices.Of(modelListService.WorkspaceFolder, modelListService.WorkspaceSourcePaths, saved);
    }

    public async Task<Result> SetSourceChoicesAsync(IReadOnlyList<SourceChoice> choices)
    {
        if (!ModelPaths.IsWorkspace(modelMgr.ModelPath))
            return new Error("The open model is not a workspace model.");

        modelMgr.WithModel(m => m.WorkspaceSources = SourceChoices.ToSaved(choices, m.WorkspaceSources));

        // Save before refreshing, as for the test projects below: the refresh can fail.
        applicationEvents.TriggerUIStateChanged();
        TriggerSave();

        return await RefreshAsync();
    }

    static bool IsSamePath(string path1, string? path2) =>
        !string.IsNullOrWhiteSpace(path1)
        && !string.IsNullOrWhiteSpace(path2)
        && string.Equals(
            global::Shared.CloudModelPath.Normalize(path1),
            global::Shared.CloudModelPath.Normalize(path2),
            StringComparison.OrdinalIgnoreCase
        );

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
        int coveredSourceChanges = sourceChanges;
        // Read before any model-lock block is opened; the model lock is thread-affine and must
        // never be held across the parse await.
        var isWorkspace = ModelPaths.IsWorkspace(path);
        if (isWorkspace && !IsSamePath(path, modelListService.WorkspaceModelPath))
        {
            // Its sources are another folder's: the ones found here would make another model.
            var error = new Error("This workspace model is of another folder than the one that is open.");
            applicationEvents.TriggerErrorReported(
                $"Failed to parse '{ModelPaths.DisplayName(path)}'. {error.Message}"
            );
            return error;
        }

        var parseOptions = modelMgr.WithModel(m => new Parsing.SolutionParseOptions
        {
            IncludeTestProjects = m.IncludeTestProjects,
            Sources = isWorkspace ? [.. SourceChoicesOf(m).Where(c => c.IsIncluded).Select(c => c.Path)] : [],
        });
        using (
            var progress = isRefresh
                ? progressService.StartDiscreet()
                : progressService.Start($"Parsing {ModelPaths.DisplayName(path)} …")
        )
        {
            // Let the renderer process the progress state before potentially CPU-heavy parse work starts.
            await Task.Yield();

            Log.Info("Parsing ...");
            await vsCodeSendService.NotifyStatusAsync("parsing");

            var parseResult = await ParseAsync(path, parseOptions);
            await vsCodeSendService.NotifyStatusAsync("idle");
            if (parseResult is not IReadOnlyList<Parsing.Item> items)
            {
                // A failed parse leaves an empty (or unchanged) diagram, which on its own looks
                // like a solution without dependencies, so always tell the user what went wrong.
                Error e = parseResult.Error;
                Log.Warn($"Failed to parse {path}: {e.AllMessages()}");
                // The path travels with the action: a failed first parse leaves no model behind
                // (the path is only set below, after a successful parse), so there is nothing to
                // refresh and the retry has to load the solution again.
                var action =
                    e.Message.Contains("only test projects", StringComparison.OrdinalIgnoreCase)
                        ? new ErrorAction("Include test projects", ErrorActionKind.IncludeTestProjects, path)
                    : e.Message.Contains("only test files", StringComparison.OrdinalIgnoreCase)
                        ? new ErrorAction("Include test files", ErrorActionKind.IncludeTestProjects, path)
                    : isRefresh ? new ErrorAction("Retry", ErrorActionKind.RetryRefresh)
                    : new ErrorAction("Retry", ErrorActionKind.RetryLoad, path);
                applicationEvents.TriggerErrorReported(
                    $"Failed to parse '{ModelPaths.DisplayName(path)}'. {e.Message}",
                    action
                );
                return e;
            }

            using (var model = modelMgr.UseModel())
            {
                model.Path = path;
                model.UpdateStamp = DateTime.UtcNow;
                // Each parse reports all problems, so it replaces the last parse's.
                model.SetParseProblems([.. items.Select(i => i.Problem).OfType<Parsing.ParseProblem>()]);
            }

            applicationEvents.TriggerModelChanged();
            await AddOrUpdateAllItems(items);

            using (var model = modelMgr.UseModel())
            {
                if (isWorkspace)
                    UpdateWorkspaceSources(model, parseOptions.Sources, items);
                PassThroughService.UpdatePassThroughFlags(model);
            }

            // Line descriptions can only be applied once all links (and thus lines) have been added
            ApplyLineDescriptions(items);
        }

        parsedSourceChanges = coveredSourceChanges;
        CheckLineVisibility();

        return Result.Ok;
    }

    IReadOnlyList<SourceChoice> SourceChoicesOf(IModel model) =>
        SourceChoices.Of(
            modelListService.WorkspaceFolder,
            modelListService.WorkspaceSourcePaths,
            model.WorkspaceSources
        );

    // After a parse of a workspace model: saves each parsed source's root node with the choice
    // of sources, and keeps what the model had from a source that could not be parsed this time
    // (its problem item says why), so a failure does not empty that part of the diagram.
    void UpdateWorkspaceSources(IModel model, IReadOnlyList<string> parsedPaths, IReadOnlyList<Parsing.Item> items)
    {
        var rootNames = items
            .Select(i => i.Source)
            .OfType<Parsing.ParsedSource>()
            .GroupBy(source => source.Path)
            .ToDictionary(group => group.Key, group => group.First().RootName);

        // The choice as it is now: the user can have changed it while the parse ran.
        var choices = SourceChoicesOf(model);
        var saved = model.WorkspaceSources;
        model.WorkspaceSources = SourceChoices.ToSaved(choices, saved, rootNames);

        foreach (var source in model.WorkspaceSources)
        {
            var path = choices.First(choice => choice.Key == source.Path).Path;
            if (source.RootName is { } rootName && parsedPaths.Contains(path) && !rootNames.ContainsKey(path))
                modelStructureService.KeepNodeTree(model, rootName);
        }
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
        lock (saveStatusLock)
        {
            saveRequests++;
            UpdateSaveStatus();
        }
        saveDebouncer.Debounce(SaveDelay, MaxSaveDelay, Save);
    }

    void Save()
    {
        long request;
        lock (saveStatusLock)
            request = saveRequests;

        ModelDto modelData;
        string modelPath;
        using (var model = modelMgr.UseModel())
        {
            modelPath = model.Path;
            if (modelPath == "")
            {
                MarkSaved(ref savedRequests, request);
                return;
            }

            modelData = model.SerializeToDto();
        }

        SaveModelAsync(modelPath, modelData, request).RunInBackground();
    }

    async Task SaveModelAsync(string modelPath, ModelDto modelData, long request)
    {
        try
        {
            if (await persistenceService.WriteAsync(modelPath, modelData) is Error e)
                Log.Warn($"Failed to save model '{modelPath}': {e.Message}");

            // The view record overrides the model's view on load, so bring it up to date now
            // that the model is written: it may be missing (a new model) or older than the view.
            TriggerViewSave();
        }
        finally
        {
            MarkSaved(ref savedRequests, request);
        }
    }

    // Panning, zooming and resizing write only the small view record: writing the whole model
    // after every navigation cost a large serialization and transfer each time.
    void TriggerViewSave()
    {
        lock (saveStatusLock)
        {
            viewSaveRequests++;
            UpdateSaveStatus();
        }
        viewSaveDebouncer.Debounce(SaveDelay, MaxSaveDelay, SaveView);
    }

    void SaveView()
    {
        long request;
        lock (saveStatusLock)
            request = viewSaveRequests;

        ViewDto view;
        string modelPath;
        using (var model = modelMgr.UseModel())
        {
            modelPath = model.Path;
            if (modelPath == "")
            {
                MarkSaved(ref savedViewRequests, request);
                return;
            }

            view = new ViewDto
            {
                Zoom = model.Zoom,
                Offset = model.Offset,
                ViewRect = model.ViewRect,
            };
        }

        SaveViewAsync(modelPath, view, request).RunInBackground();
    }

    async Task SaveViewAsync(string modelPath, ViewDto view, long request)
    {
        try
        {
            if (await persistenceService.WriteViewAsync(modelPath, view) is Error e)
                Log.Warn($"Failed to save the view of '{modelPath}': {e.Message}");
        }
        finally
        {
            MarkSaved(ref savedViewRequests, request);
        }
    }

    // A finished save, written or failed (logged), no longer counts as pending. Saves can finish
    // out of order, so the latest request covered is kept.
    void MarkSaved(ref long saved, long request)
    {
        lock (saveStatusLock)
        {
            saved = Math.Max(saved, request);
            UpdateSaveStatus();
        }
    }

    // Called with saveStatusLock held, so the status changes reach the service in order
    void UpdateSaveStatus() =>
        saveStatusService.SetPending(savedRequests < saveRequests || savedViewRequests < viewSaveRequests);

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
        // The saved rows were laid out with the saved fold of their cards' private rows.
        foreach (var node in model.Nodes.Values)
            MemberCard.RestoreLayoutState(node);
        RequireChildrenLayoutIfOlder(model, modelDto.LayoutVersion);
    }

    // A model saved with an older automatic layout gets it redone (lazily, as containers are
    // shown), except in the containers the user arranged; the flags are saved with the model
    // until each container is laid out. Member rows are never arranged by hand (their card
    // keeps them in order), so containers with members are laid out again even when arranged.
    internal static void RequireChildrenLayoutIfOlder(IModel model, int layoutVersion)
    {
        if (layoutVersion >= NodeLayout.CurrentLayoutVersion)
            return;
        RequireChildrenLayout(model);
        foreach (var node in model.Nodes.Values)
        {
            if (node.IsChildrenLayoutCustomized && NodeLayout.HasMemberChildren(node))
                node.IsChildrenLayoutRequired = true;
            // Before version 4 nested types sat right of a type's card; now they go below it.
            if (layoutVersion < 4 && node.IsChildrenLayoutCustomized && NodeViewPolicy.IsMemberCardNode(node))
                NodeLayout.ForgetCustomLayout(node);
        }
    }
}
