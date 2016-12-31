using System.Windows;
using Dependiator.MainViews.Private;


namespace Dependiator.MainViews
{
	internal abstract class Item : IItem
	{
		public object VirtualId { get; set; }
		public Rect ItemBounds { get; set; }
		public int ZIndex { get; set; }
		public double Priority { get; set; }
		public abstract object ViewModel { get; }
		public abstract void ZoomChanged();
	}
}