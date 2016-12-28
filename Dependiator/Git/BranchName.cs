﻿using System;
using Dependiator.Utils;


namespace Dependiator.Git
{
	/// <summary>
	/// Branch name type, which handles branch names as case insensitive strings.
	/// </summary>
	public class BranchName : Equatable<BranchName>, IComparable
	{
		public static BranchName Master = new BranchName("master");
		public static BranchName OriginHead = new BranchName("origin/HEAD");
		public static BranchName Head = new BranchName("HEAD");

		private readonly string name;
		private readonly string caseInsensitiveName;
		private readonly int hashCode;


		public BranchName(string name)
		{
			this.name = name;
			caseInsensitiveName = name?.ToLower();
			hashCode = caseInsensitiveName?.GetHashCode() ?? 0;
		}


		protected override int GetHash() => hashCode;

		protected override bool IsEqual(BranchName other) => 
			caseInsensitiveName == other.caseInsensitiveName;

		public bool IsEqual(string other) =>
			0 == string.Compare(caseInsensitiveName, other, StringComparison.OrdinalIgnoreCase);


		public bool StartsWith(string prefix) =>
			caseInsensitiveName?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ?? false;

		public BranchName Substring(int startIndex) => new BranchName(name.Substring(startIndex));

		public static implicit operator string(BranchName branchName) => branchName?.name;

		public static implicit operator BranchName(string branchName) =>
			branchName != null ? new BranchName(branchName) : null;


		public int CompareTo(object obj)
		{
			if (obj == null) return 1;

			BranchName other = obj as BranchName;
			if (other != null)
			{
				return string.Compare(
					caseInsensitiveName, other.caseInsensitiveName, StringComparison.Ordinal);
			}
			else
			{
				throw new ArgumentException("Object is not a BranchName");
			}
		}

		public override string ToString() => name;
	}
}