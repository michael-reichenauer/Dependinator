using System.Windows;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Private.CodeViewing
{
	internal class CodeViewModel : ViewModel
	{
		public CodeViewModel(string title)
		{
			Title = title;
		}


		public Command<Window> CancelCommand => Command<Window>(w => w.Close());


		public string Title { get => Get(); set => Set(value); }
	}
}