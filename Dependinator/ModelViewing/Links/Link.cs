using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Links
{
	internal class Link : Equatable<Link>
	{
		public Link(Node source, Node target)
		{
			Source = source;
			Target = target;

			IsEqualWhenSame(source, target);
		}

		public int Stamp { get; set; }
		public Node Target { get; }
		public Node Source { get; }
		public List<Line> Lines { get; } = new List<Line>();

		public override string ToString() => $"{Source}->{Target}";
	}
}