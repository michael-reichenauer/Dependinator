using System.Collections.Generic;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Core
{
	internal class Line : Equatable<Line>
	{
		public Line(Node source, Node target, Node owner)
		{
			Source = source;
			Target = target;
			Owner = owner;
			View = new LineViewData();

			IsEqualWhenSame(Source, Target);
		}
		

		public int Stamp { get; set; }

		public Node Source { get; }
		public Node Target { get; }
		public Node Owner { get; }

		public LineViewData View { get; }

	
		public int LinkCount { get; set; }

		public List<Link> Links { get; } = new List<Link>();

		public override string ToString() => $"{Source}->{Target}";
	}
}