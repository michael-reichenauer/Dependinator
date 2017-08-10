﻿using System.Runtime.Serialization;
using Dependinator.Utils;


namespace Dependinator.Common
{

	[DataContract]
	public class CommitId : Equatable<CommitId>
	{
		public static readonly CommitId Uncommitted = new CommitId(CommitSha.Uncommitted);
		public static readonly CommitId None = new CommitId(CommitSha.None);


		public CommitId()
		{
			IsEqualWhenSame(Id);
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

		public override string ToString() => Id;
	}
}
