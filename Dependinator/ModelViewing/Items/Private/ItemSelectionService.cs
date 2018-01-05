using Dependinator.Utils;


namespace Dependinator.ModelViewing.Items.Private
{

	[SingleInstance]
	internal class ItemSelectionService : IItemSelectionService
	{
		private ISelectableItem selectedItem;


		public void Deselect() => DeselectItem();


		public void Select(ISelectableItem clickedItem)
		{
			if (selectedItem != null)
			{
				DeselectItem();
				return;
			}

			DeselectItem();

			SelectItem(clickedItem);
		}


		private void SelectItem(ISelectableItem clickedItem)
		{
			selectedItem = clickedItem;
			selectedItem.IsSelected = true;
		}


		private void DeselectItem()
		{
			if (selectedItem != null)
			{
				selectedItem.IsSelected = false;
				selectedItem = null;
			}
		}


		//if (clickedNode == selectedNode)
		//{
		//	// User clicked om selected node
		//	DeselectSelectedItem();
		//	return;
		//}

		//if (selectedNode != null && 
		//    selectedNode.Node.Ancestors().Any(ancestor => clickedNode == ancestor.View.ViewModel))
		//{
		//	// Deselect all (root was clicked or some ancestor)
		//	DeselectSelectedItem();
		//	return;
		//}
	}
}