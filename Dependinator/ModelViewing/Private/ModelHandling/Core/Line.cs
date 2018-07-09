using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.ModelHandling.Core
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

		public bool IsToChild => Source == Target.Parent;
		public bool IsToParent => Source.Parent == Target;
		public bool IsToSibling => Source.Parent == Target.Parent;

		public override string ToString() => $"{Source}->{Target}";

		public bool IsHidden => Source.View.IsHidden || Target.View.IsHidden;
	}
}