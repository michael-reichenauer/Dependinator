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


		public int Count => references.Count;

		public IEnumerable<Reference> SourceReferences => references
			.Where(r => r.Source == ownerElement);

		public IEnumerable<Reference> TargetReferences => references
			.Where(r => r.Target == ownerElement);


		
		public void Add(Reference reference)
		{
			Asserter.Requires(reference.Kind == ReferenceKind.Direkt);
			Asserter.Requires(reference.Source == ownerElement || reference.Target == ownerElement);

			if (reference.Target.Name.Name == "Dependiator")
			{
				
			}

			if (reference.Source.Name.FullName == reference.Target.Name.FullName)
			{
				// Self reference, e.g. A type contains a field or parameter of the same type.
				return;
			}

			Reference existing = references.FirstOrDefault(
				r => r.Source == reference.Source && r.Target == reference.Target);

			if (existing != null)
			{
				existing.Add(reference);
				return;
			}

			Reference mainReference = new Reference(
				reference.Source, reference.Target, ReferenceKind.Main);
			mainReference.Add(reference);

			references.Add(mainReference);

			if (reference.Source == ownerElement)
			{
				AddPartReferences(reference);
			}
		}


		private void AddPartReferences(Reference reference)
		{
			AddPartReference(null, reference);
		}


		private void AddPartReference(Reference currentReference, Reference originalReference)
		{
			Element source = originalReference.Source;
			Element target = originalReference.Target;

			if (currentReference != null)
			{
				// A step on the way to the actual target
				AddSubReference(currentReference);
				source = currentReference.Target;
			}

			ReferenceKind kind;

			if (source == target)
			{
				// Source is same as target, (self reference)
				return;
			}
			else if (target.Ancestors().Any(ancestor => ancestor == source))
			{
				// Source is Ancestor of target		
				kind = ReferenceKind.Child;
				target = target.AncestorsAndSelf().First(ancestor => ancestor.Parent == source);
			}
			else if (target.Ancestors().Any(ancestor => ancestor == source.Parent))
			{
				// source and target are siblings or source and an target ancestor is siblings
				kind = ReferenceKind.Sibling;
				target = target.AncestorsAndSelf().First(ancestor => ancestor.Parent == source.Parent);
			}
			else
			{
				// Source is Decedent of target	
				kind = ReferenceKind.Parent;
				target = source.Parent;
			}

			Reference partReference = new Reference(source, target, kind);
			partReference.Add(originalReference);
			if (kind == ReferenceKind.Sibling || kind == ReferenceKind.Parent)
			{
				source.Parent.References.AddPartReference(partReference, originalReference);
			}
			else
			{
				source.References.AddPartReference(partReference, originalReference);
			}
		}


		public void AddSubReference(Reference reference)
		{
			Asserter.Requires(reference.Kind != ReferenceKind.Direkt);

			Reference existing = references.FirstOrDefault(
				r => r.Source == reference.Source && r.Target == reference.Target);

			if (existing != null)
			{
				existing.Add(reference);
				return;
			}

			Reference mainReference = new Reference(
				reference.Source, reference.Target, ReferenceKind.Main);
			mainReference.Add(reference);

			references.Add(mainReference);
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

		public override string ToString() => $"{references.Count} references";
	}
}