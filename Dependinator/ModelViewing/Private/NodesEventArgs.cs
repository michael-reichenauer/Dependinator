using System;
using System.Collections.Generic;
using Dependinator.Modeling;

namespace Dependinator.ModelViewing.Private
{
	internal class NodesEventArgs : EventArgs
	{
		public IReadOnlyList<Node> Nodes { get; }

		public NodesEventArgs(Node node)
		{
			Nodes = new[] { node };
		}

		public NodesEventArgs(IReadOnlyList<Node> nodes)
		{
			Nodes = nodes;
		}
	}
}