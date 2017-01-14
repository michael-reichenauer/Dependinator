using System.Collections.Generic;


namespace Dependiator.Modeling.Analyzing
{
	internal class ElementChildren : IEnumerable<Element>
	{
		private readonly Element parent;
		private readonly List<Element> children = new List<Element>();


		public ElementChildren(Element parent)
		{
			this.parent = parent;
		}


		public IEnumerable<Element> Descendents()
		{
			foreach (Element child in children)
			{
				yield return child;

				foreach (Element descendent in child.Children.Descendents())
				{
					yield return descendent;
				}
			}
		}


		public IEnumerable<Element> DescendentsAndSelf()
		{
			yield return parent;

			foreach (Element descendent in Descendents())
			{
				yield return descendent;
			}
		}


		public void Add(Element childElement)
		{
			children.Add(childElement);
		}


		public void Add(IEnumerable<Element> childElements)
		{
			children.AddRange(childElements);
		}

		public IEnumerator<Element> GetEnumerator()
		{
			return children.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}


		public override string ToString() => $"{children.Count} children";
	}
}