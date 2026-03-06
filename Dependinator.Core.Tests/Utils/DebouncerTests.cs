using System.Diagnostics;

namespace Dependinator.Core.Tests.Utils;

public class DebouncerTests
{
    [Fact]
    public async Task Debounce_ShouldInvokeLatestAction()
    {
        using Debouncer debouncer = new();
        TaskCompletionSource<string> invokedAction = new(TaskCreationOptions.RunContinuationsAsynchronously);

        debouncer.Debounce(TimeSpan.FromMilliseconds(50), () => invokedAction.TrySetResult("first"));
        await Task.Delay(10);
        debouncer.Debounce(TimeSpan.FromMilliseconds(50), () => invokedAction.TrySetResult("second"));

        string result = await invokedAction.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("second", result);
    }

    [Fact]
    public async Task Debounce_WithMaximumDelay_ShouldInvokeEvenWhenCallsContinue()
    {
        using Debouncer debouncer = new();
        TaskCompletionSource<TimeSpan> invokedAt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < TimeSpan.FromMilliseconds(500) && !invokedAt.Task.IsCompleted)
        {
            debouncer.Debounce(
                TimeSpan.FromMilliseconds(150),
                TimeSpan.FromMilliseconds(400),
                () => invokedAt.TrySetResult(stopwatch.Elapsed)
            );

            await Task.Delay(75);
        }

        TimeSpan elapsed = await invokedAt.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.InRange(elapsed, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500));
    }
}
