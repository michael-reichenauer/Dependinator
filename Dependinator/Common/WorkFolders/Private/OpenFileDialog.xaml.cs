using System.Windows;

namespace Dependinator.Common.WorkFolders.Private
{
	/// <summary>
	/// Interaction logic for OpenFileDialog.xaml
	/// </summary>
	public partial class OpenFileDialog : Window
	{
		private readonly OpenFileDialogViewModel viewModel;

		public OpenFileDialog(Window owner)
		{
			Owner = owner;
			InitializeComponent();

			viewModel = new OpenFileDialogViewModel();
			DataContext = viewModel;
			AddTagText.Focus();
		}

		public string TagText => viewModel.TagText;
	}
}
