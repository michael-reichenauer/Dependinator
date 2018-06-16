using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.ModelDataHandling;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal class ModelNodeService : IModelNodeService
	{
		private readonly IModelLinkService modelLinkService;
		private readonly IModelLineService modelLineService;
		private readonly INodeService nodeService;


		public ModelNodeService(
			INodeService nodeService,
			IModelLinkService modelLinkService,
			IModelLineService modelLineService)
		{
			this.nodeService = nodeService;
			this.modelLinkService = modelLinkService;
			this.modelLineService = modelLineService;
		}


		public void UpdateNode(DataNode dataNode, int stamp)
		{
			if (dataNode.IsReferenced)
			{
				nodeService.QueueNode(dataNode);
				return;
			}

			if (nodeService.TryGetNode(dataNode.Id, out Node node))
			{
				UpdateNode(node, dataNode, stamp);
				return;
			}

			AddNodeToModel(dataNode, stamp);
		}


		public void RemoveObsoleteNodesAndLinks(int stamp)
		{
			IReadOnlyList<Node> nodes = nodeService.AllNodes.ToList();

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


		public void HideNode(Node node)
		{
			if (node.View.IsHidden)
			{
				return;
			}

			node.DescendentsAndSelf().ForEach(n =>
			{
				n.View.IsHidden = true;
				n.View.ViewModel.IsFirstShow = true;
			});

			modelLineService.UpdateLines(node);

			node.Parent.View.ItemsCanvas.UpdateAndNotifyAll();
			node.Root.View.ItemsCanvas.UpdateAll();
		}



		public void ShowHiddenNode(NodeName nodeName)
		{
			if (nodeService.TryGetNode(new NodeId(nodeName), out Node node))
			{
				if (!node.View.IsHidden)
				{
					return;
				}

				node.DescendentsAndSelf().ForEach(n => n.View.IsHidden = false);

				modelLineService.UpdateLines(node);

				node.Parent.View.ItemsCanvas?.UpdateAndNotifyAll();
				node.Root.View.ItemsCanvas.UpdateAll();
			}
		}



		private void UpdateNode(Node node, DataNode dataNode, int stamp)
		{
			node.Stamp = stamp;

			UpdateData(node, dataNode);

			nodeService.UpdateNodeTypeIfNeeded(node, dataNode.NodeType);
		}


		private void AddNodeToModel(DataNode dataNode, int stamp)
		{
			Node node = new Node(dataNode.Id, dataNode.Name)
			{
				Stamp = stamp,
				NodeType = dataNode.NodeType,
				Description = dataNode.Description,
				CodeText = dataNode.CodeText,
			};

			node.View.Bounds = dataNode.Bounds;
			node.View.ScaleFactor = dataNode.ItemsScaleFactor;
			node.View.Color = dataNode.Color;
			node.View.IsHidden = dataNode.ShowState == Node.Hidden;

			Node parentNode = GetParentNode(dataNode.Name, dataNode);

			nodeService.AddNode(node, parentNode);
		}


		private static void UpdateData(Node node, DataNode dataNode)
		{
			node.Description = dataNode.Description;
			node.CodeText = dataNode.CodeText;
		}


		private Node GetParentNode(NodeName nodeName, DataNode dataNode)
		{
			NodeName parentName = GetParentName(nodeName, dataNode);

			return nodeService.GetParentNode(parentName, dataNode.NodeType);
		}



		private static NodeName GetParentName(NodeName nodeName, DataNode dataNode)
		{
			NodeName parentName = dataNode.Parent != null
				? NodeName.From(dataNode.Parent)
				: nodeName.ParentName;

			return parentName;
		}
	}
}
