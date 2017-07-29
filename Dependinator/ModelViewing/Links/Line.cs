using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Links
{
	internal class Line : Equatable<Line>
	{
		public Node Source { get; }
		public Node Target { get; }

		public Line(Node source, Node target)
		{
			Source = source;
			Target = target;
			IsEqualWhenSame(Source, Target);
		}

		public List<Link> Links { get; } = new List<Link>();
	}
}