using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dependinator.Utils;


namespace Dependinator.ModelViewing
{
	/// <summary>
	/// Interaction logic for ModelView.xaml
	/// </summary>
	public partial class ModelView : UserControl
	{
		private ModelViewModel viewModel;


		public ModelView()
		{
			InitializeComponent();
		}


		private async void MainView_OnLoaded(object sender, RoutedEventArgs e)
		{
			viewModel = (ModelViewModel)DataContext;
			ItemsView.SetFocus();
			await viewModel.LoadAsync();
		}


		private async void Dropped_Files(object sender, DragEventArgs e)
		{
			if (!e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				return;
			}

			string[] filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);

			if (filePaths?.Any() ?? false)
			{
				await viewModel.LoadFilesAsync(filePaths);
			}
		}
	}
}
