using System.Diagnostics;

namespace Dependinator.Utils;

public static class Util
{
    public static string CallStack(int take)
    {
        var stackTrace = new StackTrace(1, true);
        return string.Join("\n",
            stackTrace.GetFrames()!
            .Take(take)
            .Select(f => $"  {f.GetMethod()!.DeclaringType!.Name}.{f.GetMethod()!.Name}:{f.GetFileLineNumber()}"));

    }

    public static void Trigger(Action action)
    {
        Task.Delay(1).ContinueWith(_ => action()).RunInBackground();
    }

    public static void Trigger<T>(Func<Task<T>> action)
    {
        Task.Delay(1).ContinueWith(_ => action().RunInBackground()).RunInBackground();
    }
}
