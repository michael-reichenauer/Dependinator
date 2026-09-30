namespace Dependinator.UI.Diagrams;

// User-toggled diagram view options, shared by the SVG renderers and the interaction/UI
// services. A scoped service (one per user/circuit) rather than a static: a Blazor Server host
// would otherwise share one user's toggles with every connected user (see the e2e suite).
interface IViewOptions
{
    bool ShowHiddenNodes { get; }
    bool IsEditingEnabled { get; }

    void SetShowHiddenNodes(bool show);
    void SetIsEditingEnabled(bool enabled);
}

[Scoped]
class ViewOptions : IViewOptions
{
    public bool ShowHiddenNodes { get; private set; } = true;
    public bool IsEditingEnabled { get; private set; } = false;

    public void SetShowHiddenNodes(bool show) => ShowHiddenNodes = show;

    public void SetIsEditingEnabled(bool enabled) => IsEditingEnabled = enabled;
}
