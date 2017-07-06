using System.Windows;
using UserControl = System.Windows.Controls.UserControl;


namespace Dependinator.ModelViewing
{
	/// <summary>
	/// Interaction logic for RootModelView.xaml
	/// </summary>
	public partial class RootModelView : UserControl
	{
		private RootModelViewModel viewModel;


		public RootModelView()
		{
			InitializeComponent();
		}


		private async void MainView_OnLoaded(object sender, RoutedEventArgs e)
		{
			viewModel = (RootModelViewModel)DataContext;
			ItemsView.SetFocus();
			await viewModel.LoadAsync();
		}
	}
}
