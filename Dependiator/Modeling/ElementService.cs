using System.Collections.Generic;
using System.Linq;
using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal class ElementService : IElementService
	{
		public ElementTree ToElementTree(Data data, Data oldData)
		{
			ElementName rootName = new ElementName(Element.RootName, Element.RootName);
			Element root = new Element(rootName, DataNode.NameSpaceType, null);

			Dictionary<string, Element> elements = new Dictionary<string, Element>();
			elements[root.Name.FullName] = root;

			foreach (DataNode node in data.Nodes)
			{
				AddElement(node, null, elements, oldData);
			}

			ElementTree tree = new ElementTree(root);
			return tree;
		}


		public Data ToData(ElementTree elementTree)
		{
			Data data = new Data();	

			List<DataNode> nodes = elementTree.Root.Children
				.Select(element => ToNode(element, data))
				.ToList();

			data.Nodes = nodes;

			return data;
		}


		private void AddElement(
			DataNode dataNode,
			string parentName,
			Dictionary<string, Element> elements,
			Data oldData)
		{
			string name = parentName != null ? parentName + "." + dataNode.Name : dataNode.Name;

			Element element = GetOrAddElement(name, elements, oldData);
			element.SetType(dataNode.Type);

			if (dataNode.Location.HasValue || dataNode.Size.HasValue)
			{
				element.SetLocationAndSize(dataNode.Location, dataNode.Size);
			}
			else
			{
				if (oldData != null && oldData.NodesByName.TryGetValue(name, out DataNode oldNode))
				{
					element.SetLocationAndSize(oldNode.Location, oldNode.Size);
				}
			}


			if (dataNode.Nodes != null)
			{
				foreach (DataNode childNode in dataNode.Nodes)
				{
					AddElement(childNode, name, elements, oldData);
				}
			}

			if (dataNode.Links != null)
			{
				foreach (DataLink dataLink in dataNode.Links)
				{
					Element targetElement = GetOrAddElement(dataLink.Target, elements, oldData);
					Reference reference = new Reference(element, targetElement, ReferenceKind.Direkt);
					element.References.Add(reference);
				}
			}
		}


		private DataNode ToNode(Element element, Data data)
		{
			DataNode node = new DataNode
			{
				Name = element.Name.Name,
				Type = element.Type,
				Nodes = ToChildren(element.Children, data),
				Links = ToLinks(element.References),
				Location = element.Location,
				Size = element.Size
			};

			data.NodesByName[element.Name.FullName] = node;

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


		private List<DataNode> ToChildren(ElementChildren elementChildren, Data data)
		{
			if (!elementChildren.Any())
			{
				return null;
			}

			return elementChildren
				.Select(element => ToNode(element, data))
				.ToList();
		}


		private Element GetOrAddElement(string name, Dictionary<string, Element> elements, Data oldData)
		{
			if (!elements.TryGetValue(name, out Element element))
			{
				element = CreateElement(name, elements, oldData);
			}

			return element;
		}


		private Element CreateElement(string name, IDictionary<string, Element> elements, Data oldData)
		{
			string parentName = GetParentName(name);

			if (!elements.TryGetValue(parentName, out Element parent))
			{
				parent = CreateElement(parentName, elements, oldData);
			}

			string shortName = GetNamePart(name);
			ElementName elementName = new ElementName(shortName, name);
			Element element = new Element(elementName, null, parent);

			if (oldData != null && oldData.NodesByName.TryGetValue(name, out DataNode oldNode))
			{
				element.SetLocationAndSize(oldNode.Location, oldNode.Size);
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