using Dependinator.Core.ModelQueries;

namespace Dependinator.Mcp.Tests;

// A question the webview does not answer, or cannot, ends with an error the agent can act on.
public sealed class TimeLimitedModelQueriesTests
{
    readonly Mock<IModelQueryService> queries = new();

    [Fact]
    public async Task GetModelAsync_ShouldReturnAnError_WhenNobodyAnswers()
    {
        queries.Setup(q => q.GetModelAsync()).Returns(new TaskCompletionSource<Result<ModelSummary>>().Task);
        var limited = new TimeLimitedModelQueries(queries.Object, TimeSpan.FromMilliseconds(50));

        var result = await limited.GetModelAsync();

        Assert.Equal(TimeLimitedModelQueries.NoAnswerMessage, Assert.IsType<Error>(result.Value).Message);
    }

    [Fact]
    public async Task GetModelAsync_ShouldNotAsk_WhenTheOtherSideIsNotConnected()
    {
        var limited = new TimeLimitedModelQueries(
            queries.Object,
            TimeSpan.FromMinutes(1),
            () => Task.FromResult(false)
        );

        var result = await limited.GetModelAsync();

        Assert.Equal(TimeLimitedModelQueries.NoAnswerMessage, Assert.IsType<Error>(result.Value).Message);
        queries.Verify(q => q.GetModelAsync(), Times.Never);
    }

    [Fact]
    public async Task GetModelAsync_ShouldReturnTheCause_WhenTheCallFails()
    {
        queries.Setup(q => q.GetModelAsync()).ThrowsAsync(new InvalidOperationException("Broken"));
        var limited = new TimeLimitedModelQueries(queries.Object, TimeSpan.FromMinutes(1));

        var result = await limited.GetModelAsync();

        Assert.Contains("Broken", Assert.IsType<Error>(result.Value).Message);
    }
}
