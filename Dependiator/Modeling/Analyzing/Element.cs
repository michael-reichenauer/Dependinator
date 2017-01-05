using System;
using System.Collections.Generic;
using System.Linq;
using Dependiator.Utils;


namespace Dependiator.Modeling.Analyzing
{
	internal class Element
	{
		private readonly List<Element> childElements = new List<Element>();

		private readonly List<Reference> references = new List<Reference>();

		public string Name { get; }
		public string FullName { get; }


		public Element(string name, string fullName)
		{
			Name = name;
			FullName = fullName;
		}


		public IEnumerable<Element> ChildElements => childElements;


		public void AddChild(Element child)
		{
			childElements.Add(child);
		}


		public void AddChildren(IEnumerable<Element> children)
		{
			childElements.AddRange(children);
		}

		public void AddReference(Reference reference)
		{
			Asserter.Requires(reference.Source == this ||  reference.Target == this);

			if (references.Any(r => r.Source == reference.Source && r.Target == reference.Target))
			{
				// Allready added
				return;
			}

			references.Add(reference);
		}


		public override string ToString() => FullName;
	}
}