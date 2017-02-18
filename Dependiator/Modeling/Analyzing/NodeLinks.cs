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


		
		public void Add(NodeLink nodeLink)
		{
			Asserter.Requires(nodeLink.Kind == LinkKind.Direkt);
			Asserter.Requires(nodeLink.Source == ownerElement || nodeLink.Target == ownerElement);

			if (nodeLink.Source.Name.FullName == nodeLink.Target.Name.FullName)
			{
				// Self reference, e.g. A type contains a field or parameter of the same type.
				return;
			}

			LinkGroup existing = references.FirstOrDefault(
				r => r.Source == nodeLink.Source && r.Target == nodeLink.Target);

			if (existing != null)
			{
				existing.Add(nodeLink);
				return;
			}

			LinkGroup linkGroup = new LinkGroup(nodeLink.Source, nodeLink.Target);
			linkGroup.Add(nodeLink);

			references.Add(linkGroup);

			if (nodeLink.Source == ownerElement)
			{
				AddPartReferences(nodeLink);
			}
		}


		private void AddPartReferences(NodeLink reference)
		{
			AddPartReference(null, reference);
		}


		private void AddPartReference(NodeLink currentReference, NodeLink originalReference)
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

			NodeLink partReference = new NodeLink(source, target, kind);
			if (kind == LinkKind.Sibling || kind == LinkKind.Parent)
			{
				source.Parent.NodeLinks.AddPartReference(partReference, originalReference);
			}
			else
			{
				source.NodeLinks.AddPartReference(partReference, originalReference);
			}
		}


		public void AddSubReference(NodeLink reference)
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