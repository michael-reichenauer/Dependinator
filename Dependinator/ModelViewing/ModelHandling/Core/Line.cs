using System.Collections.Generic;
using System.Linq;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Core
{
	internal class Line : Equatable<Line>, IEdge
	{
		public Line(Node source, Node target)
		{
			Source = source;
			Target = target;
			View = new LineViewData();

			IsEqualWhenSame(Source, Target);
		}
		

		public int Stamp { get; set; }

		public Node Source { get; }
		public Node Target { get; }
		public Node Owner => Source == Target.Parent ? Source : Source.Parent;

		public LineViewData View { get; }

	
		public int LinkCount { get; set; }
		public int LinkCount2 => Links.Any() ? Links.Count : LinkCount;

		public List<Link> Links { get; } = new List<Link>();
		public bool IsToChild => Source == Target.Parent;
		public bool IsToParent => Source.Parent == Target;
		public bool IsToSibling => Source.Parent == Target.Parent;

		public override string ToString() => $"{Source}->{Target}";

		public bool IsHidden => Links.Any() && Links.All(link => link.IsHidden);
		public int VisibleLinksCount => Links.Count(link => !link.IsHidden);
	}
}