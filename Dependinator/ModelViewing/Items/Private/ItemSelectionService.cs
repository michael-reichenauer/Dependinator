using System.Linq;
using Dependinator.ModelViewing.Lines;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Items.Private
{
	[SingleInstance]
	internal class ItemSelectionService : IItemSelectionService
	{
		private ISelectableItem selectedItem;


		public void Deselect()
		{
			if (selectedItem == null)
			{
				return;
			}

			selectedItem.IsSelected = false;
			selectedItem = null;
		}


		public void Select(LineViewModel clickedLine)
		{
			if (clickedLine == selectedItem)
			{
				// User clicked om selected line
				Deselect();
				return;
			}

			Deselect();

			selectedItem = clickedLine;
			selectedItem.IsSelected = true;
		}



		public void Select(NodeViewModel clickedNode)
		{
			if (clickedNode == selectedItem)
			{
				// User clicked om selected node
				Deselect();
				return;
			}

			if (selectedItem is LineViewModel selectedLine &&
				 selectedLine.Line.Owner.AncestorsAndSelf().Any(ancestor => clickedNode == ancestor.View.ViewModel))
			{
				// Ancestor or root was clicked, just deselect line
				Deselect();
				return;
			}

			if (selectedItem is NodeViewModel selectedNode &&
					selectedNode.Node.Ancestors().Any(ancestor => clickedNode == ancestor.View.ViewModel))
			{
				// Ancestor or root was clicked, just deselect node
				Deselect();
				return;
			}

			Deselect();

			if (clickedNode.ItemScale > 7)
			{
				// Do not select node if it larger than mostly visible. I.e. not just seen as background
				return;
			}

			selectedItem = clickedNode;
			selectedItem.IsSelected = true;
		}
	}
}