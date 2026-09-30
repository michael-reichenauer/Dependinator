namespace Dependinator.UI.Diagrams;

// User-toggled diagram view options, shared by the SVG renderers and the interaction/UI
// services. A scoped service (one per user/circuit) rather than a static: a Blazor Server host
// would otherwise share one user's toggles with every connected user (see the e2e suite).
interface IViewOptions
{
    bool ShowHiddenNodes { get; }
    bool IsEditingEnabled { get; }

    // While a node or line is selected, lines not touching it are drawn faded so the
    // selection's own lines stand out (a hovered line shows at full strength).
    bool DimUnrelatedLines { get; }

    void SetShowHiddenNodes(bool show);
    void SetIsEditingEnabled(bool enabled);
    void SetDimUnrelatedLines(bool dim);
}

[Scoped]
class ViewOptions : IViewOptions
{
    public bool ShowHiddenNodes { get; private set; } = true;
    public bool IsEditingEnabled { get; private set; } = false;
    public bool DimUnrelatedLines { get; private set; } = true;

    public void SetShowHiddenNodes(bool show) => ShowHiddenNodes = show;

    public void SetIsEditingEnabled(bool enabled) => IsEditingEnabled = enabled;

    public void SetDimUnrelatedLines(bool dim) => DimUnrelatedLines = dim;
}
