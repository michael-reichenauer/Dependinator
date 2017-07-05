using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Dependinator.ModelViewing.Nodes
{
	/// <summary>
	/// Interaction logic for TypeView.xaml
	/// </summary>
	public partial class TypeView : UserControl
	{
		public TypeView()
		{
			InitializeComponent();
		}


		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) =>
			(DataContext as CompositeNodeViewModel)?.UpdateToolTip();

		private void UIElement_OnMouseMove(object sender, MouseEventArgs e) =>
			(DataContext as NodeViewModel)?.OnMouseMove(e);
	}
}
