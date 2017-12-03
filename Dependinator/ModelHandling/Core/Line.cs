using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.Links;
using Dependinator.Utils;


namespace Dependinator.ModelHandling.Core
{
	internal class Line : Equatable<Line>
	{
		private static readonly List<Point> DefaultPoints =
			new List<Point> { new Point(0, 0), new Point(0, 0) };


		public Line(Node source, Node target, Node owner)
		{
			Source = source;
			Target = target;

			Owner = owner;
			IsEqualWhenSame(Source, Target);
		}

		public bool IsShowing => ViewModel?.IsShowing ?? false;

		public int Stamp { get; set; }

		public Node Source { get; }

		public Node Target { get; }

		public Node Owner { get; }

		public Point RelativeSourcePoint { get; set; } = new Point(-1, -1);

		public Point RelativeTargetPoint { get; set; } = new Point(-1, -1);

		public LineViewModel ViewModel { get; set; }

		public int LinkCount { get; set; }

		public List<Link> Links { get; } = new List<Link>();

		public List<Point> Points { get; private set; } = DefaultPoints.ToList();

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


		public void ResetPoints() => Points = DefaultPoints.ToList();
	}
}