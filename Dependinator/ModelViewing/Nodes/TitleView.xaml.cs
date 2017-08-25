using System.Windows;
using System.Windows.Controls;

namespace Dependinator.ModelViewing.Nodes
{
	/// <summary>
	/// Interaction logic for TitleView.xaml
	/// </summary>
	public partial class TitleView : UserControl
	{
		private NodeViewModel ViewModel => DataContext as NodeViewModel;

		public TitleView()
		{
			InitializeComponent();
		}


		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) =>
			ViewModel?.UpdateToolTip();
	}
}
