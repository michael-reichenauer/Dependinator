using System.Collections.Generic;
using System.Linq;
using Dependinator.Common;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal class ModelNodeService : IModelNodeService
	{
		private readonly IModelLinkService modelLinkService;
		private readonly INodeService nodeService;


		public ModelNodeService(
			INodeService nodeService,
			IModelLinkService modelLinkService)
		{
			this.nodeService = nodeService;
			this.modelLinkService = modelLinkService;
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

			foreach (Node node in nodes)
			{
				if (node.Stamp != stamp && node.NodeType != NodeType.NameSpace)
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
				.Where(node => node.View.IsHidden)
				.Select(node => node.Name)
				.ToList();


		public void ShowHiddenNode(NodeName nodeName)
		{
			if (nodeService.TryGetNode(nodeName, out Node node))
			{
				node?.View.ShowHiddenNode();
			}
		}


		private void UpdateNode(Node node, ModelNode modelNode, int stamp)
		{
			node.Stamp = stamp;

			UpdateDescriptionIfNeeded(node, modelNode);

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

		
		private static void UpdateDescriptionIfNeeded(Node node, ModelNode modelNode)
		{
			if (!string.IsNullOrEmpty(modelNode.Description)
					&& node.Description != modelNode.Description)
			{
				node.Description = modelNode.Description;
				node.CodeText = modelNode.CodeText;
			}
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
