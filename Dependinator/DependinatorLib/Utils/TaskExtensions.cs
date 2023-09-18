// ReSharper disable once CheckNamespace
namespace System.Threading.Tasks;

public static class TaskExtensions
{

    // Provides a workaround for async functions that have no built-in cancellation support.
    // This functions should only be used as a last resort. It does not cancel the original, call
    // it only provides cancellation support for the caller.

    public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (ct.Register(s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                throw new OperationCanceledException(ct);
            }
        }

        return await task;
    }


    // Provides a workaround for async functions that have no built-in cancellation support.
    // This functions should only be used as a last resort. It does not cancel the original, call
    // it only provides cancellation support for the caller.
    public static async Task WithCancellation(this Task task, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (ct.Register(s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                throw new OperationCanceledException(ct);
            }
        }

        await task;
    }

    // RunInBackground ignores the return value of the task and logs any exceptions.
    // Useful for tasks that should just be started and results are ignored.
    public static void RunInBackground(this Task task)
    {
        task.ContinueWith(
            FailedBackgroundTask,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted);
    }


    private static void FailedBackgroundTask(Task task)
    {
        var e = new InvalidOperationException("RunInBackground task failed", task.Exception);
        ExceptionHandling.OnBackgroundTaskException(e);
    }
}

