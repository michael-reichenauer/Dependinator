using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Dependinator.Core.Utils;

// Measure time and log it.
public class Timing : IDisposable
{
    readonly Stopwatch stopwatch;
    readonly string msg;
    readonly string msgMember;
    readonly string msgSourceFilePath;
    readonly int msgSourceLineNumber;

    TimeSpan lastTimeSpan = TimeSpan.Zero;
    int count = 0;

    private Timing(string msg, string memberName, string sourceFilePath, int sourceLineNumber)
    {
        stopwatch = new Stopwatch();
        stopwatch.Start();
        this.msg = msg;
        this.msgMember = memberName;
        this.msgSourceFilePath = sourceFilePath;
        this.msgSourceLineNumber = sourceLineNumber;
    }

    public static Timing Start(
        string msg = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    ) => new Timing(msg, memberName, sourceFilePath, sourceLineNumber);

    public void Dispose()
    {
        var text = msg != "" ? $"{msg} {ToString()}" : $"{msgMember} {ToString()}";
        Logging.Log.Info(text, Logging.Log.StopParameter.Empty, msgMember, msgSourceFilePath, msgSourceLineNumber);
    }

    public TimeSpan Stop()
    {
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    public TimeSpan Elapsed
    {
        get
        {
            lastTimeSpan = stopwatch.Elapsed;
            return lastTimeSpan;
        }
    }

    public long ElapsedMs => (long)Elapsed.TotalMilliseconds;

    public string ElapsedText =>
        ElapsedMs < 1000 ? $"{ElapsedMs}ms"
        : ElapsedMs < 60 * 1000 ? $"{Elapsed.Seconds}s, {Elapsed.Milliseconds}ms"
        : $"{Elapsed.Hours}:{Elapsed.Minutes}:{Elapsed.Seconds}:{Elapsed.Milliseconds}";

    public TimeSpan Diff
    {
        get
        {
            TimeSpan previous = lastTimeSpan;
            return Elapsed - previous;
        }
    }

    public long DiffMs => (long)Diff.TotalMilliseconds;

    public void Log(
        string message,
        StopParameter stopParameter = default(StopParameter),
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        count++;

        Logging.Log.Info(
            $"{count}: {message}: {this}",
            Logging.Log.StopParameter.Empty,
            memberName,
            sourceFilePath,
            sourceLineNumber
        );
    }

    public void Log(
        StopParameter stopParameter = default(StopParameter),
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        count++;

        Logging.Log.Info(
            $"At {count}: {this}",
            Logging.Log.StopParameter.Empty,
            memberName,
            sourceFilePath,
            sourceLineNumber
        );
    }

    public override string ToString() => count == 0 ? $"({ElapsedText})" : $"{DiffMs}ms ({ElapsedText})";

    public struct StopParameter { }
}
