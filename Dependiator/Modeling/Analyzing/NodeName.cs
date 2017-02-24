using System;
using Dependiator.Utils;


namespace Dependiator.Modeling.Analyzing
{
	internal class NodeName : Equatable<NodeName>
	{
		public static NodeName Root = new NodeName("");

		private readonly string fullName;
		private readonly Lazy<string> shortName;
		private readonly Lazy<NodeName> parentName;


		public NodeName(string fullName)
		{
			this.fullName = fullName;
			shortName = new Lazy<string>(GetShortName);
			parentName = new Lazy<NodeName>(GetParentName);
		}

		public string ShortName => shortName.Value;

		public NodeName ParentName => parentName.Value;

		public static implicit operator string(NodeName nodeName) => nodeName?.fullName;

		public static implicit operator NodeName(string fullName) => new NodeName(fullName);

		public override string ToString() => fullName;

		protected override bool IsEqual(NodeName other) => fullName == other.fullName;

		protected override int GetHash() => fullName?.GetHashCode() ?? 0;


		private string GetShortName()
		{
			int index = fullName.LastIndexOf('.');

			if (index == -1)
			{
				// No parent
				return fullName;
			}

			return fullName.Substring(index + 1);
		}


		private NodeName GetParentName()
		{
			int index = fullName.LastIndexOf('.');

			if (index == -1)
			{
				// root namespace
				return Root;
			}

			return fullName.Substring(0, index);
		}
	}
}