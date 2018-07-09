using System.Windows;
using Dependinator.ModelViewing.Private.ItemsViewing;
using Dependinator.ModelViewing.Private.Nodes;


namespace Dependinator.ModelViewing.Private.ModelHandling.Core
{
	internal class NodeViewData
	{
		private readonly Node node;


		public NodeViewData(Node node)
		{
			this.node = node;
		}


		public ItemsCanvas ItemsCanvas { get; set; }
		public NodeViewModel ViewModel { get; set; }

		public Rect Bounds { get; set; }
		public double ScaleFactor { get; set; }
		public string Color { get; set; }
		public bool IsHidden { get; set; }


		public bool IsLayoutCompleted { get; set; }
		public bool CanShowChildren => node.IsRoot || (ViewModel?.CanShowChildren ?? false);
		public bool CanShow => ViewModel?.CanShow ?? false;
		public bool IsShowing => ViewModel?.IsShowing ?? false;
	}
}