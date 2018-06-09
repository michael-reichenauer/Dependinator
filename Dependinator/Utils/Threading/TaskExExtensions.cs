// ReSharper disable once CheckNamespace
namespace System.Threading.Tasks
{
	internal static class TaskExExtensions
	{
		public static event EventHandler<FatalExceptionEventArgs> FatalException;

		/// <summary>
		/// Provides a workaround for async functions that have no built-in cancellation support.
		/// This functions should only be used as a last resort. It does not cancel the original, call
		/// it only provides cancellation support for the caller.
		/// </summary>
		public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken ct)
		{
			var tcs = new TaskCompletionSource<bool>();
			using (ct.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
			{
				if (task != await Task.WhenAny(task, tcs.Task))
				{
					throw new OperationCanceledException(ct);
				}
			}

			return await task;
		}


		/// <summary>
		/// Provides a workaround for async functions that have no built-in cancellation support.
		/// This functions should only be used as a last resort. It does not cancel the original, call
		/// it only provides cancellation support for the caller.
		/// </summary>
		public static async Task WithCancellation(this Task task, CancellationToken ct)
		{
			var tcs = new TaskCompletionSource<bool>();
			using (ct.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
			{
				if (task != await Task.WhenAny(task, tcs.Task))
				{
					throw new OperationCanceledException(ct);
				}
			}

			await task;
		}

		public static void RunInBackground(this Task task)
		{
			task.ContinueWith(
				LogFailedTask,
				TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted);
		}


		private static void LogFailedTask(Task task)
		{
			FatalException?.Invoke(null, new FatalExceptionEventArgs(
				"RunInBackground task failed", task.Exception));
		}
	}
}
