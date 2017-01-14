using System.Collections.Generic;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling.Analyzing
{
	internal class Element
	{
		public static string NameSpaceType = DataNode.NameSpaceType;
		public static readonly string TypeType = DataNode.TypeType;
		public static readonly string MemberType = DataNode.MemberType;

		public static string RootName = "";

		public ElementName Name { get; }

		public string Type { get; private set; }

		public Element Parent { get; }

		public ElementChildren Children { get; }

		public References References { get; }


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
			References = new References(this);
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