using System.Collections.Generic;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Core
{
	internal class Line : Equatable<Line>, IEdge
	{
		private List<Link> hiddenLinks = new List<Link>();

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
		public bool IsToChild => Source == Target.Parent;
		public bool IsToParent => Source.Parent == Target;
		public bool IsToSibling => Source.Parent == Target.Parent;

		public override string ToString() => $"{Source}->{Target}";


		public void HideLink(Link link)
		{
			if (Links.Remove(link))
			{
				hiddenLinks.Add(link);
			}
		}
	}
}