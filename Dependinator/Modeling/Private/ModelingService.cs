﻿using System;
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


		public Model Analyze(string path, ModelViewData modelViewData)
		{
			Data.Model dataModel = reflectionService.Analyze(path);
			return ToModel(dataModel, modelViewData);
		}


		public ModelViewData ToViewData(Model model)
		{
			ModelViewData modelViewData = new ModelViewData();

			AddViewData(model.Root, modelViewData);

			return modelViewData;
		}



		public void Serialize(Model model, string path)
		{
			Data.Model dataModel = ToDataModel(model);

			dataSerializer.Serialize(dataModel, path);
		}


		public bool TryDeserialize(string path, out Model model)
		{
			if (dataSerializer.TryDeserialize(path, out Data.Model dataModel))
			{
				model = ToModel(dataModel, null);
				return true;
			}

			model = null;
			return false;
		}



		private Model ToModel(Data.Model dataModel, ModelViewData modelViewData)
		{
			IEnumerable<Data.Node> nodes = dataModel.Nodes ?? Enumerable.Empty<Data.Node>();
			IEnumerable<Data.Link> links = dataModel.Links ?? Enumerable.Empty<Data.Link>();

			Node root = CreateRootNode();
			Model model = new Model(root);

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


		private Node CreateRootNode()
		{
			Node root = new Node(modelService.Value, nodeService, linkService, null, NodeName.Root, NodeType.NameSpaceType);
			return root;
		}


		private Data.Model ToDataModel(Model model)
		{
			Data.Model dataModel = new Data.Model();

			dataModel.Nodes = model.Root.ChildNodes
				.Select(ToDataNode)
				.ToList();

			return dataModel;
		}


		private static void AddViewData(Node node, ModelViewData modelViewData)
		{
			Data.ViewData nodeViewData = ToViewData(node);

			modelViewData.viewData[node.NodeName] = nodeViewData;

			foreach (Node childNode in node.ChildNodes)
			{
				AddViewData(childNode, modelViewData);
			}
		}


		private static Data.ViewData ToViewData(Node node)
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
			Model model,
			ModelViewData modelViewData)
		{
			NodeName fullName = string.IsNullOrEmpty(parentName)
				? dataNode.Name
				: parentName + "." + dataNode.Name;

			Node node = GetOrAddNode(fullName, model, modelViewData);

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
					Node targetNode = GetOrAddNode(dataLink.Target, model, modelViewData);
					Link link = new Link(node, targetNode);
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


		private void AddLink(Data.Link dataLink, Model model, ModelViewData modelViewData)
		{
			Node sourceNode = GetOrAddNode(dataLink.Source, model, modelViewData);
			Node targetNode = GetOrAddNode(dataLink.Target, model, modelViewData);

			Link link = new Link(sourceNode, targetNode);
			sourceNode.Links.Add(link);
		}


		private Data.Node ToDataNode(Node node)
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


		private static List<Data.Link> ToDataLinks(Node node)
		{
			List<Data.Link> links = null;

			foreach (Link link in node.Links.Links)
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


		private List<Data.Node> ToChildren(IEnumerable<Node> nodeChildren)
		{
			if (!nodeChildren.Any())
			{
				return null;
			}

			return nodeChildren
				.Select(ToDataNode)
				.ToList();
		}


		private Node GetOrAddNode(NodeName nodeName, Model model, ModelViewData modelViewData)
		{
			if (!model.Nodes.TryGetValue(nodeName, out Node node))
			{
				node = CreateNode(nodeName, model, modelViewData);
			}

			return node;
		}


		private Node CreateNode(NodeName nodeName, Model model, ModelViewData modelViewData)
		{
			NodeName parentName = nodeName.ParentName;

			if (!model.Nodes.TryGetValue(parentName, out Node parentNode))
			{
				parentNode = CreateNode(parentName, model, modelViewData);
			}

			Node node = new Node(modelService.Value, nodeService, linkService, parentNode, nodeName, null);

			TrySetViewData(null, modelViewData, node, nodeName);

			parentNode.AddChild(node);
			model.AddNode(node);

			return node;
		}


		private static void TrySetViewData(
			Data.ViewData viewData,
			ModelViewData modelViewData,
			Node node,
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


		private static void SetViewData(Node node, Data.ViewData viewData)
		{
			node.PersistentNodeBounds = ToBounds(viewData);
			node.PersistentScale = viewData.Scale;
			node.PersistentNodeColor = viewData.Color;
			node.PersistentOffset = new Point(viewData.OffsetX, viewData.OffsetY);
		}



	}
}