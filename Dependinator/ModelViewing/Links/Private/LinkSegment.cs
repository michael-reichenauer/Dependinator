using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Links.Private
{
	internal class LinkSegment
	{
		public LinkSegment(NodeOld source, NodeOld target, NodeOld owner, LinkOld link)
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