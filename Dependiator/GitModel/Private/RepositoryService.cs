using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependiator.Common;
using Dependiator.Git;
using Dependiator.RepositoryViews;
using Dependiator.Utils;


namespace Dependiator.GitModel.Private
{
	[SingleInstance]
	internal class RepositoryService : IRepositoryService, IRepositoryMgr
	{

		private readonly ICacheService cacheService;
		private readonly ICommitsFiles commitsFiles;



		public RepositoryService(
			ICacheService cacheService,
			ICommitsFiles commitsFiles)
		{
			this.cacheService = cacheService;
			this.commitsFiles = commitsFiles;
		}

		public Repository Repository { get; private set; }



		public bool IsRepositoryCached(string workingFolder)
		{
			return cacheService.IsRepositoryCached(workingFolder);
		}


		public async Task LoadRepositoryAsync(string workingFolder)
		{
			R<Repository> repository = await GetCachedRepositoryAsync(workingFolder);
			

			Repository = repository.Value;			
		}


		private async Task<R<Repository>> GetCachedRepositoryAsync(string workingFolder)
		{
			try
			{
				Timing t = new Timing();
				MRepository mRepository = await cacheService.TryGetRepositoryAsync(workingFolder);

				if (mRepository != null)
				{
					t.Log("Read from cache");
					mRepository.IsCached = true;
					Repository repository = ToRepository(mRepository);
					int branchesCount = repository.Branches.Count;
					int commitsCount = repository.Commits.Count;
					t.Log($"Repository {branchesCount} branches, {commitsCount} commits");
					return repository;
				}

				return R<Repository>.NoValue;
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to read cached repository {e}");		
				return e;
			}

		}



		private Repository ToRepository(MRepository mRepository)
		{
			Timing t = new Timing();
			KeyedList<string, Branch> rBranches = new KeyedList<string, Branch>(b => b.Id);
			Dictionary<CommitId, Commit> rCommits = new Dictionary<CommitId, Commit>();
			Branch currentBranch = null;
			Commit currentCommit = null;
			MCommit rootCommit = mRepository.Branches
				.First(b => b.Value.Name == BranchName.Master && b.Value.IsActive)
				.Value.FirstCommit;

			Repository repository = new Repository(
				mRepository,
				new Lazy<IReadOnlyKeyedList<string, Branch>>(() => rBranches),
				new Lazy<IReadOnlyDictionary<CommitId, Commit>>(() => rCommits),
				new Lazy<Branch>(() => currentBranch),
				new Lazy<Commit>(() => currentCommit),
				commitsFiles,
				rootCommit.Id,
				mRepository.Uncommitted?.Id ?? CommitId.None);

			foreach (var mCommit in mRepository.Commits.Values)
			{
				Commit commit = Converter.ToCommit(repository, mCommit);
				rCommits[commit.Id] = commit;
				if (mCommit == mRepository.CurrentCommit)
				{
					currentCommit = commit;
				}
			}

			foreach (var mBranch in mRepository.Branches)
			{
				Branch branch = Converter.ToBranch(repository, mBranch.Value);
				rBranches.Add(branch);

				if (mBranch.Value == mRepository.CurrentBranch)
				{
					currentBranch = branch;
				}
			}

			t.Log($"Created repository {repository.Commits.Count} commits");
			return repository;
		}
	}
}