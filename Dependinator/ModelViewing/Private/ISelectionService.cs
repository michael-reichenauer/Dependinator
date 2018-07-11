using Dependinator.ModelViewing.Private.Lines;
using Dependinator.ModelViewing.Private.Nodes;


namespace Dependinator.ModelViewing.Private
{
	internal interface ISelectionService
	{
		void Select(NodeViewModel item);

		void Select(LineViewModel clickedItem);

		void Deselect();
	}
}