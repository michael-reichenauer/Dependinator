using Dependinator.Utils;

namespace Dependinator.Modeling
{
	internal class DataNode : Equatable<DataNode>
	{
		public DataNode(string name, string nodeType)
		{
			Name = name;
			NodeType = nodeType;

			IsEqualWhenSame(Name);
		}

		public string Name { get; }
		public string NodeType { get; }

		public override string ToString() => Name;
	}
}