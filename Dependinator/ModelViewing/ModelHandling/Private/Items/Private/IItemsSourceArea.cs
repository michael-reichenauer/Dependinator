using System.Windows;


namespace Dependinator.ModelViewing.ModelHandling.Private.Items.Private
{
	internal interface IItemsSourceArea
	{
		bool IsRoot { get; }
		Rect GetHierarchicalVisualArea();
	}
}