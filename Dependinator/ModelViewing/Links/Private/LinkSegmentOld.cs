using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Links.Private
{
	internal class LinkSegment
	{
		public LinkSegment(Node source, Node target, Link link)
		{
			Source = source;
			Target = target;
			Link = link;
		}


		public Node Source { get; }
		public Node Target { get; }
		public Link Link { get; }

		public override string ToString() => $"{Source}->{Target} ({Link})";
	}


	internal class LinkSegmentOld
	{
		public LinkSegmentOld(NodeOld source, NodeOld target, NodeOld owner, LinkOld link)
		{
			Source = source;
			Target = target;
			Owner = owner;
			Link = link;
		}


		public NodeOld Source { get; }
		public NodeOld Target { get; }
		public NodeOld Owner { get; }
		public LinkOld Link { get; }

		public override string ToString() => $"{Source}->{Target} (owner: {Owner}) ({Link})";
	}
}