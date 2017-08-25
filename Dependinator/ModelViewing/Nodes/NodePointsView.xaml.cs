using System.Windows.Controls;
using System.Windows.Input;

namespace Dependinator.ModelViewing.Nodes
{
	/// <summary>
	/// Interaction logic for NodePointsView.xaml
	/// </summary>
	public partial class NodePointsView : UserControl
	{
		private NodeViewModel ViewModel => DataContext as NodeViewModel;

		public NodePointsView()
		{
			InitializeComponent();
		}

		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnter();

		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeave();
	}
}
