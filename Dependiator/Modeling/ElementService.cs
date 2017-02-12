using System;
using System.Collections.Generic;
using System.Linq;
using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal class ElementService : IElementService
	{
		public ElementTree ToElementTree(Data.Model data, ModelViewData modelViewData)
		{
			ElementName rootName = new ElementName(Element.RootName, Element.RootName);
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
			if (element.Location.HasValue && element.Size.HasValue)
			{
				Data.ViewData nodeViewData = new Data.ViewData
				{
					X = element.Location.Value.X,
					Y = element.Location.Value.Y,
					Width = element.Size.Value.Width,
					Height = element.Size.Value.Height
				};

				modelViewData.viewDataByName[element.Name.FullName] = nodeViewData;
			}

			foreach (Element child in element.Children)
			{
				AddViewData(child, modelViewData);
			}
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
				element.SetLocationAndSize(dataNode.ViewData);
			}
			else
			{
				if (modelViewData != null && modelViewData.viewDataByName.TryGetValue(
					fullName, out Data.ViewData viewData))
				{
					element.SetLocationAndSize(viewData);
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
					Reference reference = new Reference(element, targetElement, ReferenceKind.Direkt);
					element.References.Add(reference);
				}
			}
		}


		private void AddLink(
			Data.Link link, 
			Dictionary<string, Element> elements, 
			ModelViewData modelViewData)
		{
			Element sourceElement = GetOrAddElement(link.Source, elements, modelViewData);
			Element targetElement = GetOrAddElement(link.Target, elements, modelViewData);
			Reference reference = new Reference(sourceElement, targetElement, ReferenceKind.Direkt);
			sourceElement.References.Add(reference);
		}


		private Data.Node ToNode(Element element)
		{
			Data.Node node = new Data.Node
			{
				Name = element.Name.Name,
				Type = element.Type,
				Nodes = ToChildren(element.Children),
				Links = ToLinks(element.References),
			};

			if (element.Location.HasValue && element.Size.HasValue)
			{
				node.ViewData = new Data.ViewData
				{
					X = element.Location.Value.X,
					Y = element.Location.Value.Y,
					Width = element.Size.Value.Width,
					Height = element.Size.Value.Height
				};
			}

			return node;
		}


		private List<Data.Link> ToLinks(References references)
		{
			if (!references.Any())
			{
				return null;
			}

			List<Data.Link> links = null;

			foreach (Reference reference in references
				.Where(r => r.Kind == ReferenceKind.Main))
			{
				foreach (Reference subReference in reference.SubReferences)
				{
					if (subReference.Kind == ReferenceKind.Direkt)
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

			if (parentName == "Axis")
			{
				
			}

			if (!elements.TryGetValue(parentName, out Element parent))
			{
				parent = CreateElement(parentName, elements, modelViewData);
			}

			string shortName = GetNamePart(name);
			ElementName elementName = new ElementName(shortName, name);
			Element element = new Element(elementName, null, parent);

			if (modelViewData != null && modelViewData.viewDataByName.TryGetValue(
				name, out Data.ViewData viewData))
			{
				element.SetLocationAndSize(viewData);
			}

			parent.Children.Add(element);
			elements[name] = element;

			return element;
		}


		private string GetParentName(string name)
		{
			int index = name.LastIndexOf('.');

			if (index == -1)
			{
				// root namespace
				return "";
			}

			return name.Substring(0, index);
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