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

    // Which lines are left out of the diagram (the explorer's own lines and user-requested
    // direct lines are always drawn).
    LineFilter LineFilter { get; }

    // Which kinds of member rows are left out of the diagram (with their lines).
    RowFilter RowFilter { get; }

    // The legend card explaining line styles and node icons (session state).
    bool IsLegendShown { get; }

    // Circular dependencies: the cycles panel is open and cyclic lines are drawn highlighted.
    bool IsCyclesShown { get; }

    // Architecture rules: the rules panel is open and violating lines are drawn highlighted.
    bool IsRulesShown { get; }

    // The minimap in the corner (persisted in Config.ShowMinimap).
    bool IsMinimapShown { get; }

    void SetShowHiddenNodes(bool show);
    void SetIsEditingEnabled(bool enabled);
    void SetDimUnrelatedLines(bool dim);
    void SetLineFilter(LineFilter filter);
    void SetRowFilter(RowFilter filter);
    void SetLegendShown(bool shown);
    void SetCyclesShown(bool shown);
    void SetRulesShown(bool shown);
    void SetMinimapShown(bool shown);
}

// MinLinkCount 1 shows every line; 2 hides the single-link lines, and so on. HideConfiguration
// (lines that only carry values into parameter and variable rows, Bicep) is on by default, so
// IsAny leaves it out: the "(filtered)" menu label only flags filters the user turned on.
readonly record struct LineFilter(
    bool HideExternal,
    bool HideInheritance,
    bool HideMember,
    bool HideConfiguration,
    int MinLinkCount
)
{
    public static readonly LineFilter None = new(false, false, false, false, 1);
    public bool IsAny => HideExternal || HideInheritance || HideMember || MinLinkCount > 1;
}

// Whole kinds of rows left out at once (View › Rows): Bicep parameters, variables and outputs,
// and the setting rows (role assignments, diagnostics, locks...) under the resource they
// configure. A view option like the line filter, not an edit: the rows stay in the model, the
// explorer and search, and their places in the layout stay empty.
readonly record struct RowFilter(bool HideParameters, bool HideVariables, bool HideOutputs, bool HideSettings)
{
    public static readonly RowFilter None = new(false, false, false, false);
    public bool IsAny => HideParameters || HideVariables || HideOutputs || HideSettings;

    public bool Hides(Parsing.NodeType type) =>
        type switch
        {
            Parsing.NodeType.ParameterMember => HideParameters,
            Parsing.NodeType.VariableMember => HideVariables,
            Parsing.NodeType.OutputMember => HideOutputs,
            Parsing.NodeType.ResourceMember => HideSettings,
            _ => false,
        };
}

[Scoped]
class ViewOptions : IViewOptions
{
    public bool ShowHiddenNodes { get; private set; } = true;
    public bool IsEditingEnabled { get; private set; } = false;
    public bool DimUnrelatedLines { get; private set; } = true;
    public LineFilter LineFilter { get; private set; } = LineFilter.None;
    public RowFilter RowFilter { get; private set; } = RowFilter.None;
    public bool IsLegendShown { get; private set; }
    public bool IsCyclesShown { get; private set; }
    public bool IsRulesShown { get; private set; }
    public bool IsMinimapShown { get; private set; }

    public void SetShowHiddenNodes(bool show) => ShowHiddenNodes = show;

    public void SetIsEditingEnabled(bool enabled) => IsEditingEnabled = enabled;

    public void SetDimUnrelatedLines(bool dim) => DimUnrelatedLines = dim;

    public void SetLineFilter(LineFilter filter) => LineFilter = filter;

    public void SetRowFilter(RowFilter filter) => RowFilter = filter;

    public void SetLegendShown(bool shown) => IsLegendShown = shown;

    public void SetCyclesShown(bool shown) => IsCyclesShown = shown;

    public void SetRulesShown(bool shown) => IsRulesShown = shown;

    public void SetMinimapShown(bool shown) => IsMinimapShown = shown;
}
