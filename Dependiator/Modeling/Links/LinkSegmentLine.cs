using System.Windows;


namespace Dependiator.Modeling.Links
{
	internal class LinkSegmentLine
	{
		public static LinkSegmentLine Empty = new LinkSegmentLine(Rect.Empty, new Point(0, 0), new Point(0, 0));

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