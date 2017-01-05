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

		public IEnumerable<Reference> SourceReferences => references.Where(r => r.Source == this);
		public IEnumerable<Reference> TargetReferences => references.Where(r => r.Target == this);


		public IEnumerable<Reference> DescendentAndSelfSourceReferences()
		{
			foreach (Element element in DescendentsAndSelfElements())
			{
				foreach (Reference reference in element.SourceReferences)
				{
					yield return reference;
				}
			}
		}

		public IEnumerable<Reference> DescendentAndSelfTargetReferences()
		{
			foreach (Element element in DescendentsAndSelfElements())
			{
				foreach (Reference reference in element.TargetReferences)
				{
					yield return reference;
				}
			}
		}


		public IEnumerable<Element> DescendentsAndSelfElements()
		{
			yield return this;
			foreach (Element descendent in DescendentElements())
			{		
				yield return descendent;
				
			}
		}

		public IEnumerable<Element> DescendentElements()
		{
			foreach (Element child in ChildElements)
			{
				yield return child;
				foreach (Element descendent in child.DescendentElements())
				{
					yield return descendent;
				}
			}
		} 


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