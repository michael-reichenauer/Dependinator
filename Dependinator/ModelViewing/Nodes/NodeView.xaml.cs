using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Dependinator.ModelViewing.Nodes
{
	/// <summary>
	/// Interaction logic for NodeView.xaml
	/// </summary>
	public partial class NodeView : UserControl
	{
		public NodeView()
		{
			InitializeComponent();
		}


		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) =>
			(DataContext as NodeViewModel)?.UpdateToolTip();

		private void UIElement_OnMouseMove(object sender, MouseEventArgs e) =>
			(DataContext as NodeViewModel)?.OnMouseMove(e);
	}
}
