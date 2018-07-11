using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace Dependinator.ModelViewing.Private.Nodes
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


		private void TitleBorderHorizontal_OnMouseEnter(object sender, MouseEventArgs e)
		{
			ViewModel?.MouseEnterTitle();
		}


		private void TitleBorderHorizontal_OnMouseLeave(object sender, MouseEventArgs e)
		{
			ViewModel?.MouseExitTitle();
		}
	}
}
