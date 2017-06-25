using System;
using System.Linq;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeName : Equatable<NodeName>
	{
		private static readonly char[] Separator = ".".ToCharArray();

		public static NodeName Root = new NodeName("");

		private readonly string fullName;
		private readonly Lazy<string> shortName;
		private readonly Lazy<NodeName> parentName;


		public NodeName(string fullName)
		{
			this.fullName = fullName;
			shortName = new Lazy<string>(GetShortName);
			parentName = new Lazy<NodeName>(GetParentName);
			IsEqualWhen(other => fullName == other.fullName, fullName);
		}

		public string ShortName => shortName.Value;

		public NodeName ParentName => parentName.Value;

		public static implicit operator string(NodeName nodeName) => nodeName?.fullName;

		public static implicit operator NodeName(string fullName) => new NodeName(fullName);

		public override string ToString() => fullName;


		public int GetLevelCount() => fullName.Count(c => c == '.') + 1;


		public string GetLevelName(int parts)
		{
			return string.Join(".", fullName.Split(Separator).Take(parts));
		}


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