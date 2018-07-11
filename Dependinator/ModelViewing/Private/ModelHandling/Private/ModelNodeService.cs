using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
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


		public void AddOrUpdateNode(DataNode dataNode, int stamp)
		{
			NodeName nodeName = NodeName.From(dataNode.Name.FullName);

			if (nodeService.TryGetNode(nodeName, out Node node))
			{
				if (node.Stamp != stamp)
				{
					UpdateNode(node, dataNode, stamp);
				}

				return;
			}

			if (dataNode.IsReferenced)
			{
				nodeService.QueueNode(dataNode);
				return;
			}
			
			AddNodeToModel(dataNode, stamp);
		}


		public void RemoveObsoleteNodesAndLinks(int stamp)
		{
			IReadOnlyList<Node> nodes = nodeService.AllNodes.ToList();

			foreach (Node node in nodes)
			{
				if (node.Stamp != stamp && !node.NodeType.IsNamespace() && node.Descendents().All(n => n.Stamp != stamp))
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
				if (node.Stamp != stamp && node.NodeType.IsNamespace() && !node.Children.Any())
				{
					// Node is an empty namespace, lets remove it
					nodeService.RemoveNode(node);
				}
			}
		}


		public void SetLayoutDone()
		{
			nodeService.AllNodes.ForEach(node => node.IsLayoutCompleted = true);
		}


		public IReadOnlyList<NodeName> GetHiddenNodeNames()
			=> nodeService.AllNodes
				.Where(node => node.IsHidden && !node.Parent.IsHidden)
				.Select(node => node.Name)
				.ToList();


		public void HideNode(Node node)
		{
			if (node.IsHidden)
			{
				return;
			}

			node.DescendentsAndSelf().ForEach(n =>
			{
				n.IsHidden = true;
				n.ViewModel.IsFirstShow = true;
			});

			modelLineService.UpdateLines(node);

			node.Parent.ItemsCanvas.UpdateAndNotifyAll(true);
			node.Root.ItemsCanvas.UpdateAll();
		}


		public void RemoveAll() => nodeService.RemoveAll();



		public void ShowHiddenNode(NodeName nodeName)
		{
			if (nodeService.TryGetNode(nodeName, out Node node))
			{
				if (!node.IsHidden)
				{
					return;
				}

				node.DescendentsAndSelf().ForEach(n => n.IsHidden = false);

				modelLineService.UpdateLines(node);

				node.Parent.ItemsCanvas?.UpdateAndNotifyAll(true);
				node.Root.ItemsCanvas.UpdateAll();
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
			NodeName nodeName = NodeName.From(dataNode.Name.FullName);
			Node node = new Node(nodeName)
			{
				Stamp = stamp,
				NodeType = dataNode.NodeType,
				Description = dataNode.Description,
			};

			node.Bounds = dataNode.Bounds;
			node.ScaleFactor = dataNode.Scale;
			node.Color = dataNode.Color;
			node.IsHidden = dataNode.ShowState == Node.Hidden;

			Node parentNode = GetParentNode(nodeName, dataNode);

			nodeService.AddNode(node, parentNode);
		}


		private static void UpdateData(Node node, DataNode dataNode)
		{
			node.Description = dataNode.Description;
		}


		private Node GetParentNode(NodeName nodeName, DataNode dataNode)
		{
			NodeName parentName = GetParentName(nodeName, dataNode);

			return nodeService.GetParentNode(parentName, dataNode.NodeType);
		}


		private static NodeName GetParentName(NodeName nodeName, DataNode dataNode)
		{
			NodeName parentName = dataNode.Parent != null
				? NodeName.From(dataNode.Parent.FullName)
				: nodeName.ParentName;

			return parentName;
		}
	}
}
