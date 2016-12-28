using System.Runtime.Serialization;
using Dependiator.Utils;


namespace Dependiator.Common
{

	[DataContract]
	public class CommitId : Equatable<CommitId>
	{
		public static readonly CommitId Uncommitted = new CommitId(CommitSha.Uncommitted);
		public static readonly CommitId None = new CommitId(CommitSha.None);


		public CommitId()
		{
		}


		public CommitId(string commitSha)
			: this()
		{
			Id = commitSha.Substring(0, 6);
		}

		public CommitId(CommitSha commitSha)
			: this(commitSha.Sha)
		{
		}


		[DataMember]
		public string Id { get; private set; }

		protected override bool IsEqual(CommitId other) => Id == other.Id;

		protected override int GetHash() => Id.GetHashCode();

		public override string ToString() => Id;
	}
}
