using System;
using System.Runtime.Serialization;
using Dependinator.Utils;


namespace Dependinator.Common
{
	[DataContract]
	public class CommitSha : Equatable<CommitSha>
	{
		public static readonly CommitSha Uncommitted = new CommitSha(new string('0', 40));
		public static readonly CommitSha None = new CommitSha(new string('1', 40));

		private readonly Lazy<string> shortSha;


		public CommitSha()
		{
			shortSha = new Lazy<string>(() => Sha.Substring(0, 6));
			IsEqualWhen(Sha);
		}

		public CommitSha(string commitSha)
			:this()
		{
			Sha = commitSha;	
		}

		[DataMember]
		public string Sha { get; private set; }

		public string ShortSha => shortSha.Value;

		public override string ToString() => Sha;
	}
}