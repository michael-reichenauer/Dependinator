using System.Diagnostics;
using System.Reflection;

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

    [Fact]
    public async Task Debounce_ShouldIgnoreStaleTimerCallbackAfterReschedule()
    {
        using Debouncer debouncer = new();
        TaskCompletionSource<string> invokedAction = new(TaskCreationOptions.RunContinuationsAsynchronously);

        debouncer.Debounce(TimeSpan.FromMilliseconds(200), () => invokedAction.TrySetResult("first"));
        await Task.Delay(50);
        debouncer.Debounce(TimeSpan.FromMilliseconds(200), () => invokedAction.TrySetResult("second"));

        InvokeTimerCallback(debouncer);

        Assert.False(invokedAction.Task.IsCompleted);

        string result = await invokedAction.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("second", result);
    }

    [Fact]
    public async Task Debounce_ShouldIgnoreStaleTimerCallbackFromPreviousCycle()
    {
        using Debouncer debouncer = new();
        TaskCompletionSource firstInvocation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondInvocation = new(TaskCreationOptions.RunContinuationsAsynchronously);

        debouncer.Debounce(TimeSpan.Zero, () => firstInvocation.TrySetResult());
        await firstInvocation.Task.WaitAsync(TimeSpan.FromSeconds(1));

        debouncer.Debounce(TimeSpan.FromMilliseconds(200), () => secondInvocation.TrySetResult());

        InvokeTimerCallback(debouncer);

        Assert.False(secondInvocation.Task.IsCompleted);

        await secondInvocation.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    static void InvokeTimerCallback(Debouncer debouncer)
    {
        MethodInfo? invokeAction = typeof(Debouncer).GetMethod("InvokeAction", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(invokeAction);
        invokeAction.Invoke(debouncer, null);
    }
}
