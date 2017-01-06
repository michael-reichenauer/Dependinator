using System.Collections.Generic;
using System.Linq;
using Dependiator.Utils;


namespace Dependiator.Modeling.Analyzing
{
	internal class References : IEnumerable<Reference>
	{
		private readonly Element ownerElement;
		private readonly List<Reference> references = new List<Reference>();


		public References(Element ownerElement)
		{
			this.ownerElement = ownerElement;
		}

		public IEnumerable<Reference> SourceReferences => references
			.Where(r => r.Source == ownerElement);

		public IEnumerable<Reference> TargetReferences => references
			.Where(r => r.Target == ownerElement);


		public void AddReference(Reference reference)
		{
			Asserter.Requires(reference.Source == ownerElement || reference.Target == ownerElement);

			if (references.Any(r => r.Source == reference.Source && r.Target == reference.Target))
			{
				// Allready added
				return;
			}

			references.Add(reference);
		}


		public IEnumerable<Reference> DescendentAndSelfSourceReferences()
		{
			foreach (Element element in ownerElement.Children.DescendentsAndSelf())
			{
				foreach (Reference reference in element.References.SourceReferences)
				{
					yield return reference;
				}
			}
		}

		public IEnumerable<Reference> DescendentAndSelfTargetReferences()
		{
			foreach (Element element in ownerElement.Children.DescendentsAndSelf())
			{
				foreach (Reference reference in element.References.TargetReferences)
				{
					yield return reference;
				}
			}
		}

		public IEnumerator<Reference> GetEnumerator()
		{
			return references.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}