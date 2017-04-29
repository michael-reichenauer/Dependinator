using System.Windows;


namespace Dependiator.Modeling
{
	internal class LinkSegmentLine
	{
		public LinkSegmentLine(Rect itemBounds, Point source, Point target)
		{
			ItemBounds = itemBounds;
			Source = source;
			Target = target;
		}


		public Rect ItemBounds { get; }

		public Point Source { get; }

		public Point Target { get; }
	}
}