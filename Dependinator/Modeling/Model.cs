using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;

namespace Dependinator.Modeling
{
	internal class Model
	{
		private readonly Dictionary<NodeName, Node> nodes = new Dictionary<NodeName, Node>();


		public Model(Node root)
		{
			Root = root;
			nodes[root.NodeName] = root;
		}

		public Node Root { get; }

		public IReadOnlyDictionary<NodeName, Node> Nodes => nodes;


		public void AddNode(Node node)
		{
			nodes[node.NodeName] = node;
		}
	}


	internal class Model2
	{
		public Nodes2 Nodes { get; } = new Nodes2();

		public Links2 Links { get; } = new Links2();
	}



	internal class Nodes2
	{
		private readonly Dictionary<NodeId, Node2> allNodes = new Dictionary<NodeId, Node2>();

		private readonly Dictionary<NodeId, List<Node2>> nodesChildren =
			new Dictionary<NodeId, List<Node2>>();


		public Nodes2()
		{
			Root = new RootNode();
			allNodes[Root.Id] = Root;
		}

		//public event EventHandler<NodesEventArgs> NodesAdded;
		public event EventHandler<NodesEventArgs> NodesUpdated;
		public event EventHandler<NodesEventArgs> NodesRemoved;
		public Node2 Root { get; }
		public Node2 Node(NodeId nodeId) => allNodes[nodeId];
		public IReadOnlyList<Node2> Children(NodeId nodeId) => nodesChildren[nodeId];


		public void Add(IReadOnlyList<Node2> nodes)
		{
			foreach (var node in nodes)
			{
				Asserter.Requires(!allNodes.ContainsKey(node.Id));
				allNodes[node.Id] = node;
				AddNodeToParentChildren(node);
			}

			var parentNodes = nodes
				.Select(node => allNodes.TryGetValue(node.ParentId, out Node2 parent) ? parent : null)
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

		public void Remove(IReadOnlyList<Node2> nodes)
		{
			foreach (Node2 node in nodes)
			{
				allNodes.Remove(node.Id);
				nodesChildren[node.Id].Remove(node);				
			}

			var parentNodes = nodes
				.Select(node => allNodes.TryGetValue(node.ParentId, out Node2 parent) ? parent : null)
				.Where(node => node != null)
				.ToList();

			OnNodesRemoved(new NodesEventArgs(nodes));

			if (parentNodes.Any())
			{
				OnNodesUpdated(new NodesEventArgs(parentNodes));
			}
		}


		public void Remove(Node2 node)
		{
			Asserter.Requires(allNodes.ContainsKey(node.Id));

			allNodes.Remove(node.Id);
			nodesChildren[node.ParentId].Remove(node);

			OnNodesRemoved(new NodesEventArgs(node));

			if (allNodes.TryGetValue(node.ParentId, out Node2 parent))
			{
				OnNodesUpdated(new NodesEventArgs(parent));
			}
		}


		protected virtual void OnNodesUpdated(NodesEventArgs e) => NodesUpdated?.Invoke(this, e);

		protected virtual void OnNodesRemoved(NodesEventArgs e) => NodesRemoved?.Invoke(this, e);


		private void AddNodeToParentChildren(Node2 node)
		{
			if (!nodesChildren.TryGetValue(node.ParentId, out List<Node2> parentChildren))
			{
				parentChildren = new List<Node2>();
				nodesChildren[node.ParentId] = parentChildren;
			}

			parentChildren.Add(node);
		}
	}



	internal class NodesEventArgs : EventArgs
	{
		public IReadOnlyList<Node2> Nodes { get; }

		public NodesEventArgs(Node2 node)
		{
			Nodes = new[] { node };
		}

		public NodesEventArgs(IReadOnlyList<Node2> nodes)
		{
			Nodes = nodes;
		}
	}


	internal class Links2
	{
		private readonly Dictionary<LinkId, Link2> links = new Dictionary<LinkId, Link2>();

		private readonly Dictionary<NodeId, List<LinkId>> nodeLinks =
			new Dictionary<NodeId, List<LinkId>>();


		public IEnumerable<Link2> NodeLinks(NodeId nodeId) => nodeLinks[nodeId].Select(Link);

		public Link2 Link(LinkId linkId) => links[linkId];
	}



	//internal class ShowModel2
	//{
	//	private Model2 model;

	//	private readonly Dictionary<LinkId, IReadOnlyList<LinkSegment>> linkSegments =
	//		new Dictionary<LinkId, IReadOnlyList<LinkSegment>>();

	//	private readonly Dictionary<LinkId, IReadOnlyList<LinkLine>> linkLines =
	//		new Dictionary<LinkId, IReadOnlyList<LinkLine>>();


	//	public IReadOnlyDictionary<LinkId, IReadOnlyList<LinkSegment>> LinkSegments => linkSegments;

	//	public IReadOnlyDictionary<LinkId, IReadOnlyList<LinkLine>> LinkLines => linkLines;


	//	public void AddNode(Node node)
	//	{
	//		nodes[node.NodeName] = node;
	//	}
	//}
}