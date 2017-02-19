using System.Collections.Generic;
using System.Linq;
using Dependiator.Utils;


namespace Dependiator.Modeling.Analyzing
{
	internal class NodeLinks : IEnumerable<LinkGroup>
	{
		private readonly Node ownerNode;
		private readonly List<LinkGroup> links = new List<LinkGroup>();


		public NodeLinks(Node ownerNode)
		{
			this.ownerNode = ownerNode;
		}


		public int Count => links.Count;

		public IEnumerable<LinkGroup> SourceReferences => links
			.Where(r => r.Source == ownerNode);

		public IEnumerable<LinkGroup> TargetReferences => links
			.Where(r => r.Target == ownerNode);


		
		public void Add(NodeLink nodeLink)
		{
			Asserter.Requires(nodeLink.Kind == LinkKind.Direkt);
			Asserter.Requires(nodeLink.Source == ownerNode);

			//Asserter.Requires(nodeLink.Source.Name.FullName != nodeLink.Target.Name.FullName);
			if (nodeLink.Source.NodeName.FullName == nodeLink.Target.NodeName.FullName)
			{
				// Self reference, e.g. A type contains a field or parameter of the same type.
				return;
			}

			LinkGroup existing = links.FirstOrDefault(
				l => l.Source == nodeLink.Source && l.Target == nodeLink.Target);

			if (existing != null)
			{
				existing.Add(nodeLink);
				return;
			}

			//LinkGroup linkGroup = new LinkGroup(nodeLink.Source, nodeLink.Target);
			//linkGroup.Add(nodeLink);

			//links.Add(linkGroup);

			//if (nodeLink.Source == ownerElement)
			//{
				AddPartReferences(nodeLink);
			//}
		}


		private void AddPartReferences(NodeLink reference)
		{
			AddPartReference(null, reference);
		}


		private static void AddPartReference(NodeLink linkPart, NodeLink actualNodeLink)
		{
			Node source = actualNodeLink.Source;
			Node target = actualNodeLink.Target;

			if (linkPart != null)
			{
				// A step on the way to the actual target
				linkPart.Source.NodeLinks.AddLinkPart(linkPart, actualNodeLink);
				linkPart.Target.NodeLinks.AddLinkPart(linkPart, actualNodeLink);
				source = linkPart.Target;
			}

			LinkKind kind;

			if (source == target)
			{
				// Source is same as target, reached end
				return;
			}
			else if (target.Ancestors().Any(ancestor => ancestor == source))
			{
				// Source is Ancestor of target		
				kind = LinkKind.Child;
				target = target.AncestorsAndSelf().First(ancestor => ancestor.ParentNode == source);
			}
			else if (target.Ancestors().Any(ancestor => ancestor == source.ParentNode))
			{
				// source and target are siblings or source and an target ancestor is siblings
				kind = LinkKind.Sibling;
				target = target.AncestorsAndSelf().First(ancestor => ancestor.ParentNode == source.ParentNode);
			}
			else
			{
				// Source is Decedent of target	
				kind = LinkKind.Parent;
				target = source.ParentNode;
			}

			NodeLink partReference = new NodeLink(source, target, kind);
			//if (kind == LinkKind.Sibling || kind == LinkKind.Parent)
			//{
			//	source.Parent.NodeLinks.AddPartReference(partReference, actualNodeLink);
			//}
			//else
			//{
				AddPartReference(partReference, actualNodeLink);
			//}
		}


		public void AddLinkPart(NodeLink linkPart, NodeLink actualNodeLink)
		{
			Asserter.Requires(linkPart.Kind != LinkKind.Direkt);

			LinkGroup existing = links.FirstOrDefault(
				l => l.Source == linkPart.Source && l.Target == linkPart.Target);

			if (existing != null)
			{
				existing.Add(actualNodeLink);
				return;
			}

			LinkGroup linkGroup = new LinkGroup(linkPart.Source, linkPart.Target);
			linkGroup.Add(actualNodeLink);
			links.Add(linkGroup);
		}


		public IEnumerable<LinkGroup> DescendentAndSelfSourceReferences()
		{
			foreach (Node node in ownerNode.DescendentsAndSelf())
			{
				foreach (LinkGroup reference in node.NodeLinks.SourceReferences)
				{
					yield return reference;
				}
			}
		}


		public IEnumerable<LinkGroup> DescendentAndSelfTargetReferences()
		{
			foreach (Node node in ownerNode.DescendentsAndSelf())
			{
				foreach (LinkGroup reference in node.NodeLinks.TargetReferences)
				{
					yield return reference;
				}
			}
		}


		public IEnumerator<LinkGroup> GetEnumerator()
		{
			return links.GetEnumerator();
		}


		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public override string ToString() => $"{links.Count} references";
	}
}