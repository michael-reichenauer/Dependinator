using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace Dependinator.ModelViewing.Nodes
{
	public partial class NamespaceView : UserControl
	{
		public NamespaceView()
		{
			InitializeComponent();
		}


		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) =>
			(DataContext as CompositeNodeViewModel)?.UpdateToolTip();

		private void UIElement_OnMouseMove(object sender, MouseEventArgs e) =>
			(DataContext as NodeViewModel)?.OnMouseMove(e);
	}
}
