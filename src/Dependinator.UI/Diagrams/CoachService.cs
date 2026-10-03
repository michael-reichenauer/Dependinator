using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Dependencies;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Diagrams;

enum CoachStep
{
    Welcome,
    Zoom,
    Select,
    Explore,
    Search,
    Done,
}

// What the callout shows for a step. Text may carry simple markup (bold, a link).
record CoachCard(string Icon, string Title, string Text, string NextLabel);

// The first-run tour: a short series of callouts over the diagram that teach the four things
// a new user needs (zoom to look inside, click a node, open the explorer, search). Each step
// moves on by itself once the user does the thing, or on Next; Skip ends it. Shown once per
// browser/host (Config.IsCoachSeen) and again on request from Menu › Help › Show Tips.
interface ICoachService
{
    bool IsActive { get; }
    CoachStep Step { get; }
    CoachCard Card { get; }

    // 1-based position among the task steps (zoom, select, explore, search); 0 for the welcome
    // and the closing card.
    int TaskNumber { get; }
    int TaskCount { get; }

    event Action? Changed;

    // On startup: begins the tour unless it has been seen (or in test mode, where the e2e
    // tests start it themselves from the Help menu).
    Task StartIfFirstRunAsync(bool isDemoModel);
    void Start();
    void Next();
    Task DismissAsync();

    // The search dialog opened (the app bar tells us; there is no event for it).
    void NoticeSearchOpened();
}

[Scoped]
class CoachService : ICoachService, IDisposable
{
    // How much the zoom has to change for the zoom step to count as done.
    const double ZoomStepFactor = 1.4;

    static readonly CoachStep[] TaskSteps = [CoachStep.Zoom, CoachStep.Select, CoachStep.Explore, CoachStep.Search];

    readonly IApplicationEvents applicationEvents;
    readonly IConfigService configService;
    readonly ISelectionService selectionService;
    readonly IDependenciesService dependenciesService;
    readonly IModelMgr modelMgr;

    bool isDemoModel;
    double zoomAtStepStart;
    string selectedIdAtStepStart = "";
    bool wasExplorerOpenAtStepStart;

    public CoachService(
        IApplicationEvents applicationEvents,
        IConfigService configService,
        ISelectionService selectionService,
        IDependenciesService dependenciesService,
        IModelMgr modelMgr
    )
    {
        this.applicationEvents = applicationEvents;
        this.configService = configService;
        this.selectionService = selectionService;
        this.dependenciesService = dependenciesService;
        this.modelMgr = modelMgr;
        applicationEvents.UIStateChanged += OnUiStateChanged;
        applicationEvents.ViewChanged += OnViewChanged;
    }

    public bool IsActive { get; private set; }
    public CoachStep Step { get; private set; }
    public int TaskNumber => Array.IndexOf(TaskSteps, Step) + 1;
    public int TaskCount => TaskSteps.Length;
    public event Action? Changed;

    public CoachCard Card => GetCard(Step, isDemoModel);

    public async Task StartIfFirstRunAsync(bool isDemoModel)
    {
        if (Dependinator.Core.Build.IsTestMode)
            return;
        var config = await configService.GetAsync();
        if (config.IsCoachSeen)
            return;
        this.isDemoModel = isDemoModel;
        Begin();
    }

    public void Start()
    {
        isDemoModel = modelMgr.WithModel(m => m.Path) == DemoModel.Path;
        Begin();
    }

    void Begin()
    {
        Step = CoachStep.Welcome;
        IsActive = true;
        Changed?.Invoke();
    }

    public void Next()
    {
        if (!IsActive)
            return;
        if (Step == CoachStep.Done)
        {
            _ = DismissAsync();
            return;
        }
        Step++;
        EnterStep();
        Changed?.Invoke();
    }

    // Remember the state the step starts from, so only something the user does during the
    // step counts (a node that was already selected does not complete "click a node").
    void EnterStep()
    {
        zoomAtStepStart = modelMgr.WithModel(m => m.Zoom);
        selectedIdAtStepStart = selectionService.SelectedId.Id;
        wasExplorerOpenAtStepStart = dependenciesService.IsShowExplorer;
    }

    public async Task DismissAsync()
    {
        if (!IsActive)
            return;
        IsActive = false;
        Changed?.Invoke();
        await configService.SetAsync(config => config.IsCoachSeen = true);
    }

    public void NoticeSearchOpened()
    {
        if (IsActive && Step == CoachStep.Search)
            Next();
    }

    void OnUiStateChanged()
    {
        if (!IsActive)
            return;
        switch (Step)
        {
            case CoachStep.Select
                when selectionService.IsSelected && selectionService.SelectedId.Id != selectedIdAtStepStart:
                Next();
                break;
            case CoachStep.Explore when dependenciesService.IsShowExplorer && !wasExplorerOpenAtStepStart:
                Next();
                break;
        }
    }

    void OnViewChanged()
    {
        if (!IsActive || Step != CoachStep.Zoom)
            return;
        var zoom = modelMgr.WithModel(m => m.Zoom);
        if (zoomAtStepStart <= 0 || zoom <= 0)
            return;
        if (Math.Abs(Math.Log(zoom / zoomAtStepStart)) >= Math.Log(ZoomStepFactor))
            Next();
    }

    internal static CoachCard GetCard(CoachStep step, bool isDemoModel) =>
        step switch
        {
            CoachStep.Welcome => new(
                MudBlazor.Icons.Material.Outlined.TipsAndUpdates,
                "Welcome to Dependinator",
                (isDemoModel ? DemoIntro : "Your diagram is open. ")
                    + OwnModelsHint
                    + "Take a one-minute tour of the four things to know?",
                "Take the tour"
            ),
            CoachStep.Zoom => new(
                MudBlazor.Icons.Material.Outlined.ZoomIn,
                "Zoom to look inside",
                "Scroll (or pinch) over a node to zoom into it: its namespaces, classes and members come into "
                    + "view as you go. Drag to pan; <b>Fit to Screen</b> in the toolbar brings everything back. Try it now.",
                "Next"
            ),
            CoachStep.Select => new(
                MudBlazor.Icons.Material.Outlined.TouchApp,
                "Click a node",
                "Clicking a node shows its toolbar with everything you can do with it. Double-click zooms to it, "
                    + "<b>Shift</b>+click adds more nodes, <b>Esc</b> deselects.",
                "Next"
            ),
            CoachStep.Explore => new(
                Icons.Icon.DependenciesIcon,
                "See what it uses",
                "The toolbar's dependencies and references buttons open the explorer: what this node uses, or who "
                    + "uses it. Its lines are drawn in the diagram while the explorer is open.",
                "Next"
            ),
            CoachStep.Search => new(
                MudBlazor.Icons.Material.Outlined.Search,
                "Find anything",
                "Press <b>Ctrl+F</b> (or the search button) and type part of a name, even just the first letters "
                    + "of each word, to jump to any node.",
                "Next"
            ),
            _ => new(
                MudBlazor.Icons.Material.Outlined.CheckCircle,
                "That's the basics",
                "<b>Menu › Help</b> has the full list of controls, and <b>Menu › Help › Show Tips</b> brings this "
                    + "tour back.",
                "Done"
            ),
        };

    const string DemoIntro = "You don't have a diagram yet, so a <b>demo diagram</b> is open for you to explore. ";

    // Where the user's own models come from differs per host: the VS Code extension parses the
    // workspace solution, the web app shows models synced from VS Code (or hand-drawn ones).
    static string OwnModelsHint =>
        Dependinator.Core.Build.IsVsCodeExtWasm
            ? "Your workspace's solution is parsed and opened automatically when one is found; switch between "
                + "solutions and models under <b>Menu › Models</b>. "
            : "To map your own code, install the "
                + "<a href=\"https://marketplace.visualstudio.com/items?itemName=michaelreichenauer.dependinator\" "
                + "target=\"_blank\" rel=\"noopener\">Dependinator VS Code extension</a> and enable device sync there "
                + "and here: your models then appear under <b>Menu › Models</b>. You can also sketch an architecture "
                + "by hand with <b>Menu › Models › New Model</b>. ";

    public void Dispose()
    {
        applicationEvents.UIStateChanged -= OnUiStateChanged;
        applicationEvents.ViewChanged -= OnViewChanged;
    }
}
