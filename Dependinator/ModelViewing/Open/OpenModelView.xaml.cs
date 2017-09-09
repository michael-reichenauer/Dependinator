using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;


namespace Dependinator.ModelViewing.Open
{
	/// <summary>
	/// Interaction logic for OpenModelView.xaml
	/// </summary>
	public partial class OpenModelView : UserControl
	{
		private OpenModelViewModel ViewModel => DataContext as OpenModelViewModel;

		public OpenModelView()
		{
			InitializeComponent();
		}


		private void OpenFile_OnClick(object sender, RoutedEventArgs e)
		{
			ViewModel?.OpenFile();
		}

		private void RecentFile_OnClick(object sender, RoutedEventArgs e)
		{
			((sender as Hyperlink)?.DataContext as FileItem)?.OpenFileCommand.Execute();
		}
	}
}
