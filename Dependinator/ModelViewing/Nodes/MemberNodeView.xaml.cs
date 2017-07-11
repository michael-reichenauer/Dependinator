using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace Dependinator.ModelViewing.Nodes
{
	/// <summary>
	/// Interaction logic for MemberNodeView.xaml
	/// </summary>
	public partial class MemberNodeView : UserControl
	{
		public MemberNodeView()
		{
			InitializeComponent();
		}

		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) =>
			(DataContext as NodeOldViewModel)?.UpdateToolTip();

		private void UIElement_OnMouseMove(object sender, MouseEventArgs e) =>
			(DataContext as NodeOldViewModel)?.OnMouseMove(e);
	}
}
