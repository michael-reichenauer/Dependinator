using System.Windows;
using UserControl = System.Windows.Controls.UserControl;


namespace Dependinator.MainViews
{
	/// <summary>
	/// Interaction logic for MainView.xaml
	/// </summary>
	public partial class MainView : UserControl
	{
		private MainViewModel viewModel;


		public MainView()
		{
			InitializeComponent();
		}


		private async void MainView_OnLoaded(object sender, RoutedEventArgs e)
		{
			viewModel = (MainViewModel)DataContext;
			ModelView.SetFocus();
			await viewModel.LoadAsync();
		}
	}
}
