using System.Collections.Generic;
using System.Windows;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Links
{
	internal class Line : Equatable<Line>
	{
		public Line(Node source, Node target)
		{
			Source = source;
			Target = target;
			IsEqualWhenSame(Source, Target);
		}

		public Node Source { get; }

		public Node Target { get; }

		public Point SourceAdjust { get; set; } = new Point(-1, -1);

		public Point TargetAdjust { get; set; } = new Point(-1, -1);

		public LineViewModel ViewModel { get; set; }

		public List<Link> Links { get; } = new List<Link>();

		public List<Point> Points { get; } = new List<Point> { new Point(0, 0), new Point(0, 0) };

		public Point FirstPoint { get => Points[FirstIndex]; set => Points[FirstIndex] = value; }
		public Point LastPoint { get => Points[LastIndex]; set => Points[LastIndex] = value; }
		public int LastIndex => Points.Count - 1;
		public int FirstIndex => 0;

		public IEnumerable<Point> MiddlePoints()
		{
			for (int i = 1; i < Points.Count - 1; i++)
			{
				yield return Points[i];
			}
		}

		public override string ToString() => $"{Source}->{Target}";
	}
}