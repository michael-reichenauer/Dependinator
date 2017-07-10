using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.Modeling.Private.Analyzing;
using Dependinator.Modeling.Private.Serializing;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;

namespace Dependinator.Modeling.Private
{
	internal class ModelingService : IModelingService
	{
		private readonly Lazy<IItemsService> modelService;
		private readonly INodeService nodeService;
		private readonly ILinkService linkService;
		private readonly IReflectionService reflectionService;
		private readonly IDataSerializer dataSerializer;


		public ModelingService(
			Lazy<IItemsService> modelService,
			INodeService nodeService,
			ILinkService linkService,
			IReflectionService reflectionService,
			IDataSerializer dataSerializer)
		{
			this.modelService = modelService;
			this.nodeService = nodeService;
			this.linkService = linkService;
			this.reflectionService = reflectionService;
			this.dataSerializer = dataSerializer;
		}


		public ModelOld Analyze(string path, ModelViewDataOld modelViewData)
		{
			Data.Model dataModel = reflectionService.Analyze(path);
			return ToModel(dataModel, modelViewData);
		}


		public ModelViewDataOld ToViewData(ModelOld model)
		{
			ModelViewDataOld modelViewData = new ModelViewDataOld();

			AddViewData(model.Root, modelViewData);

			return modelViewData;
		}



		public void Serialize(ModelOld model, string path)
		{
			Data.Model dataModel = ToDataModel(model);

			dataSerializer.Serialize(dataModel, path);
		}


		public bool TryDeserialize(string path, out ModelOld model)
		{
			if (dataSerializer.TryDeserialize(path, out Data.Model dataModel))
			{
				model = ToModel(dataModel, null);
				return true;
			}

			model = null;
			return false;
		}



		private ModelOld ToModel(Data.Model dataModel, ModelViewDataOld modelViewData)
		{
			IEnumerable<Data.Node> nodes = dataModel.Nodes ?? Enumerable.Empty<Data.Node>();
			IEnumerable<Data.Link> links = dataModel.Links ?? Enumerable.Empty<Data.Link>();

			NodeOld root = CreateRootNode();
			ModelOld model = new ModelOld(root);

			Timing t = Timing.Start();
			foreach (Data.Node node in nodes)
			{
				AddNode(node, null, model, modelViewData);
			}
			t.Log("Added nodes");

			foreach (Data.Link link in links)
			{
				AddLink(link, model, modelViewData);
			}
			t.Log("Added links");

			return model;
		}


		private NodeOld CreateRootNode()
		{
			NodeOld root = new NodeOld(modelService.Value, nodeService, linkService, null, NodeName.Root, NodeType.NameSpaceType);
			return root;
		}


		private Data.Model ToDataModel(ModelOld model)
		{
			Data.Model dataModel = new Data.Model();

			dataModel.Nodes = model.Root.ChildNodes
				.Select(ToDataNode)
				.ToList();

			return dataModel;
		}


		private static void AddViewData(NodeOld node, ModelViewDataOld modelViewData)
		{
			Data.ViewData nodeViewData = ToViewData(node);

			modelViewData.viewData[node.NodeName] = nodeViewData;

			foreach (NodeOld childNode in node.ChildNodes)
			{
				AddViewData(childNode, modelViewData);
			}
		}


		private static Data.ViewData ToViewData(NodeOld node)
		{
			if (node.NodeName.ShortName == "GitMind")
			{

			}
			Data.ViewData viewData = new Data.ViewData
			{
				Color = node.PersistentNodeColor,
				X = node.NodeBounds.X,
				Y = node.NodeBounds.Y,
				Width = node.NodeBounds.Width,
				Height = node.NodeBounds.Height,
				Scale = node.ItemsScale,
				OffsetX = node.ItemsOffset.X,
				OffsetY = node.ItemsOffset.Y
			};

			return viewData;
		}


		private void AddNode(
			Data.Node dataNode,
			NodeName parentName,
			ModelOld model,
			ModelViewDataOld modelViewData)
		{
			NodeName fullName = string.IsNullOrEmpty(parentName)
				? dataNode.Name
				: parentName + "." + dataNode.Name;

			NodeOld node = GetOrAddNode(fullName, model, modelViewData);

			node.SetType(dataNode.Type);

			TrySetViewData(dataNode.ViewData, modelViewData, node, fullName);


			if (dataNode.Nodes != null)
			{
				foreach (Data.Node childDataNode in dataNode.Nodes)
				{
					AddNode(childDataNode, fullName, model, modelViewData);
				}
			}

			if (dataNode.Links != null)
			{
				foreach (Data.Link dataLink in dataNode.Links)
				{
					NodeOld targetNode = GetOrAddNode(dataLink.Target, model, modelViewData);
					LinkOld link = new LinkOld(node, targetNode);
					node.Links.Add(link);
				}
			}
		}


		private static Rect? ToBounds(Data.ViewData viewData)
		{
			if (viewData == null || viewData.Width == 0)
			{
				return null;
			}

			return new Rect(
				new Point(viewData.X, viewData.Y),
				new Size(viewData.Width, viewData.Height));
		}


		private void AddLink(Data.Link dataLink, ModelOld model, ModelViewDataOld modelViewData)
		{
			NodeOld sourceNode = GetOrAddNode(dataLink.Source, model, modelViewData);
			NodeOld targetNode = GetOrAddNode(dataLink.Target, model, modelViewData);

			LinkOld link = new LinkOld(sourceNode, targetNode);
			sourceNode.Links.Add(link);
		}


		private Data.Node ToDataNode(NodeOld node)
		{
			Data.Node dataNode = new Data.Node
			{
				Name = node.NodeName.ShortName,
				Type = node.NodeType,
				Nodes = ToChildren(node.ChildNodes),
				Links = ToDataLinks(node),
				ViewData = ToViewData(node)
			};

			return dataNode;
		}


		private static List<Data.Link> ToDataLinks(NodeOld node)
		{
			List<Data.Link> links = null;

			foreach (LinkOld link in node.Links.Links)
			{
				Data.Link dataLink = new Data.Link { Target = link.Target.NodeName };

				if (links == null)
				{
					links = new List<Data.Link>();
				}

				links.Add(dataLink);
			}

			return links;
		}


		private List<Data.Node> ToChildren(IEnumerable<NodeOld> nodeChildren)
		{
			if (!nodeChildren.Any())
			{
				return null;
			}

			return nodeChildren
				.Select(ToDataNode)
				.ToList();
		}


		private NodeOld GetOrAddNode(NodeName nodeName, ModelOld model, ModelViewDataOld modelViewData)
		{
			if (!model.Nodes.TryGetValue(nodeName, out NodeOld node))
			{
				node = CreateNode(nodeName, model, modelViewData);
			}

			return node;
		}


		private NodeOld CreateNode(NodeName nodeName, ModelOld model, ModelViewDataOld modelViewData)
		{
			NodeName parentName = nodeName.ParentName;

			if (!model.Nodes.TryGetValue(parentName, out NodeOld parentNode))
			{
				parentNode = CreateNode(parentName, model, modelViewData);
			}

			NodeOld node = new NodeOld(modelService.Value, nodeService, linkService, parentNode, nodeName, null);

			TrySetViewData(null, modelViewData, node, nodeName);

			parentNode.AddChild(node);
			model.AddNode(node);

			return node;
		}


		private static void TrySetViewData(
			Data.ViewData viewData,
			ModelViewDataOld modelViewData,
			NodeOld node,
			NodeName fullName)
		{
			if (viewData != null)
			{
				SetViewData(node, viewData);
			}
			else
			{
				if (modelViewData != null
						&& modelViewData.viewData.TryGetValue(fullName, out viewData))
				{
					SetViewData(node, viewData);
				}
			}
		}


		private static void SetViewData(NodeOld node, Data.ViewData viewData)
		{
			node.PersistentNodeBounds = ToBounds(viewData);
			node.PersistentScale = viewData.Scale;
			node.PersistentNodeColor = viewData.Color;
			node.PersistentOffset = new Point(viewData.OffsetX, viewData.OffsetY);
		}



	}
}