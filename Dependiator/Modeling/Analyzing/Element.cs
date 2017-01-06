using System;
using System.Collections.Generic;
using Dependiator.MainViews.Private;


namespace Dependiator.Modeling.Analyzing
{
	internal class Element
	{
		public ElementName Name { get; }

		public Element Parent { get; }


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

		public ElementChildren Children { get; }

		public References References { get; }


		//public string DeclaringName => Parent?.Name.FullName ?? "";

		public Element(ElementName name, Element parent)
		{
			References = new References(this);
			Children = new ElementChildren(this);
			Name = name;

			Parent = parent;
		}

		public override string ToString() => Name.FullName;
	}
}