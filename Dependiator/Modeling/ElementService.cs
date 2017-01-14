using System.Collections.Generic;
using System.Linq;
using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal class ElementService : IElementService
	{
		public ElementTree ToElementTree(Data data)
		{
			ElementName rootName = new ElementName(Element.RootName, Element.RootName);
			Element root = new Element(rootName, DataNode.NameSpaceType, null);

			Dictionary<string, Element> elements = new Dictionary<string, Element>();
			elements[root.Name.FullName] = root;

			foreach (DataNode node in data.Nodes)
			{
				AddNode(node, null, elements);
			}

			ElementTree tree = new ElementTree(root);
			return tree;
		}


		public Data ToData(ElementTree elementTree)
		{
			List<DataNode> nodes = elementTree.Root.Children
				.Select(ToNode)
				.ToList();

			Data data = new Data
			{
				Nodes = nodes
			};

			return data;
		}


		private void AddNode(
			DataNode dataNode,
			string parentName,
			Dictionary<string, Element> elements)
		{
			string name = parentName != null ? parentName + "." + dataNode.Name : dataNode.Name;

			Element element = GetOrAddElement(name, elements);
			element.SetType(dataNode.Type);

			if (dataNode.Nodes != null)
			{
				foreach (DataNode childNode in dataNode.Nodes)
				{
					AddNode(childNode, name, elements);
				}
			}

			if (name == "Dependiator")
			{
				
			}

			if (dataNode.Links != null)
			{
				foreach (DataLink dataLink in dataNode.Links)
				{
					Element targetElement = GetOrAddElement(dataLink.Target, elements);
					Reference reference = new Reference(element, targetElement, ReferenceKind.Direkt);
					element.References.Add(reference);
				}
			}
		}


		private DataNode ToNode(Element element)
		{
			DataNode node = new DataNode
			{
				Name = element.Name.Name,
				Type = element.Type,
				Nodes = ToChildren(element.Children),
				Links = ToLinks(element.References)
			};

			return node;
		}


		private List<DataLink> ToLinks(References references)
		{
			if (!references.Any())
			{
				return null;
			}

			List<DataLink> links = null;

			foreach (Reference reference in references
				.Where(r => r.Kind == ReferenceKind.Main))
			{
				foreach (Reference subReference in reference.SubReferences)
				{
					if (subReference.Kind == ReferenceKind.Direkt)
					{
						if (links == null)
						{
							links = new List<DataLink>();
						}

						links.Add(new DataLink { Target = subReference.Target.Name.FullName});
					}
				}
			}

			return links;
		}


		private List<DataNode> ToChildren(ElementChildren elementChildren)
		{
			if (!elementChildren.Any())
			{
				return null;
			}

			return elementChildren
				.Select(ToNode)
				.ToList();
		}


		private Element GetOrAddElement(string name, Dictionary<string, Element> elements)
		{
			if (!elements.TryGetValue(name, out Element element))
			{
				element = CreateElement(name, elements);
			}

			return element;
		}


		private Element CreateElement(string name, IDictionary<string, Element> elements)
		{
			string parentName = GetParentName(name);

			if (!elements.TryGetValue(parentName, out Element parent))
			{
				parent = CreateElement(parentName, elements);
			}

			string shortName = GetNamePart(name);
			ElementName elementName = new ElementName(shortName, name);
			Element element = new Element(elementName, null, parent);

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