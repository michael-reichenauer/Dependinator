using System.Windows;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Open
{
	internal class OpenModelViewModel : ItemViewModel
	{
		public OpenModelViewModel()
		{
			ItemBounds = new Rect(100, 100, 400, 600);
		}
	}
}