using System.Collections.Generic;
using System.Linq;
using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal class ElementService : IElementService
	{
		public ElementTree ToElementTree(DataModel data, DataModel oldData)
		{
			ElementName rootName = new ElementName(Element.RootName, Element.RootName);
			Element root = new Element(rootName, Element.NameSpaceType, null);

			Dictionary<string, Element> elements = new Dictionary<string, Element>();
			elements[root.Name.FullName] = root;

			foreach (Data.Node node in data.Model.Nodes)
			{
				AddElement(node, null, elements, oldData);
			}

			ElementTree tree = new ElementTree(root);
			return tree;
		}


		public DataModel ToData(ElementTree elementTree)
		{
			DataModel data = new DataModel();	

			List<Data.Node> nodes = elementTree.Root.Children
				.Select(element => ToNode(element, data))
				.ToList();

			Data.Model model = new Data.Model();
			model.Nodes = nodes;
			data.Model = model;

			return data;
		}


		private void AddElement(
			Data.Node dataNode,
			string parentName,
			Dictionary<string, Element> elements,
			DataModel oldData)
		{
			string name = parentName != null ? parentName + "." + dataNode.Name : dataNode.Name;

			Element element = GetOrAddElement(name, elements, oldData);
			element.SetType(dataNode.Type);

			if (dataNode.ViewData != null)
			{
				element.SetLocationAndSize(dataNode.ViewData);
			}
			else
			{
				if (oldData != null && oldData.NodesByName.TryGetValue(name, out Data.Node oldNode))
				{
					element.SetLocationAndSize(oldNode.ViewData);
				}
			}


			if (dataNode.Nodes != null)
			{
				foreach (Data.Node childNode in dataNode.Nodes)
				{
					AddElement(childNode, name, elements, oldData);
				}
			}

			if (dataNode.Links != null)
			{
				foreach (Data.Link dataLink in dataNode.Links)
				{
					Element targetElement = GetOrAddElement(dataLink.Target, elements, oldData);
					Reference reference = new Reference(element, targetElement, ReferenceKind.Direkt);
					element.References.Add(reference);
				}
			}
		}


		private Data.Node ToNode(Element element, DataModel data)
		{
			Data.Node node = new Data.Node
			{
				Name = element.Name.Name,
				Type = element.Type,
				Nodes = ToChildren(element.Children, data),
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

			data.NodesByName[element.Name.FullName] = node;

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

						links.Add(new Data.Link { Target = subReference.Target.Name.FullName});
					}
				}
			}

			return links;
		}


		private List<Data.Node> ToChildren(ElementChildren elementChildren, DataModel data)
		{
			if (!elementChildren.Any())
			{
				return null;
			}

			return elementChildren
				.Select(element => ToNode(element, data))
				.ToList();
		}


		private Element GetOrAddElement(string name, Dictionary<string, Element> elements, DataModel oldData)
		{
			if (!elements.TryGetValue(name, out Element element))
			{
				element = CreateElement(name, elements, oldData);
			}

			return element;
		}


		private Element CreateElement(string name, IDictionary<string, Element> elements, DataModel oldData)
		{
			string parentName = GetParentName(name);

			if (!elements.TryGetValue(parentName, out Element parent))
			{
				parent = CreateElement(parentName, elements, oldData);
			}

			string shortName = GetNamePart(name);
			ElementName elementName = new ElementName(shortName, name);
			Element element = new Element(elementName, null, parent);

			if (oldData != null && oldData.NodesByName.TryGetValue(name, out Data.Node oldNode))
			{
				element.SetLocationAndSize(oldNode.ViewData);
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