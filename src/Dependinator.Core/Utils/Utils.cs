using System.Diagnostics;
using System.Runtime.CompilerServices;

// General-purpose utility helpers used across the core: the Result (R/R<T>) error-handling
// types, async and threading helpers, common extension methods, timing/logging aids, and
// dependency-injection support.
namespace Dependinator.Core.Utils;

public static class Util
{
    public static string CallStack(int take)
    {
        var stackTrace = new StackTrace(1, true);
        return string.Join(
            "\n",
            stackTrace
                .GetFrames()!
                .Take(take)
                .Select(f => $"  {f.GetMethod()!.DeclaringType!.Name}.{f.GetMethod()!.Name}:{f.GetFileLineNumber()}")
        );
    }

    public static void Trigger(Action action)
    {
        Task.Delay(1).ContinueWith(_ => action()).RunInBackground();
    }

    public static void Trigger<T>(Func<Task<T>> action)
    {
        Task.Delay(1).ContinueWith(_ => action().RunInBackground()).RunInBackground();
    }

    public static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
