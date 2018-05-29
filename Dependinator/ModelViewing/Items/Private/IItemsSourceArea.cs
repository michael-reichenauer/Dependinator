using System.Windows;


namespace Dependinator.ModelViewing.Items.Private
{
	internal interface IItemsSourceArea
	{
		bool IsRoot { get; }
		Rect GetHierarchicalVisualArea();
	}
}