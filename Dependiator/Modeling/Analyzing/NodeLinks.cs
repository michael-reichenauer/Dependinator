using System.Collections.Generic;
using System.Linq;
using Dependiator.Utils;


namespace Dependiator.Modeling.Analyzing
{
	internal class NodeLinks : IEnumerable<Link>
	{
		private readonly IItemService itemService;
		private readonly Node ownerNode;
		private readonly List<Link> links = new List<Link>();


		public NodeLinks(IItemService itemService, Node ownerNode)
		{
			this.itemService = itemService;
			this.ownerNode = ownerNode;
		}


		public int Count => links.Count;

		public IEnumerable<Link> SourceReferences => links
			.Where(r => r.Source == ownerNode);

		public IEnumerable<Link> TargetReferences => links
			.Where(r => r.Target == ownerNode);


		
		public void Add(NodeLink nodeLink)
		{
			Asserter.Requires(nodeLink.Source == ownerNode);

			if (nodeLink.Source.NodeName == nodeLink.Target.NodeName)
			{
				// Self reference, e.g. A type contains a field or parameter of the same type.
				return;
			}

			Link existing = links.FirstOrDefault(
				l => l.Source == nodeLink.Source && l.Target == nodeLink.Target);

			if (existing != null)
			{
				existing.Add(nodeLink);
				return;
			}

			AddPartReferences(nodeLink);			
		}


		private static void AddPartReferences(NodeLink reference)
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

			if (source == target)
			{
				// Source is same as target, reached end
				return;
			}
			else if (target.Ancestors().Any(ancestor => ancestor == source))
			{
				// Source is Ancestor of target		
				target = target.AncestorsAndSelf().First(ancestor => ancestor.ParentNode == source);
			}
			else if (target.Ancestors().Any(ancestor => ancestor == source.ParentNode))
			{
				// source and target are siblings or source and an target ancestor is siblings
				target = target.AncestorsAndSelf().First(ancestor => ancestor.ParentNode == source.ParentNode);
			}
			else
			{
				// Source is Decedent of target	
				target = source.ParentNode;
			}

			NodeLink partReference = new NodeLink(source, target);
			
			AddPartReference(partReference, actualNodeLink);
		}


		public void AddLinkPart(NodeLink linkPart, NodeLink actualNodeLink)
		{
			Link existing = links.FirstOrDefault(
				l => l.Source == linkPart.Source && l.Target == linkPart.Target);

			if (existing != null)
			{
				existing.Add(actualNodeLink);
				return;
			}

			Link linkGroup = new Link(itemService, linkPart.Source, linkPart.Target);
			linkGroup.Add(actualNodeLink);
			links.Add(linkGroup);
		}


		public IEnumerable<Link> DescendentAndSelfSourceReferences()
		{
			foreach (Node node in ownerNode.DescendentsAndSelf())
			{
				foreach (Link reference in node.NodeLinks.SourceReferences)
				{
					yield return reference;
				}
			}
		}


		public IEnumerable<Link> DescendentAndSelfTargetReferences()
		{
			foreach (Node node in ownerNode.DescendentsAndSelf())
			{
				foreach (Link reference in node.NodeLinks.TargetReferences)
				{
					yield return reference;
				}
			}
		}


		public IEnumerator<Link> GetEnumerator()
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