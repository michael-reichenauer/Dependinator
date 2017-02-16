using System.Collections.Generic;
using System.Linq;
using Dependiator.Utils;


namespace Dependiator.Modeling.Analyzing
{
	internal class NodeLinks : IEnumerable<LinkGroup>
	{
		private readonly Element ownerElement;
		private readonly List<LinkGroup> references = new List<LinkGroup>();


		public NodeLinks(Element ownerElement)
		{
			this.ownerElement = ownerElement;
		}


		public int Count => references.Count;

		public IEnumerable<LinkGroup> SourceReferences => references
			.Where(r => r.Source == ownerElement);

		public IEnumerable<LinkGroup> TargetReferences => references
			.Where(r => r.Target == ownerElement);


		
		public void Add(LinkX link)
		{
			Asserter.Requires(link.Kind == LinkKind.Direkt);
			Asserter.Requires(link.Source == ownerElement || link.Target == ownerElement);

			if (link.Source.Name.FullName == link.Target.Name.FullName)
			{
				// Self reference, e.g. A type contains a field or parameter of the same type.
				return;
			}

			LinkGroup existing = references.FirstOrDefault(
				r => r.Source == link.Source && r.Target == link.Target);

			if (existing != null)
			{
				existing.Add(link);
				return;
			}

			LinkGroup linkGroup = new LinkGroup(link.Source, link.Target);
			linkGroup.Add(link);

			references.Add(linkGroup);

			if (link.Source == ownerElement)
			{
				AddPartReferences(link);
			}
		}


		private void AddPartReferences(LinkX reference)
		{
			AddPartReference(null, reference);
		}


		private void AddPartReference(LinkX currentReference, LinkX originalReference)
		{
			Element source = originalReference.Source;
			Element target = originalReference.Target;

			if (currentReference != null)
			{
				// A step on the way to the actual target
				AddSubReference(currentReference);
				source = currentReference.Target;
			}

			LinkKind kind;

			if (source == target)
			{
				// Source is same as target, (self reference)
				return;
			}
			else if (target.Ancestors().Any(ancestor => ancestor == source))
			{
				// Source is Ancestor of target		
				kind = LinkKind.Child;
				target = target.AncestorsAndSelf().First(ancestor => ancestor.Parent == source);
			}
			else if (target.Ancestors().Any(ancestor => ancestor == source.Parent))
			{
				// source and target are siblings or source and an target ancestor is siblings
				kind = LinkKind.Sibling;
				target = target.AncestorsAndSelf().First(ancestor => ancestor.Parent == source.Parent);
			}
			else
			{
				// Source is Decedent of target	
				kind = LinkKind.Parent;
				target = source.Parent;
			}

			LinkX partReference = new LinkX(source, target, kind);
			//partReference.Add(originalReference);
			if (kind == LinkKind.Sibling || kind == LinkKind.Parent)
			{
				source.Parent.NodeLinks.AddPartReference(partReference, originalReference);
			}
			else
			{
				source.NodeLinks.AddPartReference(partReference, originalReference);
			}
		}


		public void AddSubReference(LinkX reference)
		{
			Asserter.Requires(reference.Kind != LinkKind.Direkt);

			LinkGroup existing = references.FirstOrDefault(
				r => r.Source == reference.Source && r.Target == reference.Target);

			if (existing != null)
			{
				existing.Add(reference);
				return;
			}

			LinkGroup linkGroup = new LinkGroup(reference.Source, reference.Target);
			linkGroup.Add(reference);

			references.Add(linkGroup);
		}


		public IEnumerable<LinkGroup> DescendentAndSelfSourceReferences()
		{
			foreach (Element element in ownerElement.Children.DescendentsAndSelf())
			{
				foreach (LinkGroup reference in element.NodeLinks.SourceReferences)
				{
					yield return reference;
				}
			}
		}


		public IEnumerable<LinkGroup> DescendentAndSelfTargetReferences()
		{
			foreach (Element element in ownerElement.Children.DescendentsAndSelf())
			{
				foreach (LinkGroup reference in element.NodeLinks.TargetReferences)
				{
					yield return reference;
				}
			}
		}


		public IEnumerator<LinkGroup> GetEnumerator()
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