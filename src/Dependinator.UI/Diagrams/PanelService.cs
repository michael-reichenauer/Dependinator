namespace Dependinator.UI.Diagrams;

// The panels that share the diagram's top-right corner. Only one is open at a time.
enum Panel
{
    Cycles,
    Rules,
    HiddenNodes,
    Ask,
    Tour,
    PathFinder,
}

// Opens one of the top-right panels and closes the others. The cycles, rules and path panels
// recolor lines, which are baked into the tiles, so closing or opening them redraws the diagram.
interface IPanelService
{
    // Raised for the panel about to open, after the view options' panels are closed. The path
    // finder keeps its own open state and closes itself here (it cannot be asked directly:
    // it uses this service to open).
    event Action<Panel>? Opening;

    // Closes every panel but this one, which the caller then opens.
    void CloseOthers(Panel panel);

    void ToggleCycles();
    void ToggleRules();

    // From the problem indicator: opens the panel unfolded (the cycles, not minimized; the
    // rules, not their suggestions), or leaves it open.
    void ShowCycles();
    void ShowRules();
}

[Scoped]
class PanelService(IViewOptions viewOptions, IApplicationEvents applicationEvents) : IPanelService
{
    public event Action<Panel>? Opening;

    public void CloseOthers(Panel panel)
    {
        bool isTilesChanged = false;
        if (panel != Panel.Cycles && viewOptions.IsCyclesShown)
        {
            viewOptions.SetCyclesShown(false);
            isTilesChanged = true;
        }
        if (panel != Panel.Rules && viewOptions.IsRulesShown)
        {
            viewOptions.SetRulesShown(false);
            isTilesChanged = true;
        }
        if (panel != Panel.HiddenNodes)
            viewOptions.SetHiddenNodesShown(false);
        if (panel != Panel.Ask)
            viewOptions.SetAskShown(false);
        if (panel != Panel.Tour)
            viewOptions.SetTourShown(false);

        Opening?.Invoke(panel);
        if (isTilesChanged)
            applicationEvents.TriggerModelChanged();
    }

    public void ToggleCycles()
    {
        if (!viewOptions.IsCyclesShown)
            CloseOthers(Panel.Cycles);
        viewOptions.SetCyclesShown(!viewOptions.IsCyclesShown);
        applicationEvents.TriggerModelChanged(); // Cyclic lines are drawn differently, baked into the tiles
        applicationEvents.TriggerUIStateChanged();
    }

    public void ToggleRules()
    {
        if (!viewOptions.IsRulesShown)
            CloseOthers(Panel.Rules);
        viewOptions.SetRulesShown(!viewOptions.IsRulesShown);
        applicationEvents.TriggerModelChanged(); // Violating lines are drawn differently, baked into the tiles
        applicationEvents.TriggerUIStateChanged();
    }

    public void ShowCycles()
    {
        if (!viewOptions.IsCyclesShown)
            ToggleCycles();
        else if (viewOptions.IsCyclesMinimized)
        {
            viewOptions.SetCyclesMinimized(false);
            applicationEvents.TriggerUIStateChanged();
        }
    }

    public void ShowRules()
    {
        if (!viewOptions.IsRulesShown)
            ToggleRules();
        else if (viewOptions.IsRuleSuggestionsShown)
        {
            viewOptions.SetRuleSuggestionsShown(false);
            applicationEvents.TriggerUIStateChanged();
        }
    }
}
