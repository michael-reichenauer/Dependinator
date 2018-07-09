using Dependinator.ModelViewing.Private.Lines;
using Dependinator.ModelViewing.Private.Nodes;


namespace Dependinator.ModelViewing.Private.Items
{
	internal interface IItemSelectionService
	{
		void Select(NodeViewModel item);

		void Select(LineViewModel clickedItem);

		void Deselect();
		bool IsNodeSelected { get; }
		ISelectableItem SelectedItem { get; }
	}
}