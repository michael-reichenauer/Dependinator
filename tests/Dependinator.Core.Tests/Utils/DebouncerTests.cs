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

        // Spam Debounce far faster than the 300ms debounce delay so the normal debounce
        // never fires; only the 600ms maximum delay should trigger the action. The large
        // gap between the call interval (50ms) and the debounce delay (300ms) keeps the
        // test robust against Task.Delay jitter on loaded CI runners.
        while (stopwatch.Elapsed < TimeSpan.FromMilliseconds(1500) && !invokedAt.Task.IsCompleted)
        {
            debouncer.Debounce(
                TimeSpan.FromMilliseconds(300),
                TimeSpan.FromMilliseconds(600),
                () => invokedAt.TrySetResult(stopwatch.Elapsed)
            );

            await Task.Delay(50);
        }

        TimeSpan elapsed = await invokedAt.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.InRange(elapsed, TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(1500));
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
        MethodInfo? invokeAction = typeof(Debouncer).GetMethod(
            "InvokeAction",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.NotNull(invokeAction);
        invokeAction.Invoke(debouncer, null);
    }
}
