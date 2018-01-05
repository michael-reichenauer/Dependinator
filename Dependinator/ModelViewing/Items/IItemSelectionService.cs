namespace Dependinator.ModelViewing.Items
{
	internal interface IItemSelectionService
	{
		void Select(ISelectableItem item);

		void Deselect();
	}
}