using System.Collections.ObjectModel;
using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ReferencesViewing;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.DecompilationViewing
{
	internal class CodeViewModel : ViewModel
	{

		public Command<Window> OkCommand => Command<Window>(SetOK);

		public Command<Window> CancelCommand => Command<Window>(w => w.DialogResult = false);


		public CodeViewModel(Node node)
		{
			Title = $"{node.Name.DisplayFullName}";
		}



		public string Title { get => Get(); set => Set(value); }

		
		private void SetOK(Window window) => window.DialogResult = true;

	}
}