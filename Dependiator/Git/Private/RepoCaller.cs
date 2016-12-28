using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Dependiator.ApplicationHandling;
using Dependiator.Utils;
using LibGit2Sharp;


namespace Dependiator.Git.Private
{
	internal class RepoCaller : IRepoCaller
	{
		private readonly Lazy<WorkingFolder> lazyWorkingFolder;
	


		public RepoCaller(
			Lazy<WorkingFolder> lazyWorkingFolder)
		{
			this.lazyWorkingFolder = lazyWorkingFolder;
		}


		private WorkingFolder workingFolder => lazyWorkingFolder.Value;

		public R UseRepo(
			Action<GitRepository> doAction,
			[CallerMemberName] string memberName = "")
		{
			Log.Debug($"Start {memberName} in {workingFolder} ...");
			try
			{
				using (GitRepository gitRepository = GitRepository.Open(workingFolder))
				{
					doAction(gitRepository);

					Log.Debug($"Done  {memberName} in {workingFolder}");

					return R.Ok;
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to {memberName} in {workingFolder}, {e.Message}");
				return Error.From(e, $"Failed to {memberName} in {workingFolder}");
			}
		}

		public R UseRepo(
			Action<Repository> doAction,
			[CallerMemberName] string memberName = "")
		{
			Log.Debug($"Start {memberName} in {workingFolder} ...");
			try
			{
				using (Repository gitRepository = new Repository(workingFolder))
				{
					doAction(gitRepository);

					Log.Debug($"Done  {memberName} in {workingFolder}");

					return R.Ok;
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to {memberName} in {workingFolder}, {e.Message}");
				return Error.From(e, $"Failed to {memberName} in {workingFolder}");
			}
		}

		public R UseLibRepo(
			Action<Repository> doAction,
			[CallerMemberName] string memberName = "")
		{
			Log.Debug($"Start {memberName} in {workingFolder} ...");
			try
			{
				using (Repository gitRepository = new Repository(workingFolder))
				{
					doAction(gitRepository);

					Log.Debug($"Done  {memberName} in {workingFolder}");

					return R.Ok;
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to {memberName} in {workingFolder}, {e.Message}");
				return Error.From(e, $"Failed to {memberName} in {workingFolder}");
			}
		}


		public Task<R> UseRepoAsync(
			Action<GitRepository> doAction,
			[CallerMemberName] string memberName = "")
		{
			return Task.Run(() => UseRepo(doAction, memberName));
		}


		public Task<R> UseRepoAsync(Action<Repository> doAction, string memberName = "")
		{
			return Task.Run(() => UseRepo(doAction, memberName));
		}


		public Task<R> UseLibRepoAsync( Action<Repository> doAction, string memberName = "")
		{
			return Task.Run(() => UseLibRepo(doAction, memberName));
		}


		public async Task<R> UseRepoAsync(
			TimeSpan timeout,
			Action<GitRepository> doAction,
			[CallerMemberName] string memberName = "")
		{
			CancellationTokenSource cts = new CancellationTokenSource(timeout);

			try
			{
				return await Task.Run(() => UseRepo(doAction, memberName), cts.Token)
					.WithCancellation(cts.Token);
			}
			catch (OperationCanceledException e)
			{
				Log.Warn($"Timeout for {memberName} in {workingFolder}, {e.Message}");
				Error error = Error.From(e, $"Failed to {memberName} in {workingFolder}");
				return error;
			}
		}


		public async Task<R> UseRepoAsync(
			TimeSpan timeout, 
			Action<Repository> doAction, 
			string memberName = "")
		{
			CancellationTokenSource cts = new CancellationTokenSource(timeout);

			try
			{
				return await Task.Run(() => UseRepo(doAction, memberName), cts.Token)
					.WithCancellation(cts.Token);
			}
			catch (OperationCanceledException e)
			{
				Log.Warn($"Timeout for {memberName} in {workingFolder}, {e.Message}");
				Error error = Error.From(e, $"Failed to {memberName} in {workingFolder}");
				return error;
			}
		}


		public R<T> UseRepo<T>(
			Func<GitRepository, T> doFunction,
			[CallerMemberName] string memberName = "")
		{
			Log.Debug($"Start {memberName} in {workingFolder} ...");
			try
			{
				using (GitRepository gitRepository = GitRepository.Open(workingFolder))
				{
					T functionResult = doFunction(gitRepository);

					R<T> result = R.From(functionResult);

					Log.Debug($"Done  {memberName} in {workingFolder}");

					return result;
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to {memberName} in {workingFolder}, {e.Message}");
				return Error.From(e, $"Failed to {memberName} in {workingFolder}");
			}
		}


		public R<T> UseRepo<T>(
			Func<Repository, T> doFunction, 
			string memberName = "")
		{
			Log.Debug($"Start {memberName} in {workingFolder} ...");
			try
			{
				using (Repository gitRepository = new Repository(workingFolder))
				{
					T functionResult = doFunction(gitRepository);

					R<T> result = R.From(functionResult);

					Log.Debug($"Done  {memberName} in {workingFolder}");

					return result;
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to {memberName} in {workingFolder}, {e.Message}");
				return Error.From(e, $"Failed to {memberName} in {workingFolder}");
			}
		}


		public R<T> UseLibRepo<T>(
			Func<Repository, T> doFunction,
			[CallerMemberName] string memberName = "")
		{
			Log.Debug($"Start {memberName} in {workingFolder} ...");
			try
			{
				using (Repository repository = new Repository(workingFolder))
				{
					T functionResult = doFunction(repository);

					R<T> result = R.From(functionResult);

					Log.Debug($"Done  {memberName} in {workingFolder}");

					return result;
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to {memberName} in {workingFolder}, {e.Message}");
				return Error.From(e, $"Failed to {memberName} in {workingFolder}");
			}
		}


		public R UseRepo(
			Func<GitRepository, R> doFunction,
			[CallerMemberName] string memberName = "")
		{
			Log.Debug($"Start {memberName} in {workingFolder} ...");
			try
			{
				using (GitRepository gitRepository = GitRepository.Open(workingFolder))
				{
					R result = doFunction(gitRepository);

					Log.Debug($"Done  {memberName} in {workingFolder}");

					return result;
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to {memberName} in {workingFolder}, {e.Message}");
				return Error.From(e, $"Failed to {memberName} in {workingFolder}");
			}
		}


		public R UseLibRepo(Func<Repository, R> doFunction, string memberName = "")
		{
			Log.Debug($"Start {memberName} in {workingFolder} ...");
			try
			{
				using (Repository gitRepository = new Repository(workingFolder))
				{
					R result = doFunction(gitRepository);

					Log.Debug($"Done  {memberName} in {workingFolder}");

					return result;
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to {memberName} in {workingFolder}, {e.Message}");
				return Error.From(e, $"Failed to {memberName} in {workingFolder}");
			}
		}


		//public Task<R> UseRepoAsync(
		//	string workingFolder,
		//	Func<Repository, R> doFunction,
		//	[CallerMemberName] string memberName = "")
		//{
		//	return Task.Run(() => UseRepo(workingFolder, doFunction, memberName));
		//}

		public Task<R> UseRepoAsync(
			Func<GitRepository, R> doFunction,
			[CallerMemberName] string memberName = "")
		{
			return Task.Run(() => UseRepo(doFunction, memberName));
		}

		public async Task<R> UseRepoAsync(
			TimeSpan timeout,
			Func<GitRepository, R> doFunction,
			[CallerMemberName] string memberName = "")
		{
			CancellationTokenSource cts = new CancellationTokenSource(timeout);

			try
			{
				return await Task.Run(() => UseRepo(doFunction, memberName), cts.Token)
					.WithCancellation(cts.Token);
			}
			catch (OperationCanceledException e)
			{
				Log.Warn($"Timeout for {memberName} in {workingFolder}, {e.Message}");
				Error error = Error.From(e, $"Failed to {memberName} in {workingFolder}");
				return error;
			}
		}


		public async Task<R> UseRepoAsync(
			TimeSpan timeout, 
			Func<Repository, R> doFunction, 
			string memberName = "")
		{
			CancellationTokenSource cts = new CancellationTokenSource(timeout);

			try
			{
				return await Task.Run(() => UseLibRepo(doFunction, memberName), cts.Token)
					.WithCancellation(cts.Token);
			}
			catch (OperationCanceledException e)
			{
				Log.Warn($"Timeout for {memberName} in {workingFolder}, {e.Message}");
				Error error = Error.From(e, $"Failed to {memberName} in {workingFolder}");
				return error;
			}
		}


		public Task<R<T>> UseRepoAsync<T>(
			Func<GitRepository, T> doFunction,
			[CallerMemberName] string memberName = "")
		{
			return Task.Run(() => UseRepo(doFunction, memberName));
		}


		public Task<R<T>> UseLibRepoAsync<T>(
			Func<Repository, T> doFunction, 
			string memberName = "")
		{
			return Task.Run(() => UseLibRepo(doFunction, memberName));
		}


		public Task<R<T>> UseRepoAsync<T>(
			Func<GitRepository, Task<T>> doFunction,
			[CallerMemberName] string memberName = "")
		{
			return Task.Run(async () =>
			{
				Log.Debug($"{memberName} in {workingFolder} ...");
				try
				{
					using (GitRepository gitRepository = GitRepository.Open(workingFolder))
					{
						T functionResult = await doFunction(gitRepository);

						R<T> result = R.From(functionResult);

						Log.Debug($"Done {memberName} in {workingFolder}");

						return result;
					}
				}
				catch (Exception e)
				{
					Log.Warn($"Failed to {memberName} in {workingFolder}, {e.Message}");
					return Error.From(e, $"Failed to {memberName} in {workingFolder}");
				}
			});
		}
	}
}