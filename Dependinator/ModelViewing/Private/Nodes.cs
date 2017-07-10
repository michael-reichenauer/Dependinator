using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Modeling;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Private
{
	internal class Nodes
	{
		private readonly Dictionary<NodeId, Node> allNodes = new Dictionary<NodeId, Node>();

		private readonly Dictionary<NodeId, List<Node>> nodesChildren =
			new Dictionary<NodeId, List<Node>>();


		public Nodes()
		{
			Root = new RootNode();
			allNodes[Root.Id] = Root;
		}

		//public event EventHandler<NodesEventArgs> NodesAdded;
		public event EventHandler<NodesEventArgs> NodesUpdated;
		public event EventHandler<NodesEventArgs> NodesRemoved;
		public Node Root { get; }
		public Node Node(NodeId nodeId) => allNodes[nodeId];
		public IReadOnlyList<Node> Children(NodeId nodeId) => nodesChildren[nodeId];


		public void Add(IReadOnlyList<Node> nodes)
		{
			foreach (var node in nodes)
			{
				Asserter.Requires(!allNodes.ContainsKey(node.Id));
				allNodes[node.Id] = node;
				AddNodeToParentChildren(node);
			}

			var parentNodes = nodes
				.Select(node => allNodes.TryGetValue(node.ParentId, out Node parent) ? parent : null)
				.Where(node => node != null);

			var updatedNodes = nodes
				.Concat(parentNodes)
				.Distinct()
				.ToList();

			if (updatedNodes.Any())
			{
				OnNodesUpdated(new NodesEventArgs(updatedNodes));
			}
		}

		public void Remove(IReadOnlyList<Node> nodes)
		{
			foreach (Node node in nodes)
			{
				allNodes.Remove(node.Id);
				nodesChildren[node.Id].Remove(node);				
			}

			var parentNodes = nodes
				.Select(node => allNodes.TryGetValue(node.ParentId, out Node parent) ? parent : null)
				.Where(node => node != null)
				.ToList();

			OnNodesRemoved(new NodesEventArgs(nodes));

			if (parentNodes.Any())
			{
				OnNodesUpdated(new NodesEventArgs(parentNodes));
			}
		}


		public void Remove(Node node)
		{
			Asserter.Requires(allNodes.ContainsKey(node.Id));

			allNodes.Remove(node.Id);
			nodesChildren[node.ParentId].Remove(node);

			OnNodesRemoved(new NodesEventArgs(node));

			if (allNodes.TryGetValue(node.ParentId, out Node parent))
			{
				OnNodesUpdated(new NodesEventArgs(parent));
			}
		}


		protected virtual void OnNodesUpdated(NodesEventArgs e) => NodesUpdated?.Invoke(this, e);

		protected virtual void OnNodesRemoved(NodesEventArgs e) => NodesRemoved?.Invoke(this, e);


		private void AddNodeToParentChildren(Node node)
		{
			if (!nodesChildren.TryGetValue(node.ParentId, out List<Node> parentChildren))
			{
				parentChildren = new List<Node>();
				nodesChildren[node.ParentId] = parentChildren;
			}

			parentChildren.Add(node);
		}
	}
}