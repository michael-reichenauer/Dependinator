using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.Common.ThemeHandling;
using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal class ElementService : IElementService
	{
		private readonly IItemService itemService;


		public ElementService(IItemService itemService)
		{
			this.itemService = itemService;
		}


		public ElementTree ToElementTree(Data.Model data, ModelViewData modelViewData)
		{
			IEnumerable<Data.Node> nodes = data.Nodes ?? Enumerable.Empty<Data.Node>();
			IEnumerable<Data.Link> links = data.Links ?? Enumerable.Empty<Data.Link>();

			NodeName rootName = new NodeName(NodeName.Root, NodeName.Root);
			Node root = new Node(itemService, null, rootName, Node.NameSpaceType);

			Dictionary<string, Node> elements = new Dictionary<string, Node>();
			elements[root.NodeName.FullName] = root;

			foreach (Data.Node node in nodes)
			{
				AddElement(node, null, elements, modelViewData);
			}

			foreach (Data.Link link in links)
			{
				AddLink(link, elements, modelViewData);
			}

			ElementTree tree = new ElementTree(root);
			return tree;
		}


		public Data.Model ToData(ElementTree elementTree)
		{
			List<Data.Node> nodes = elementTree.Root.ChildNodeItems
				.Select(ToNode)
				.ToList();

			Data.Model model = new Data.Model();
			model.Nodes = nodes;

			return model;
		}


		public ModelViewData ToViewData(ElementTree elementTree)
		{
			ModelViewData modelViewData = new ModelViewData();

			foreach (Node child in elementTree.Root.ChildNodeItems)
			{
				AddViewData(child, modelViewData);
			}

			return modelViewData;
		}


		private void AddViewData(Node element, ModelViewData modelViewData)
		{
			Data.ViewData nodeViewData = ToViewData(element);

			modelViewData.viewDataByName[element.NodeName.FullName] = nodeViewData;
			
			foreach (Node child in element.ChildNodeItems)
			{
				AddViewData(child, modelViewData);
			}
		}


		private static Data.ViewData ToViewData(Node element)
		{
			Data.ViewData viewData = new Data.ViewData
			{
				Color = Converter.HexFromBrush(element.RectangleBrush),
			};

			if (element.ElementBounds.HasValue)
			{
				viewData.X = element.ElementBounds.Value.X;
				viewData.Y = element.ElementBounds.Value.Y;
				viewData.Width = element.ElementBounds.Value.Width;
				viewData.Height = element.ElementBounds.Value.Height;
			}


			return viewData;
		}


		private void AddElement(
			Data.Node dataNode,
			string parentName,
			Dictionary<string, Node> elements,
			ModelViewData modelViewData)
		{
			string fullName = string.IsNullOrEmpty(parentName)
				? dataNode.Name
				: parentName + "." + dataNode.Name;

			Node element = GetOrAddElement(fullName, elements, modelViewData);
			element.SetType(dataNode.Type);

			if (dataNode.ViewData != null)
			{
				element.ElementBounds = ToBounds(dataNode.ViewData);
				//element.ElementBrush = Converter.BrushFromHex(dataNode.ViewData.Color);
			}
			else
			{
				if (modelViewData != null && modelViewData.viewDataByName.TryGetValue(
					fullName, out Data.ViewData viewData))
				{
					element.ElementBounds = ToBounds(viewData);
					//element.ElementBrush = Converter.BrushFromHex(viewData.Color);
				}
			}


			if (dataNode.Nodes != null)
			{
				foreach (Data.Node childNode in dataNode.Nodes)
				{
					AddElement(childNode, fullName, elements, modelViewData);
				}
			}

			if (dataNode.Links != null)
			{
				foreach (Data.Link dataLink in dataNode.Links)
				{
					Node targetElement = GetOrAddElement(dataLink.Target, elements, modelViewData);
					NodeLink reference = new NodeLink(element, targetElement, LinkKind.Direkt);
					element.NodeLinks.Add(reference);
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


		private void AddLink(
			Data.Link link,
			Dictionary<string, Node> elements,
			ModelViewData modelViewData)
		{
			Node sourceElement = GetOrAddElement(link.Source, elements, modelViewData);
			Node targetElement = GetOrAddElement(link.Target, elements, modelViewData);
			NodeLink reference = new NodeLink(sourceElement, targetElement, LinkKind.Direkt);
			sourceElement.NodeLinks.Add(reference);
		}


		private Data.Node ToNode(Node element)
		{
			Data.Node node = new Data.Node
			{
				Name = element.NodeName.Name,
				Type = element.NodeType,
				Nodes = ToChildren(element.ChildNodeItems),
				Links = ToLinks(element.NodeLinks),
				ViewData = ToViewData(element)
			};


			return node;
		}


		private List<Data.Link> ToLinks(NodeLinks nodeLinks)
		{
			if (!nodeLinks.Any())
			{
				return null;
			}

			List<Data.Link> links = null;

			foreach (Link reference in nodeLinks)
		//		.Where(r => r.Kind == LinkKind.Main))
			{
				foreach (NodeLink subReference in reference.Links)
				{
					if (subReference.Kind == LinkKind.Direkt)
					{
						if (links == null)
						{
							links = new List<Data.Link>();
						}

						links.Add(new Data.Link { Target = subReference.Target.NodeName.FullName });
					}
				}
			}

			return links;
		}


		private List<Data.Node> ToChildren(IEnumerable<Node> elementChildren)
		{
			if (!elementChildren.Any())
			{
				return null;
			}

			return elementChildren
				.Select(ToNode)
				.ToList();
		}


		private Node GetOrAddElement(
			string name,
			Dictionary<string, Node> elements,
			ModelViewData modelViewData)
		{
			if (!elements.TryGetValue(name, out Node element))
			{
				element = CreateElement(name, elements, modelViewData);
			}

			return element;
		}


		private Node CreateElement(
			string name, IDictionary<string, Node> elements,
			ModelViewData modelViewData)
		{
			string parentName = GetParentName(name);

			if (!elements.TryGetValue(parentName, out Node parent))
			{
				parent = CreateElement(parentName, elements, modelViewData);
			}

			string shortName = GetNamePart(name);
			NodeName nodeName = new NodeName(shortName, name);
			Node element = new Node(itemService, parent, nodeName, null);

			if (modelViewData != null && modelViewData.viewDataByName.TryGetValue(
				name, out Data.ViewData viewData))
			{
				element.ElementBounds = ToBounds(viewData);
				//element.ElementBrush = Converter.BrushFromHex(viewData.Color);
			}

			parent.AddChild(element);
			elements[name] = element;

			return element;
		}


		private string GetParentName(string fullName)
		{
			int index = fullName.LastIndexOf('.');

			if (index == -1)
			{
				// root namespace
				return "";
			}

			return fullName.Substring(0, index);
		}


		private string GetNamePart(string fullName)
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