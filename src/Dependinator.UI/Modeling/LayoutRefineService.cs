using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling.Layout;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling;

interface ILayoutRefineService
{
    void Start();
}

// Refines the layout of containers that were too large for the thorough layout pass when first
// shown: they got the fast pass and IsChildrenLayoutRefinePending (see NodeLayout). In idle
// moments (no clicks, drags, wheel, keys or view changes for a while, and no parse running) the
// thorough pass runs a slice of work at a time, visible containers first. The model is locked
// only to capture a container and to apply the result. The user may be looking at the
// container, so the result is applied only when the container did not change meanwhile and the
// layout reads clearly better; node sizes stay as they are, only positions and lines change. A
// container the view is zoomed into (larger than the view) waits: rearranging it would move what
// the user looks at out of sight.
[Scoped]
class LayoutRefineService(
    IModelMgr modelMgr,
    IApplicationEvents applicationEvents,
    IProgressService progressService,
    IPointerEventService pointerEventService,
    IKeyboardService keyboardService
) : ILayoutRefineService, IDisposable
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    static readonly TimeSpan IdleTime = TimeSpan.FromMilliseconds(700);

    // Work per slice between yields to the UI (WebAssembly runs on the UI thread)
    const long SliceWork = 20_000;

    // A refined layout must score at least this much better to be worth the visible change
    const double MinImprovement = 0.10;

    // Containers that keep changing while being refined are given up after this many tries
    const int MaxAttempts = 3;

    readonly CancellationTokenSource cancellation = new();
    readonly Dictionary<NodeId, int> attempts = [];
    long lastActivityTicks = DateTime.UtcNow.Ticks;
    bool isStarted;

    public void Start()
    {
        if (isStarted)
            return;
        isStarted = true;

        pointerEventService.PointerDown += OnPointer;
        pointerEventService.PointerUp += OnPointer;
        pointerEventService.PointerMove += OnPointerMove;
        pointerEventService.Wheel += OnPointer;
        keyboardService.KeyDown += OnKeyDown;
        applicationEvents.ViewChanged += OnActivity;
        RunAsync(cancellation.Token).RunInBackground();
    }

    public void Dispose()
    {
        cancellation.Cancel();
        if (!isStarted)
            return;
        pointerEventService.PointerDown -= OnPointer;
        pointerEventService.PointerUp -= OnPointer;
        pointerEventService.PointerMove -= OnPointerMove;
        pointerEventService.Wheel -= OnPointer;
        keyboardService.KeyDown -= OnKeyDown;
        applicationEvents.ViewChanged -= OnActivity;
    }

    void OnPointer(PointerEvent e) => OnActivity();

    // Hovering is not activity, dragging is
    void OnPointerMove(PointerEvent e)
    {
        if (e.Buttons != 0)
            OnActivity();
    }

    void OnKeyDown(KeyPress keyPress) => OnActivity();

    void OnActivity() => Interlocked.Exchange(ref lastActivityTicks, DateTime.UtcNow.Ticks);

    bool IsIdle =>
        DateTime.UtcNow.Ticks - Interlocked.Read(ref lastActivityTicks) >= IdleTime.Ticks
        && !progressService.IsProminentActive
        && !progressService.IsDiscreetActive;

    async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(PollInterval, cancellationToken);
                if (!IsIdle)
                    continue;

                RefineJob? job;
                using (var model = modelMgr.UseModel())
                {
                    job = SelectNext(model) is { } container ? Begin(model, container) : null;
                }
                if (job is null)
                    continue;

                await ComputeAsync(job, cancellationToken);

                bool isApplied;
                using (var model = modelMgr.UseModel())
                {
                    isApplied = TryApply(model, job, attempts);
                }
                if (!isApplied)
                    continue;

                Log.Info($"Refined the layout of {job.ContainerName}");
                applicationEvents.TriggerModelChanged();
                applicationEvents.TriggerUIStateChanged();
                applicationEvents.TriggerSaveNeeded();
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped on dispose
        }
    }

    // Runs the thorough pass a slice at a time, pausing while the user is active.
    async Task ComputeAsync(RefineJob job, CancellationToken cancellationToken)
    {
        job.EvaluateCurrent();
        while (!job.Run.IsComplete)
        {
            if (!IsIdle)
            {
                await Task.Delay(PollInterval, cancellationToken);
                continue;
            }
            job.Run.Step(SliceWork);
            await Task.Delay(1, cancellationToken); // A real yield, also in single-threaded WebAssembly
        }
    }

    // The next container to refine: visible ones first, then the larger on screen, then by name.
    internal static Node? SelectNext(IModel model) =>
        model
            .Nodes.Values.Where(node =>
                node.IsChildrenLayoutRefinePending
                && !node.IsChildrenLayoutCustomized
                && !node.IsChildrenLayoutRequired
                && !IsViewInside(model, node)
            )
            .OrderByDescending(node => IsVisible(model, node))
            .ThenByDescending(CanvasArea)
            .ThenBy(node => node.Name, StringComparer.Ordinal)
            .FirstOrDefault();

    // The container's children show and its box intersects the view.
    static bool IsVisible(IModel model, Node node) =>
        IsChildrenShown(model, node) && ViewRect(model) is { } view && Intersects(CanvasRect(node), view);

    // The view shows only a part of the container: the user is zoomed into it.
    static bool IsViewInside(IModel model, Node node)
    {
        if (!IsChildrenShown(model, node) || ViewRect(model) is not { } view)
            return false;
        var rect = CanvasRect(node);
        return Intersects(rect, view) && (rect.Width > view.Width || rect.Height > view.Height);
    }

    // The root always shows its children; it is a fixed frame, so its content counts as its box.
    static bool IsChildrenShown(IModel model, Node node) =>
        node.IsRoot || (model.Zoom > 0 && NodeViewPolicy.IsChildrenShown(node, model.Zoom));

    // The visible part of the canvas (model.ViewRect holds the offset in canvas units and the
    // size in pixels), or null before the first render.
    static Rect? ViewRect(IModel model)
    {
        var view = model.ViewRect;
        if (model.Zoom <= 0 || view == Rect.None)
            return null;
        return new Rect(view.X, view.Y, view.Width * model.Zoom, view.Height * model.Zoom);
    }

    static Rect CanvasRect(Node node)
    {
        if (node.IsRoot)
            return node.GetTotalBounds();
        var (pos, zoom) = node.GetPosAndZoom();
        return new Rect(pos.X, pos.Y, node.Boundary.Width * zoom, node.Boundary.Height * zoom);
    }

    static bool Intersects(Rect a, Rect b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    static double CanvasArea(Node node)
    {
        var rect = CanvasRect(node);
        return rect.Width * rect.Height;
    }

    // Captures the container (under the model lock): its children keep their current sizes.
    internal static RefineJob? Begin(IModel model, Node container)
    {
        var snapshot = LayoutSnapshot.Capture(
            container,
            NodeLayout.MetricsFor(container),
            child => new Size(child.Boundary.Width, child.Boundary.Height)
        );
        if (snapshot is null || LayeredEngine.Prepare(snapshot.Graph) is not { } plan)
        {
            container.IsChildrenLayoutRefinePending = false;
            return null;
        }

        return new RefineJob(
            container.Id,
            container.LongName,
            model.Path,
            LayoutSnapshot.Fingerprint(container),
            snapshot,
            snapshot.CurrentResult(),
            new ThoroughLayoutRun(plan, NodeLayout.RefineThoroughWorkBudget)
        );
    }

    // Applies the computed layout (under the model lock) if the container is still the one that
    // was captured and the layout reads clearly better. A container changed meanwhile is tried
    // again later, a few times at most.
    internal static bool TryApply(IModel model, RefineJob job, Dictionary<NodeId, int> attempts)
    {
        if (model.Path != job.ModelPath || !model.Nodes.TryGetValue(job.ContainerId, out var container))
            return false;
        if (
            !container.IsChildrenLayoutRefinePending
            || container.IsChildrenLayoutCustomized
            || container.IsChildrenLayoutRequired
        )
        {
            container.IsChildrenLayoutRefinePending = false;
            return false;
        }
        if (IsViewInside(model, container))
            return false; // Tried again when the user is no longer zoomed into it
        if (LayoutSnapshot.Fingerprint(container) != job.Fingerprint)
        {
            var count = attempts[job.ContainerId] = attempts.GetValueOrDefault(job.ContainerId) + 1;
            if (count >= MaxAttempts)
                container.IsChildrenLayoutRefinePending = false;
            return false;
        }

        container.IsChildrenLayoutRefinePending = false;
        attempts.Remove(job.ContainerId);
        if (!job.Run.IsComplete || job.RefinedScore > job.CurrentScore * (1 - MinImprovement))
            return false;

        job.Snapshot.Apply(job.Run.Result);
        if (container.IsRoot)
            NodeLayout.BumpGeneration();
        else
            NodeLayout.FitContainerTransform(container);
        return true;
    }

    // A refinement of one container: what was captured, the running thorough pass, and the
    // scores of the current and the refined layout (evaluated without the model lock).
    internal sealed class RefineJob(
        NodeId containerId,
        string containerName,
        string modelPath,
        int fingerprint,
        LayoutSnapshot snapshot,
        LayoutResult current,
        ThoroughLayoutRun run
    )
    {
        public NodeId ContainerId { get; } = containerId;
        public string ContainerName { get; } = containerName;
        public string ModelPath { get; } = modelPath;
        public int Fingerprint { get; } = fingerprint;
        public LayoutSnapshot Snapshot { get; } = snapshot;
        public ThoroughLayoutRun Run { get; } = run;
        public double CurrentScore { get; private set; } = double.NaN;

        public double RefinedScore =>
            Run.IsComplete ? LayoutQuality.Evaluate(Snapshot.Graph, Run.Result).Score : double.MaxValue;

        public void EvaluateCurrent() => CurrentScore = LayoutQuality.Evaluate(Snapshot.Graph, current).Score;
    }
}
