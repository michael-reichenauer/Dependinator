using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Dependencies;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

// The first-run tour: it starts once (persisted as seen when ended), walks welcome → zoom →
// select → explore → search → done, and each task step completes by itself when the user does
// the thing, counting only what happens during the step.
public class CoachServiceTests
{
    readonly Mock<IApplicationEvents> applicationEvents = new();
    readonly Mock<IConfigService> configService = new();
    readonly Mock<ISelectionService> selectionService = new();
    readonly Mock<IDependenciesService> dependenciesService = new();
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly Config config = new();

    public CoachServiceTests()
    {
        configService.Setup(c => c.GetAsync()).ReturnsAsync(config);
        configService
            .Setup(c => c.SetAsync(It.IsAny<Action<Config>>()))
            .Callback<Action<Config>>(update => update(config))
            .Returns(Task.CompletedTask);
        selectionService.Setup(s => s.SelectedId).Returns(PointerId.Empty);
        modelMgr.WithModel(m => m.Zoom = 1);
    }

    CoachService CreateService() =>
        new(
            applicationEvents.Object,
            configService.Object,
            selectionService.Object,
            dependenciesService.Object,
            modelMgr
        );

    void Select(string id)
    {
        selectionService.Setup(s => s.IsSelected).Returns(true);
        selectionService.Setup(s => s.SelectedId).Returns(PointerId.Parse(id));
        applicationEvents.Raise(e => e.UIStateChanged += null);
    }

    void SetZoom(double zoom)
    {
        modelMgr.WithModel(m => m.Zoom = zoom);
        applicationEvents.Raise(e => e.ViewChanged += null);
    }

    [Fact]
    public async Task StartIfFirstRun_ShouldStartOnce_AndDismissMarksItSeen()
    {
        var service = CreateService();

        await service.StartIfFirstRunAsync(isDemoModel: true);
        Assert.True(service.IsActive);
        Assert.Equal(CoachStep.Welcome, service.Step);
        Assert.Contains("demo diagram", service.Card.Text);
        Assert.Equal(0, service.TaskNumber);

        await service.DismissAsync();
        Assert.False(service.IsActive);
        Assert.True(config.IsCoachSeen);

        await service.StartIfFirstRunAsync(isDemoModel: true);
        Assert.False(service.IsActive);
    }

    [Fact]
    public async Task Steps_ShouldCompleteBythemselves_WhenTheUserDoesTheThing()
    {
        var service = CreateService();
        await service.StartIfFirstRunAsync(isDemoModel: false);
        Assert.DoesNotContain("demo diagram", service.Card.Text);

        service.Next();
        Assert.Equal(CoachStep.Zoom, service.Step);
        Assert.Equal(1, service.TaskNumber);

        SetZoom(1.1); // Too small a change to count
        Assert.Equal(CoachStep.Zoom, service.Step);
        SetZoom(2);
        Assert.Equal(CoachStep.Select, service.Step);

        Select("node-A");
        Assert.Equal(CoachStep.Explore, service.Step);

        dependenciesService.Setup(d => d.IsShowExplorer).Returns(true);
        applicationEvents.Raise(e => e.UIStateChanged += null);
        Assert.Equal(CoachStep.Search, service.Step);
        Assert.Equal(4, service.TaskNumber);

        service.NoticeSearchOpened();
        Assert.Equal(CoachStep.Done, service.Step);
        Assert.Equal("Done", service.Card.NextLabel);

        service.Next();
        Assert.False(service.IsActive);
        Assert.True(config.IsCoachSeen);
    }

    [Fact]
    public async Task SelectStep_ShouldIgnoreASelectionMadeBeforeTheStep()
    {
        var service = CreateService();
        Select("node-A"); // Selected before the tour even starts
        await service.StartIfFirstRunAsync(isDemoModel: true);
        service.Next();
        SetZoom(3);
        Assert.Equal(CoachStep.Select, service.Step);

        applicationEvents.Raise(e => e.UIStateChanged += null); // Still the old selection
        Assert.Equal(CoachStep.Select, service.Step);

        Select("node-B");
        Assert.Equal(CoachStep.Explore, service.Step);
    }

    [Fact]
    public void Start_ShouldRestartTheTour_EvenWhenSeen()
    {
        config.IsCoachSeen = true;
        var service = CreateService();

        service.Start();

        Assert.True(service.IsActive);
        Assert.Equal(CoachStep.Welcome, service.Step);
    }
}
