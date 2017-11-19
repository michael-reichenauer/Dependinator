using System.Windows;


namespace Dependinator.ModelHandling.Private.Items.Private
{
	internal interface IItemsSourceArea
	{
		bool IsRoot { get; }
		Rect GetHierarchicalVisualArea();
	}
}