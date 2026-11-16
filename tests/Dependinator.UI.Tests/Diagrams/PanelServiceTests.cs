using Dependinator.UI.Diagrams;
using Dependinator.UI.Shared;
using Moq;

namespace Dependinator.UI.Tests.Diagrams;

public class PanelServiceTests
{
    readonly ViewOptions viewOptions = new();
    readonly Mock<IApplicationEvents> applicationEvents = new();
    readonly PanelService service;

    public PanelServiceTests()
    {
        service = new PanelService(viewOptions, applicationEvents.Object);
    }

    [Fact]
    public void CloseOthers_ShouldCloseEveryOtherPanel_AndTellThePathFinder()
    {
        viewOptions.SetRulesShown(true);
        viewOptions.SetHiddenNodesShown(true);
        viewOptions.SetAskShown(true);
        Panel? opening = null;
        service.Opening += panel => opening = panel;

        service.CloseOthers(Panel.Cycles);

        Assert.False(viewOptions.IsRulesShown);
        Assert.False(viewOptions.IsHiddenNodesShown);
        Assert.False(viewOptions.IsAskShown);
        Assert.Equal(Panel.Cycles, opening);
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Once); // Rule lines are in the tiles
    }

    [Fact]
    public void CloseOthers_ShouldKeepThePanelItOpens_AndLeaveTheTiles_WhenNoLinesChange()
    {
        viewOptions.SetHiddenNodesShown(true);
        viewOptions.SetTourShown(true);

        service.CloseOthers(Panel.HiddenNodes);

        Assert.True(viewOptions.IsHiddenNodesShown);
        Assert.False(viewOptions.IsTourShown);
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Never);
    }

    [Fact]
    public void ToggleRules_ShouldCloseTheCycles_AndCloseAgain()
    {
        viewOptions.SetCyclesShown(true);

        service.ToggleRules();
        Assert.True(viewOptions.IsRulesShown);
        Assert.False(viewOptions.IsCyclesShown);

        service.ToggleRules();
        Assert.False(viewOptions.IsRulesShown);
    }

    [Fact]
    public void ShowCycles_ShouldUnfoldAMinimizedPanel_AndKeepItOpen()
    {
        service.ShowCycles();
        viewOptions.SetCyclesMinimized(true);

        service.ShowCycles();

        Assert.True(viewOptions.IsCyclesShown);
        Assert.False(viewOptions.IsCyclesMinimized);
    }

    [Fact]
    public void ShowRules_ShouldLeaveTheSuggestions_ForTheRules()
    {
        service.ShowRules();
        viewOptions.SetRuleSuggestionsShown(true);

        service.ShowRules();

        Assert.True(viewOptions.IsRulesShown);
        Assert.False(viewOptions.IsRuleSuggestionsShown);
    }
}
