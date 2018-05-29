using System.Windows;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.CodeViewing
{
	internal class CodeViewModel : ViewModel
	{
		public Command<Window> CancelCommand => Command<Window>(w => w.Close());


		public CodeViewModel(string title)
		{
			Title = $"{title}";
		}


		public string Title { get => Get(); set => Set(value); }
	}
}