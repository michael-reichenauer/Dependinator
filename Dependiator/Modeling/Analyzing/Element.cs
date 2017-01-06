using System;
using Dependiator.MainViews.Private;


namespace Dependiator.Modeling.Analyzing
{
	internal class Element
	{
		public ElementName Name { get; }

		public Element Parent { get; }

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