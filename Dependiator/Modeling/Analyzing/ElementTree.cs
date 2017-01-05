using System.Collections.Generic;


namespace Dependiator.Modeling.Analyzing
{
	internal class ElementTree
	{
		public NameSpaceElement Root { get; } = new NameSpaceElement("", "");


		public void AddChild(Element child)
		{
			Root.AddChild(child);
		}


		public void AddChildren(IEnumerable<Element> children)
		{
			Root.AddChildren(children);
		}
	}
}