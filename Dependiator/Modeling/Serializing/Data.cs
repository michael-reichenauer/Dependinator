using System.Collections.Generic;


namespace Dependiator.Modeling.Serializing
{
	internal class Data
	{
		public List<DataNode> Nodes { get; set; }

		public override string ToString() => $"{Nodes?.Count ?? 0} nodes";
	}
}