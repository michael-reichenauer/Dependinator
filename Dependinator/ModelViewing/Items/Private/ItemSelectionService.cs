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
		public ISelectableItem SelectedItem { get; private set; }

		public bool IsNodeSelected => SelectedItem is NodeViewModel;


		public void Deselect()
		{
			if (SelectedItem == null)
			{
				return;
			}

			SelectedItem.IsSelected = false;
			SelectedItem = null;
		}


		public void Select(LineViewModel clickedLine)
		{
			if (clickedLine == SelectedItem)
			{
				// User clicked om selected line
				Deselect();
				return;
			}

			Deselect();

			SelectedItem = clickedLine;
			SelectedItem.IsSelected = true;
		}



		public void Select(NodeViewModel clickedNode)
		{
			if (clickedNode == SelectedItem)
			{
				// User clicked om selected node
				Deselect();
				return;
			}

			if (SelectedItem is LineViewModel selectedLine &&
			    selectedLine.Line.Owner.AncestorsAndSelf().Any(ancestor => clickedNode == ancestor.View.ViewModel))
			{
				// Ancestor or root was clicked, just deselect line
				Deselect();
				return;
			}

			if (SelectedItem is NodeViewModel selectedNode &&
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

			SelectedItem = clickedNode;
			SelectedItem.IsSelected = true;
		}
	}
}