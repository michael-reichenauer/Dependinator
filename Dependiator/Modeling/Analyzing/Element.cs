using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling.Analyzing
{
	internal class Element
	{
		public static string NameSpaceType = "NameSpace";
		public static readonly string TypeType = "Type";
		public static readonly string MemberType = "Member";

		public static string RootName = "";

		public ElementName Name { get; }

		public string Type { get; private set; }

		public Element Parent { get; }

		public ElementChildren Children { get; }

		public NodeLinks NodeLinks { get; }

		public Rect? ElementBounds { get; set; }

		public Brush ElementBrush { get; set; }


		public IEnumerable<Element> AncestorsAndSelf()
		{
			yield return this;

			foreach (Element ancestor in Ancestors())
			{			
				yield return ancestor;
			}
		}


		public IEnumerable<Element> Ancestors()
		{
			Element current = Parent;

			while (current != null)
			{
				yield return current;
				current = current.Parent;
			}
		}


		public Element(ElementName name, string type, Element parent)
		{
			Type = type;
			NodeLinks = new NodeLinks(this);
			Children = new ElementChildren(this);
			Name = name;

			Parent = parent;
		}


		public void SetType(string type)
		{
			Type = type;
		}

		public override string ToString() => Name.FullName;
	}
}