using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;
using Dependinator.Utils.Threading;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal class ModelNodeService : IModelNodeService
	{
		private readonly IModelLinkService modelLinkService;
		private readonly ILinkSegmentService linkSegmentService;
		private readonly INodeService nodeService;


		public ModelNodeService(
			INodeService nodeService,
			IModelLinkService modelLinkService,
			ILinkSegmentService linkSegmentService)
		{
			this.nodeService = nodeService;
			this.modelLinkService = modelLinkService;
			this.linkSegmentService = linkSegmentService;
		}


		public void UpdateNode(ModelNode modelNode, int stamp)
		{
			NodeName name = NodeName.From(modelNode.Name);

			if (nodeService.TryGetNode(name, out Node node))
			{
				UpdateNode(node, modelNode, stamp);
				return;
			}

			AddNodeToModel(name, modelNode, stamp);
		}


		public void RemoveObsoleteNodesAndLinks(int stamp)
		{
			IReadOnlyList<Node> nodes = nodeService.AllNodes.ToList();
			Log.Warn("Testing ----------------");

			nodeService.TryGetNode(NodeName.Root, out Node root);

			Timing t = Timing.Start();

			int count = root.Descendents().Count();
			t.Log($"Nodes ref = {count}");

			count = Descendents2(root.Name).Count();
			t.Log($"Nodes dict = {count}");

			int linkCount = Descendents2(root.Name).SelectMany(n => n.SourceLinks).Count();
			t.Log($"Links {linkCount}");

			int lineCount = Descendents2(root.Name).SelectMany(n => n.SourceLines).Count();
			t.Log($"Lines {lineCount}");

			int segmentCount = Descendents2(root.Name)
				.SelectMany(n => n.SourceLinks)
				.SelectMany(l => linkSegmentService.GetLinkSegments(l))
				.Count();
			t.Log($"segments {segmentCount}");

			foreach (Node node in nodes)
			{
				if (node.Stamp != stamp && node.NodeType != NodeType.NameSpace && node.Descendents().All(n => n.Stamp != stamp))
				{
					List<Link> obsoleteLinks = node.SourceLinks.ToList();
					modelLinkService.RemoveObsoleteLinks(obsoleteLinks);
					nodeService.RemoveNode(node);
				}
				else
				{
					List<Link> obsoleteLinks = node.SourceLinks.Where(link => link.Stamp != stamp).ToList();
					modelLinkService.RemoveObsoleteLinks(obsoleteLinks);
				}
			}

			foreach (Node node in nodes.Reverse())
			{
				if (node.Stamp != stamp && node.NodeType == NodeType.NameSpace && !node.Children.Any())
				{
					// Node is an empty namespace, lets remove it
					nodeService.RemoveNode(node);
				}
			}
		}


		private IEnumerable<Node> Descendents2(NodeName nodeName)
		{
			Queue<Node> queue = new Queue<Node>();

			Node node = GetNode(nodeName);
			node.Children.Select(c => GetNode(c.Name)).ForEach(queue.Enqueue);

			while (queue.Any())
			{
				Node descendent = queue.Dequeue();
				yield return descendent;

				descendent.Children.Select(c => GetNode(c.Name)).ForEach(queue.Enqueue);
			}
		}


		private Node GetNode(NodeName nodeName)
		{
			nodeService.TryGetNode(nodeName, out Node node);
			return node;
		}


		public void SetLayoutDone()
		{
			nodeService.AllNodes.ForEach(node => node.View.IsLayoutCompleted = true);
		}


		public void RemoveAll() => nodeService.RemoveAll();


		public IReadOnlyList<NodeName> GetHiddenNodeNames()
			=> nodeService.AllNodes
				.Where(node => node.View.IsHidden && !node.Parent.View.IsHidden)
				.Select(node => node.Name)
				.ToList();


		public void ShowHiddenNode(NodeName nodeName)
		{
			if (nodeService.TryGetNode(nodeName, out Node node))
			{
				ShowHiddenNode(node);
				node.Parent.View.ItemsCanvas?.UpdateAndNotifyAll();
				node.Root.View.ItemsCanvas.UpdateAll();
			}
		}


		private void ShowHiddenNode(Node node)
		{
			node.DescendentsAndSelf().ForEach(n => n.View.IsHidden = false);
		}


		private void UpdateNode(Node node, ModelNode modelNode, int stamp)
		{
			node.Stamp = stamp;

			UpdateData(node, modelNode);

			nodeService.UpdateNodeTypeIfNeeded(node, modelNode.NodeType);
		}


		private void AddNodeToModel(NodeName name, ModelNode modelNode, int stamp)
		{
			Node node = new Node(name)
			{
				Stamp = stamp,
				NodeType = modelNode.NodeType,
				Description = modelNode.Description,
				CodeText = modelNode.CodeText,
			};

			node.View.Bounds = modelNode.Bounds;
			node.View.ScaleFactor = modelNode.ItemsScaleFactor;
			node.View.Color = modelNode.Color;
			node.View.IsHidden = modelNode.ShowState == Node.Hidden;

			Node parentNode = GetParentNode(name, modelNode);

			nodeService.AddNode(node, parentNode);
		}


		private static void UpdateData(Node node, ModelNode modelNode)
		{
			node.Description = modelNode.Description;
			node.CodeText = modelNode.CodeText;
		}


		private Node GetParentNode(NodeName nodeName, ModelNode modelNode)
		{
			NodeName parentName = GetParentName(nodeName, modelNode);

			return nodeService.GetParentNode(parentName, modelNode.NodeType);
		}



		private static NodeName GetParentName(NodeName nodeName, ModelNode modelNode)
		{
			NodeName parentName = modelNode.Parent != null
				? NodeName.From(modelNode.Parent)
				: nodeName.ParentName;

			return parentName;
		}
	}
}
