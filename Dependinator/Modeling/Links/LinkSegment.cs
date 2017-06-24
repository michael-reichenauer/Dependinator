using Dependinator.Modeling.Nodes;


namespace Dependinator.Modeling.Links
{
	internal class LinkSegment
	{
		public LinkSegment(Node source, Node target, Node owner, Link link)
		{
			Source = source;
			Target = target;
			Owner = owner;
			Link = link;
		}


		public Node Source { get; }
		public Node Target { get; }
		public Node Owner { get; }
		public Link Link { get; }

		public override string ToString() => $"{Source}->{Target} (owner: {Owner}) ({Link})";
	}
}