using System.Collections.Generic;
using System.Linq;


namespace Dependiator.Modeling.Analyzing
{
	//internal class LinkGroup
	//{
	

	//	public LinkGroup(Node source, Node target)
	//	{
	//		Source = source;
	//		Target = target;
	//	}

	//	public Node Source { get; }

	//	public Node Target { get; }

	//	public IReadOnlyList<NodeLink> Links => links;


	//	public void Add(NodeLink nodeLink)
	//	{
	//		if (links
	//			.Any(l => l.Source == nodeLink.Source && l.Target == nodeLink.Target && l.Kind == nodeLink.Kind))
	//		{
	//			return;
	//		}

	//		links.Add(nodeLink);
	//	}

	//	public override string ToString() => $"{Source} -> {Target}";
	//}


	internal class NodeLink
	{
		public NodeLink(Node source, Node target, LinkKind kind)
		{
			Source = source;
			Target = target;
			Kind = kind;
		}

		public Node Source { get; }

		public Node Target { get; }

		public LinkKind Kind { get; }

		public override string ToString() => $"{Source} -> {Target}";
	}
}