using System.Windows;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.ModelHandling.Core
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
		public Point Offset { get; set; }
		public string Color { get; set; }
		public bool IsHidden { get; set; }


		public bool IsLayoutCompleted { get; set; }
		public bool CanShowChildren => node.IsRoot || ViewModel.CanShowChildren;
		public bool CanShow => ViewModel?.CanShow ?? false;
		public bool IsShowNode => ViewModel?.IsShowNode ?? false;
		public bool IsShowing => ViewModel?.IsShowing ?? false;


		//public void ShowHiddenNode()
		//{
		//	IsHidden = false;
		//	node.Parent.View.ItemsCanvas?.UpdateAndNotifyAll();
		//}

	}
}