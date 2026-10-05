using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Shared;

// The prominent (blocking) progress can be sent to the background: it then shows as the
// discreet spinner until the work ends, and the next prominent work is blocking again.
public class ProgressServiceTests
{
    readonly ProgressService service = new(new Mock<IApplicationEvents>().Object);

    [Fact]
    public void ContinueInBackground_ShouldTurnProminentIntoDiscreet_UntilItEnds()
    {
        using (service.Start("Parsing"))
        {
            Assert.True(service.IsProminentActive);
            Assert.False(service.IsDiscreetActive);
            Assert.NotNull(service.ProminentStartedUtc);

            service.ContinueInBackground();

            Assert.False(service.IsProminentActive);
            Assert.True(service.IsDiscreetActive);
        }

        Assert.False(service.IsProminentActive);
        Assert.False(service.IsDiscreetActive);
        Assert.Null(service.ProminentStartedUtc);

        using (service.Start("Parsing again"))
        {
            Assert.True(service.IsProminentActive);
        }
    }

    [Fact]
    public void ContinueInBackground_ShouldDoNothing_WithoutProminentProgress()
    {
        using (service.StartDiscreet())
        {
            service.ContinueInBackground();
            Assert.True(service.IsDiscreetActive);
            Assert.False(service.IsProminentActive);
        }
    }
}
