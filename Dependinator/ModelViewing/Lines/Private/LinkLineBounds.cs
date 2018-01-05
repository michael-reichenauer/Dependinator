using System.Windows;


namespace Dependinator.ModelViewing.Lines.Private
{
	internal class LinkLineBounds
	{
		public static LinkLineBounds Empty = new LinkLineBounds(Rect.Empty, new Point(0, 0), new Point(0, 0));

		public LinkLineBounds(Rect itemBounds, Point source, Point target)
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