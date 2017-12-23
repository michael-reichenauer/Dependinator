using System.Windows;
using Dependinator.ModelViewing.Items;


namespace Dependinator.ModelViewing.Nodes
{
	internal class NodePointsView2Model : ItemViewModel
	{
		private readonly NodeViewModel nodeViewModel;


		public NodePointsView2Model(NodeViewModel nodeViewModel)
		{
			this.nodeViewModel = nodeViewModel;

			ItemBounds = nodeViewModel.ItemBounds;
		}

		public override bool CanShow => true;


		public void MouseMove(Point point, bool b)
		{
		}


		public void MouseDown(Point point)
		{
		}


		public void MouseUp(Point point)
		{
		}


		public void OnMouseEnter(bool b)
		{
		}


		public void OnMouseLeave()
		{
			

		}


		public void UpdateToolTip()
		{
			

		}
	}
}