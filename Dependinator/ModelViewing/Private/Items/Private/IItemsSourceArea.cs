using System.Windows;

namespace Dependinator.ModelViewing.Private.Items.Private
{
	internal interface IItemsSourceArea
	{
		bool IsRoot { get; }
		Rect GetHierarchicalVisualArea();
	}
}