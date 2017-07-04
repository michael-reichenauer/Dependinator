using System.Windows;
using UserControl = System.Windows.Controls.UserControl;


namespace Dependinator.ModelViewing
{
	/// <summary>
	/// Interaction logic for RootNodeView.xaml
	/// </summary>
	public partial class RootNodeView : UserControl
	{
		private RootNodeViewModel viewModel;


		public RootNodeView()
		{
			InitializeComponent();
		}


		private async void MainView_OnLoaded(object sender, RoutedEventArgs e)
		{
			viewModel = (RootNodeViewModel)DataContext;
			ModelView.SetFocus();
			await viewModel.LoadAsync();
		}
	}
}
