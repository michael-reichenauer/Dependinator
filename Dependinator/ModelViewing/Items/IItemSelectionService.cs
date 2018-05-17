using Dependinator.ModelViewing.Lines;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.Items
{
	internal interface IItemSelectionService
	{
		void Select(NodeViewModel item);

		void Select(LineViewModel clickedItem);

		void Deselect();
	}
}