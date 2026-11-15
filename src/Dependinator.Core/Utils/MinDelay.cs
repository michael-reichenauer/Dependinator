using System.Diagnostics;

namespace Dependinator.Core.Utils;

/// <summary>
/// Ensures a minimum elapsed time using await using.
/// On DisposeAsync, delays for any remaining time to reach the target duration, and at least
/// minimum (default none): a minimum above zero always yields, which lets the single-threaded
/// browser app paint and take input between the steps of a loop.
/// </summary>
public readonly struct MinDelay(TimeSpan target, TimeSpan minimum = default) : IAsyncDisposable
{
    readonly Stopwatch sw = Stopwatch.StartNew();

    public async ValueTask DisposeAsync()
    {
        var remaining = target - sw.Elapsed;
        if (remaining < minimum)
            remaining = minimum;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }
    }
}
