using Dependiator.Common;


namespace Dependiator.Git
{
	internal class GitDivergence
	{
		public GitDivergence(
			CommitSha localId,
			CommitSha remoteId,
			CommitSha commonId,
			int aheadBy,
			int behindBy)
		{
			LocalId = localId;
			RemoteId = remoteId;
			CommonId = commonId;
			AheadBy = aheadBy;
			BehindBy = behindBy;
		}

		public CommitSha LocalId { get; }
		public CommitSha RemoteId { get; }
		public CommitSha CommonId { get; }
		public int AheadBy { get; }
		public int BehindBy { get; }
	}
}