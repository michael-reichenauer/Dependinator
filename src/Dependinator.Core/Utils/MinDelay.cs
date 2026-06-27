using System.Diagnostics;

namespace Dependinator.Core.Utils;

/// <summary>
/// Ensures a minimum elapsed time using await using.
/// On DisposeAsync, delays for any remaining time to reach the target duration.
/// </summary>
public readonly struct MinDelay(TimeSpan target) : IAsyncDisposable
{
    readonly Stopwatch sw = Stopwatch.StartNew();

    public async ValueTask DisposeAsync()
    {
        var remaining = target - sw.Elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }
    }
}
