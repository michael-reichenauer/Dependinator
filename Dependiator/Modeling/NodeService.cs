using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.Common.ThemeHandling;
using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal class NodeService : INodeService
	{
		private readonly IItemService itemService;


		public NodeService(IItemService itemService)
		{
			this.itemService = itemService;
		}


		public Model ToModel(DataModel dataModel, ModelViewData modelViewData)
		{
			IEnumerable<Data.Node> nodes = dataModel.Nodes ?? Enumerable.Empty<Data.Node>();
			IEnumerable<Data.Link> links = dataModel.Links ?? Enumerable.Empty<Data.Link>();

			Node root = CreateRootNode();
			Model model = new Model(root);

			foreach (Data.Node node in nodes)
			{
				AddNode(node, null, model, modelViewData);
			}

			foreach (Data.Link link in links)
			{
				AddLink(link, model, modelViewData);
			}

			return model;
		}


		private Node CreateRootNode()
		{
			NodeName rootName = new NodeName(NodeName.Root, NodeName.Root);
			Node root = new Node(itemService, null, rootName, Node.NameSpaceType);
			return root;
		}


		public DataModel ToDataModel(Model model)
		{
			DataModel dataModel = new DataModel();

			dataModel.Nodes = model.Root.ChildNodes
				.Select(ToDataNode)
				.ToList();

			return dataModel;
		}


		public ModelViewData ToViewData(Model model)
		{
			ModelViewData modelViewData = new ModelViewData();

			AddViewData(model.Root, modelViewData);
		
			return modelViewData;
		}


		private static void AddViewData(Node node, ModelViewData modelViewData)
		{
			Data.ViewData nodeViewData = ToViewData(node);

			modelViewData.viewData[node.NodeName.FullName] = nodeViewData;

			foreach (Node childNode in node.ChildNodes)
			{
				AddViewData(childNode, modelViewData);
			}
		}


		private static Data.ViewData ToViewData(Node node)
		{
			Data.ViewData viewData = new Data.ViewData
			{
				Color = Converter.HexFromBrush(node.RectangleBrush),
			};

			if (node.ElementBounds.HasValue)
			{
				viewData.X = node.ElementBounds.Value.X;
				viewData.Y = node.ElementBounds.Value.Y;
				viewData.Width = node.ElementBounds.Value.Width;
				viewData.Height = node.ElementBounds.Value.Height;
			}

			return viewData;
		}


		private void AddNode(
			Data.Node dataNode,
			string parentName,
			Model model,
			ModelViewData modelViewData)
		{
			string fullName = string.IsNullOrEmpty(parentName)
				? dataNode.Name
				: parentName + "." + dataNode.Name;

			Node node = GetOrAddNode(fullName, model, modelViewData);

			node.SetType(dataNode.Type);

			if (dataNode.ViewData != null)
			{
				node.ElementBounds = ToBounds(dataNode.ViewData);
				//element.ElementBrush = Converter.BrushFromHex(dataNode.ViewData.Color);
			}
			else
			{
				if (modelViewData != null 
					&& modelViewData.viewData.TryGetValue(fullName, out Data.ViewData viewData))
				{
					node.ElementBounds = ToBounds(viewData);
					//element.ElementBrush = Converter.BrushFromHex(viewData.Color);
				}
			}


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
					NodeLink reference = new NodeLink(node, targetNode);
					node.NodeLinks.Add(reference);
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


		private void AddLink(Data.Link link, Model model, ModelViewData modelViewData)
		{
			Node sourceNode = GetOrAddNode(link.Source, model, modelViewData);
			Node targetNode = GetOrAddNode(link.Target, model, modelViewData);

			NodeLink nodeLink = new NodeLink(sourceNode, targetNode);
			sourceNode.NodeLinks.Add(nodeLink);
		}


		private Data.Node ToDataNode(Node node)
		{
			Data.Node dataNode = new Data.Node
			{
				Name = node.NodeName.Name,
				Type = node.NodeType,
				Nodes = ToChildren(node.ChildNodes),
				Links = ToLinks(node),
				ViewData = ToViewData(node)
			};

			return dataNode;
		}


		private static List<Data.Link> ToLinks(Node node)
		{
			if (!node.NodeLinks.Any())
			{
				return null;
			}

			List<Data.Link> links = null;

			foreach (Link link in node.NodeLinks)
			{
				foreach (NodeLink nodeLink in link.NodeLinks.Where(n => n.Source == node))
				{
					if (links == null)
					{
						links = new List<Data.Link>();
					}

					Data.Link dataLink = new Data.Link { Target = nodeLink.Target.NodeName.FullName };
					links.Add(dataLink);
				}
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


		private Node GetOrAddNode(string name, Model model, ModelViewData modelViewData)
		{
			if (!model.Nodes.TryGetValue(name, out Node node))
			{
				node = CreateNode(name, model, modelViewData);
			}

			return node;
		}


		private Node CreateNode(string name, Model model, ModelViewData modelViewData)
		{
			string parentName = GetParentName(name);

			if (!model.Nodes.TryGetValue(parentName, out Node parentNode))
			{
				parentNode = CreateNode(parentName, model, modelViewData);
			}

			string shortName = GetNamePart(name);
			NodeName nodeName = new NodeName(shortName, name);
			Node node = new Node(itemService, parentNode, nodeName, null);

			if (modelViewData != null && modelViewData.viewData.TryGetValue(
				name, out Data.ViewData viewData))
			{
				node.ElementBounds = ToBounds(viewData);
				//node.ElementBrush = Converter.BrushFromHex(viewData.Color);
			}

			parentNode.AddChild(node);
			model.AddNode(node);

			return node;
		}


		private static string GetParentName(string fullName)
		{
			int index = fullName.LastIndexOf('.');

			if (index == -1)
			{
				// root namespace
				return "";
			}

			return fullName.Substring(0, index);
		}


		private static string GetNamePart(string fullName)
		{
			int index = fullName.LastIndexOf('.');

			if (index == -1)
			{
				// root namespace
				return fullName;
			}

			return fullName.Substring(index + 1);
		}
	}
}