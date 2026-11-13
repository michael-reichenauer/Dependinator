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

    // Parses the model again after files of the kinds changed (IsParsedFrom). A workspace model
    // parses only its sources of those kinds, and keeps what it has of the others.
    Task<Result> RefreshChangedAsync(IReadOnlyCollection<string> changedKinds);
    Task<Result> SetIncludeTestProjectsAsync(bool includeTestProjects);

    // Whether the model has been opened here before, so that its layout and settings are saved.
    Task<bool> HasSavedModelAsync(string path);

    // The sources found in the workspace, each with whether it is in the workspace model at the
    // path: its saved choice when that model is the open one, else the choice that is proposed,
    // or what was chosen for a first parse that failed (those are not IsNew).
    IReadOnlyList<SourceChoice> GetSourceChoices(string workspaceModelPath);

    // Changes which sources the open workspace model is made of, and parses the kinds of
    // sources that it then has more or fewer of.
    Task<Result> SetSourceChoicesAsync(IReadOnlyList<SourceChoice> choices);

    // Source files changed since the last successful parse, as reported while automatic refresh
    // is off (VS Code); the Re-parse button shows a dot until a parse succeeds.
    // The open model is parsed from files of one of the kinds, as the VS Code extension names
    // the files that changed (WorkspaceSources.ChangeKind: "script", "bicep" or "code"). A
    // workspace model is also made anew when the sources of the workspace changed
    // (WorkspaceSources.SourcesChangedKind): it can have one more or one less.
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

    readonly SemaphoreSlim refreshGate = new(1, 1);
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

    // The sources chosen for a workspace model whose first parse has not succeeded yet. The
    // choice is saved with the model, which is there only once it is parsed: until then the
    // next try starts from this (GetSourceChoices). Forgotten once that model is open.
    IReadOnlyList<WorkspaceSource> unparsedSourceChoices = [];

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
        {
            unparsedSourceChoices = SourceChoices.ToSaved(sourceChoices, []);
            modelMgr.WithModel(m => m.WorkspaceSources = unparsedSourceChoices);
        }

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
            if (parsedModelInfo is ModelInfo && IsWorkspaceModel(path))
                unparsedSourceChoices = [];
            TriggerSave();
            applicationEvents.TriggerModelChanged();
            applicationEvents.TriggerUIStateChanged();
            return parsedModelInfo;
        }

        if (IsWorkspaceModel(path))
            unparsedSourceChoices = [];
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

    public Task<Result> RefreshAsync() => RefreshAsync(null);

    public Task<Result> RefreshChangedAsync(IReadOnlyCollection<string> changedKinds) => RefreshAsync(changedKinds);

    // changedKinds: the kinds of files that changed, or null to parse everything.
    // Refreshes run one at a time: each plans from what the one before left in the model (the
    // sources it has), and removes what it did not update.
    async Task<Result> RefreshAsync(IReadOnlyCollection<string>? changedKinds)
    {
        var path = modelMgr.ModelPath;
        await refreshGate.WaitAsync();
        try
        {
            // A refresh that waited is of the model it was asked for, not of one opened since
            if (modelMgr.ModelPath != path)
            {
                Log.Info("Another model was opened, refresh skipped", path);
                return Result.Ok;
            }

            return await RefreshModelAsync(changedKinds);
        }
        finally
        {
            refreshGate.Release();
        }
    }

    async Task<Result> RefreshModelAsync(IReadOnlyCollection<string>? changedKinds)
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
        var result = await ParseAndUpdateAsync(path, true, changedKinds);
        if (result is not bool isUpdated)
            return result.Error;
        if (!isUpdated)
            return Result.Ok;

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

        // A workspace model is parsed from the files of every source in it, and has something
        // to do when it gets or loses a source.
        // A change of only these kinds leaves it as it is when none of its sources is of
        // them, and it neither gets nor loses a source.
        return IsWorkspaceModel(path)
            && !modelMgr.WithModel(m => PlanSources(m, SourceChoicesOf(m), kinds).IsNothingToDo);
    }

    // Whether the path is the model of this workspace (not another folder's workspace model).
    bool IsWorkspaceModel(string path) => ModelListService.IsSamePath(path, modelListService.WorkspaceModelPath);

    public Task<bool> HasSavedModelAsync(string path) => persistenceService.ExistsAsync(path);

    public IReadOnlyList<SourceChoice> GetSourceChoices(string workspaceModelPath)
    {
        IReadOnlyList<WorkspaceSource> saved = modelMgr.WithModel(m =>
            ModelListService.IsSamePath(m.Path, workspaceModelPath) ? m.WorkspaceSources : unparsedSourceChoices
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

        // No files changed: only the kinds of the sources that were added or taken out are parsed.
        return await RefreshAsync([]);
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

    // False: there was nothing to do, the model is as it was (a refresh for changedKinds of a
    // workspace model that has no source of those kinds, and neither gets nor loses a source).
    async Task<Result<bool>> ParseAndUpdateAsync(
        string path,
        bool isRefresh = false,
        IReadOnlyCollection<string>? changedKinds = null
    )
    {
        using var _ = Timing.Start($"Parsed and added model items {path}");
        int coveredSourceChanges = sourceChanges;
        // Read before any model-lock block is opened; the model lock is thread-affine and must
        // never be held across the parse await.
        var isWorkspace = ModelPaths.IsWorkspace(path);
        if (isWorkspace && !IsWorkspaceModel(path))
        {
            // Its sources are another folder's: the ones found here would make another model.
            var error = new Error("This workspace model is of another folder than the one that is open.");
            applicationEvents.TriggerErrorReported(
                $"Failed to parse '{ModelPaths.DisplayName(path)}'. {error.Message}"
            );
            return error;
        }

        // The sources of a workspace model that are parsed this time, and the ones that are in
        // the model but not parsed (keptSources): the model keeps what it has of those.
        IReadOnlyList<SourceChoice> keptSources = [];
        var isNothingToDo = false;
        var parseOptions = modelMgr.WithModel(m =>
        {
            var sources = isWorkspace ? SourceChoicesOf(m) : [];
            var plan = PlanSources(m, sources, changedKinds);
            keptSources = plan.Kept;
            isNothingToDo = isWorkspace && changedKinds is not null && plan.IsNothingToDo;
            return new Parsing.SolutionParseOptions
            {
                IncludeTestProjects = m.IncludeTestProjects,
                Sources = [.. plan.Parsed.Select(c => c.Path)],
                HasOtherSources = plan.Kept.Count > 0,
                // The root names the sources had when last parsed, which they keep, also the
                // ones that are left out now (WorkspaceItems)
                KnownSources =
                [
                    .. sources
                        .Select(c => (c.Path, SourceChoices.Saved(m.WorkspaceSources, c.Key)?.RootName))
                        .Where(source => source.RootName is not null)
                        .Select(source => new Parsing.ParsedSource(source.Path, source.RootName!)),
                ],
            };
        });
        if (isNothingToDo)
            return false;

        var isParsed = true;
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

            // Nothing to parse when a source was only taken out of a workspace model
            isParsed = !(isWorkspace && changedKinds is not null && parseOptions.Sources.Count == 0);
            var parseResult = isParsed ? await ParseAsync(path, parseOptions) : NoItems;
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

            // A refresh whose model was closed while it parsed (another one was opened) is not
            // of the model that is open now.
            if (isRefresh && modelMgr.ModelPath != path)
            {
                Log.Info("Another model was opened while parsing, refresh dropped", path);
                return false;
            }

            // The sources that are new in a workspace model bring the layout of their own model
            var newRoots = isWorkspace
                ? modelMgr.WithModel(m =>
                    items
                        .Select(i => i.Source)
                        .OfType<Parsing.ParsedSource>()
                        .Where(s => !m.Nodes.ContainsKey(NodeId.FromName(s.RootName)))
                        .ToList()
                )
                : [];
            var sourceLayouts = await ReadSourceModelsAsync(newRoots);

            using (var model = modelMgr.UseModel())
            {
                model.Path = path;
                model.UpdateStamp = DateTime.UtcNow;
                // Each parse reports all problems of what it parsed, so they replace the last
                // parse's, but for the problems of the sources that were not parsed this time.
                model.SetParseProblems([
                    .. items.Select(i => i.Problem).OfType<Parsing.ParseProblem>(),
                    .. model.ParseProblems.Where(p => keptSources.Any(source => IsSameKey(source.Key, p.Source))),
                ]);
            }

            applicationEvents.TriggerModelChanged();
            await AddOrUpdateAllItems(items);

            using (var model = modelMgr.UseModel())
            {
                if (isWorkspace)
                    UpdateWorkspaceSources(model, parseOptions.Sources, [.. keptSources.Select(c => c.Path)], items);
                foreach (var (rootName, sourceModel) in sourceLayouts)
                    SourceLayout.Apply(model, modelStructureService, sourceModel, rootName);
                PassThroughService.UpdatePassThroughFlags(model);
            }

            // Line descriptions can only be applied once all links (and thus lines) have been added
            ApplyLineDescriptions(items);
        }

        // The source changes that were only marked (automatic refresh is off) are covered by a
        // parse of everything, or of the kinds VS Code says changed, not by one that only adds
        // or removes some sources.
        if (changedKinds is null || changedKinds.Count > 0 || isParsed && keptSources.Count == 0)
            parsedSourceChanges = coveredSourceChanges;
        CheckLineVisibility();

        return true;
    }

    IReadOnlyList<SourceChoice> SourceChoicesOf(IModel model) =>
        SourceChoices.Of(
            modelListService.WorkspaceFolder,
            modelListService.WorkspaceSourcePaths,
            model.WorkspaceSources
        );

    // The saved models of the sources, by their root names in the workspace model; a source
    // that was never opened on its own has none.
    async Task<IReadOnlyList<(string RootName, ModelDto Model)>> ReadSourceModelsAsync(
        IReadOnlyList<Parsing.ParsedSource> sources
    )
    {
        List<(string, ModelDto)> models = [];
        foreach (var source in sources)
        {
            if (await persistenceService.ReadAsync(source.Path) is ModelDto model)
                models.Add((source.RootName, model));
        }
        return models;
    }

    static readonly Result<IReadOnlyList<Parsing.Item>> NoItems = new List<Parsing.Item>();

    static bool IsSameKey(string key, string? otherKey) =>
        string.Equals(key, otherKey, StringComparison.OrdinalIgnoreCase);

    // What a refresh of a workspace model does with its sources: the ones it parses, the ones
    // in the model that it does not parse (the model keeps what it has of them), and whether
    // the model has a source that is no longer in it, of which everything is removed.
    record SourcePlan(IReadOnlyList<SourceChoice> Parsed, IReadOnlyList<SourceChoice> Kept, bool HasRemoved)
    {
        public bool IsNothingToDo => Parsed.Count == 0 && !HasRemoved;
    }

    // All included sources are parsed, or after a change of some kinds of files (changedKinds)
    // only the sources of those kinds. A kind is also parsed when the model gets a source of
    // it, or loses one (WorkspaceSource.IsInModel): sources of one kind can share nodes (a
    // project of two solutions), so they are parsed together.
    static SourcePlan PlanSources(
        IModel model,
        IReadOnlyList<SourceChoice> sources,
        IReadOnlyCollection<string>? changedKinds
    )
    {
        IReadOnlyList<SourceChoice> included = [.. sources.Where(c => c.IsIncluded)];
        var saved = model.WorkspaceSources;

        // A model that does not say which sources it has (it was saved by an earlier version,
        // or its only parse failed) is parsed as a whole. When the user then takes a source
        // out, there is no saying what the model has of it, so all that is not parsed goes.
        if (changedKinds is null || !saved.Any(s => s.IsInModel))
            return new SourcePlan(included, [], HasRemoved: changedKinds is { Count: 0 });

        var added = included.Where(c => SourceChoices.Saved(saved, c.Key)?.IsInModel != true).Select(c => c.Path);
        var removed = saved
            .Where(s => s.IsInModel && !included.Any(c => IsSameKey(c.Key, s.Path)))
            .Select(s => s.Path)
            .ToList();
        var kinds = changedKinds
            .Concat(added.Concat(removed).Select(WorkspaceSources.ChangeKind))
            .ToHashSet(StringComparer.Ordinal);

        IReadOnlyList<SourceChoice> parsed =
        [
            .. included.Where(c => kinds.Contains(WorkspaceSources.ChangeKind(c.Path))),
        ];
        return new SourcePlan(parsed, [.. included.Except(parsed)], removed.Count > 0);
    }

    // After a parse of a workspace model: saves each parsed source's root node with the choice
    // of sources, and which sources the model now has (the parsed and the kept ones). It keeps
    // what the model had from a source that was not parsed this time (keptPaths) or could not
    // be parsed (its problem item says why), so neither empties that part of the diagram.
    void UpdateWorkspaceSources(
        IModel model,
        IReadOnlyList<string> parsedPaths,
        IReadOnlyList<string> keptPaths,
        IReadOnlyList<Parsing.Item> items
    )
    {
        var rootNames = items
            .Select(i => i.Source)
            .OfType<Parsing.ParsedSource>()
            .GroupBy(source => source.Path)
            .ToDictionary(group => group.Key, group => group.First().RootName);

        // The choice as it is now: the user can have changed it while the parse ran.
        var choices = SourceChoicesOf(model);
        model.WorkspaceSources = SourceChoices.ToSaved(
            choices,
            model.WorkspaceSources,
            rootNames,
            inModelPaths: [.. parsedPaths, .. keptPaths]
        );

        bool IsFailed(SourceChoice c) => parsedPaths.Contains(c.Path) && !rootNames.ContainsKey(c.Path);
        foreach (var choice in choices.Where(c => IsFailed(c) || keptPaths.Contains(c.Path)))
        {
            if (SourceChoices.Saved(model.WorkspaceSources, choice.Key)?.RootName is { } rootName)
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
    internal static void RequireChildrenLayoutIfOlder(IModel model, int layoutVersion) =>
        RequireChildrenLayoutIfOlder(model.Nodes.Values, layoutVersion);

    internal static void RequireChildrenLayoutIfOlder(IEnumerable<Node> nodes, int layoutVersion)
    {
        if (layoutVersion >= NodeLayout.CurrentLayoutVersion)
            return;
        foreach (var node in nodes)
        {
            if (node.Children.Count > 0 && !node.IsChildrenLayoutCustomized)
                node.IsChildrenLayoutRequired = true;
            if (node.IsChildrenLayoutCustomized && NodeLayout.HasMemberChildren(node))
                node.IsChildrenLayoutRequired = true;
            // Before version 4 nested types sat right of a type's card; now they go below it.
            if (layoutVersion < 4 && node.IsChildrenLayoutCustomized && NodeViewPolicy.IsMemberCardNode(node))
                NodeLayout.ForgetCustomLayout(node);
        }
    }
}
