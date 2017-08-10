using System;
using System.Collections.Generic;
using Dependinator.Modeling;

namespace Dependinator.ModelViewing.Private
{
	internal class NodesEventArgs : EventArgs
	{
		public IReadOnlyList<DataNode> Nodes { get; }

		public NodesEventArgs(DataNode node)
		{
			Nodes = new[] { node };
		}

		public NodesEventArgs(IReadOnlyList<DataNode> nodes)
		{
			Nodes = nodes;
		}
	}
}