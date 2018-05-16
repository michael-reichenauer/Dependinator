using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.CodeViewing
{
	internal class CodeViewModel : ViewModel
	{
		public Command<Window> CancelCommand => Command<Window>(w => w.Close());


		public CodeViewModel(Node node)
		{
			Title = $"{node.Name.DisplayFullName}";
		}


		public string Title { get => Get(); set => Set(value); }
	}
}