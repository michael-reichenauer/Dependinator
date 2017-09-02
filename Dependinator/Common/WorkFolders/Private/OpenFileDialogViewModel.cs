using System.Windows;
using Dependinator.Utils.UI;

namespace Dependinator.Common.WorkFolders.Private
{
	internal class OpenFileDialogViewModel : ViewModel
	{
		public Command<Window> OkCommand => Command<Window>(SetOK);
		public Command<Window> CancelCommand => Command<Window>(w => w.DialogResult = false);


		public string TagText
		{
			get => Get();
			set => Set(value).Notify(nameof(OkCommand));
		}


		private void SetOK(Window window)
		{
			if (string.IsNullOrEmpty(TagText))
			{
				return;
			}

			window.DialogResult = true;
		}
	}
}
