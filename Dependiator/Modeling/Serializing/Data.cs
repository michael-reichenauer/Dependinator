using System.Collections.Generic;


namespace Dependiator.Modeling.Serializing
{
	internal class Data
	{
		public Dictionary<string, DataNode> NodesByName { get; } = new Dictionary<string, DataNode>();
		public List<DataNode> Nodes { get; set; } = new List<DataNode>();

		public override string ToString() => $"{Nodes?.Count ?? 0} nodes";
	}
}