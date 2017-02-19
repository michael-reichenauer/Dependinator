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
		public ElementTree ToElementTree(Data.Model data, ModelViewData modelViewData)
		{
			NodeName rootName = new NodeName(Element.RootName, Element.RootName);
			Element root = new Element(rootName, Element.NameSpaceType, null);

			Dictionary<string, Element> elements = new Dictionary<string, Element>();
			elements[root.Name.FullName] = root;

			foreach (Data.Node node in data.Nodes ?? Enumerable.Empty<Data.Node>())
			{
				AddElement(node, null, elements, modelViewData);
			}

			foreach (Data.Link link in data.Links ?? Enumerable.Empty<Data.Link>())
			{
				AddLink(link, elements, modelViewData);
			}

			ElementTree tree = new ElementTree(root);
			return tree;
		}


		public Data.Model ToData(ElementTree elementTree)
		{
			List<Data.Node> nodes = elementTree.Root.Children
				.Select(ToNode)
				.ToList();

			Data.Model model = new Data.Model();
			model.Nodes = nodes;

			return model;
		}


		public ModelViewData ToViewData(ElementTree elementTree)
		{
			ModelViewData modelViewData = new ModelViewData();

			foreach (Element child in elementTree.Root.Children)
			{
				AddViewData(child, modelViewData);
			}

			return modelViewData;
		}


		private void AddViewData(Element element, ModelViewData modelViewData)
		{
			Data.ViewData nodeViewData = ToViewData(element);

			modelViewData.viewDataByName[element.Name.FullName] = nodeViewData;
			
			foreach (Element child in element.Children)
			{
				AddViewData(child, modelViewData);
			}
		}


		private static Data.ViewData ToViewData(Element element)
		{
			Data.ViewData viewData = new Data.ViewData
			{
				Color = Converter.HexFromBrush(element.ElementBrush),
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
			Dictionary<string, Element> elements,
			ModelViewData modelViewData)
		{
			string fullName = string.IsNullOrEmpty(parentName)
				? dataNode.Name
				: parentName + "." + dataNode.Name;

			Element element = GetOrAddElement(fullName, elements, modelViewData);
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
					Element targetElement = GetOrAddElement(dataLink.Target, elements, modelViewData);
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
			Dictionary<string, Element> elements,
			ModelViewData modelViewData)
		{
			Element sourceElement = GetOrAddElement(link.Source, elements, modelViewData);
			Element targetElement = GetOrAddElement(link.Target, elements, modelViewData);
			NodeLink reference = new NodeLink(sourceElement, targetElement, LinkKind.Direkt);
			sourceElement.NodeLinks.Add(reference);
		}


		private Data.Node ToNode(Element element)
		{
			Data.Node node = new Data.Node
			{
				Name = element.Name.Name,
				Type = element.Type,
				Nodes = ToChildren(element.Children),
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

			foreach (LinkGroup reference in nodeLinks)
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

						links.Add(new Data.Link { Target = subReference.Target.Name.FullName });
					}
				}
			}

			return links;
		}


		private List<Data.Node> ToChildren(ElementChildren elementChildren)
		{
			if (!elementChildren.Any())
			{
				return null;
			}

			return elementChildren
				.Select(ToNode)
				.ToList();
		}


		private Element GetOrAddElement(
			string name,
			Dictionary<string, Element> elements,
			ModelViewData modelViewData)
		{
			if (!elements.TryGetValue(name, out Element element))
			{
				element = CreateElement(name, elements, modelViewData);
			}

			return element;
		}


		private Element CreateElement(
			string name, IDictionary<string, Element> elements,
			ModelViewData modelViewData)
		{
			string parentName = GetParentName(name);

			if (!elements.TryGetValue(parentName, out Element parent))
			{
				parent = CreateElement(parentName, elements, modelViewData);
			}

			string shortName = GetNamePart(name);
			NodeName nodeName = new NodeName(shortName, name);
			Element element = new Element(nodeName, null, parent);

			if (modelViewData != null && modelViewData.viewDataByName.TryGetValue(
				name, out Data.ViewData viewData))
			{
				element.ElementBounds = ToBounds(viewData);
				//element.ElementBrush = Converter.BrushFromHex(viewData.Color);
			}

			parent.Children.Add(element);
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