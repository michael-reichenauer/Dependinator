using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal class NodeService : INodeService
	{
		private readonly INodeItemService nodeItemService;



		public NodeService(INodeItemService nodeItemService)
		{
			this.nodeItemService = nodeItemService;
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
			Node root = new Node(nodeItemService, null, NodeName.Root, NodeType.NameSpaceType);
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

			modelViewData.viewData[node.NodeName] = nodeViewData;

			foreach (Node childNode in node.ChildNodes)
			{
				AddViewData(childNode, modelViewData);
			}
		}


		private static Data.ViewData ToViewData(Node node)
		{
			Data.ViewData viewData = new Data.ViewData
			{
				Color = node.NodeColor,
			};

			if (node.NodeBounds.HasValue)
			{
				viewData.X = node.NodeBounds.Value.X;
				viewData.Y = node.NodeBounds.Value.Y;
				viewData.Width = node.NodeBounds.Value.Width;
				viewData.Height = node.NodeBounds.Value.Height;
			}

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

			if (dataNode.ViewData != null)
			{
				node.NodeBounds = ToBounds(dataNode.ViewData);
				//element.ElementBrush = Converter.BrushFromHex(dataNode.ViewData.Color);
			}
			else
			{
				if (modelViewData != null 
					&& modelViewData.viewData.TryGetValue(fullName, out Data.ViewData viewData))
				{
					node.NodeBounds = ToBounds(viewData);
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

			Node node = new Node(nodeItemService, parentNode, nodeName, null);

			if (modelViewData != null && modelViewData.viewData.TryGetValue(
				nodeName, out Data.ViewData viewData))
			{
				node.NodeBounds = ToBounds(viewData);
				//node.ElementBrush = Converter.BrushFromHex(viewData.Color);
			}

			parentNode.AddChild(node);
			model.AddNode(node);

			return node;
		}

	}
}