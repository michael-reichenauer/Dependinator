using System.Windows;


namespace Dependinator.ModelViewing.Private.ItemsViewing
{
    internal interface IItemsCanvasOwner
    {
        Rect ItemBounds { get; }

        bool IsShowing { get; }

        bool CanShow { get; }
    }
}
