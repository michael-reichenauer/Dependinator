using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Utils.UI;


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
			ViewModel?.OnMouseEnter(false);

		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeave();

		private void UIElement_OnMouseEnterLeftTop(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnterPoint(1);

		private void UIElement_OnMouseLeaveLeftTop(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeavePoint(1);

		private void UIElement_OnMouseEnterRightTop(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnterPoint(2);

		private void UIElement_OnMouseLeaveRightTop(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeavePoint(2);

		private void UIElement_OnMouseEnterRightBottom(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnterPoint(3);

		private void UIElement_OnMouseLeaveRightBottom(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeavePoint(3);

		private void UIElement_OnMouseEnterLeftBottom(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnterPoint(4);

		private void UIElement_OnMouseLeaveLeftBottom(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeavePoint(4);

	

		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) =>
			ViewModel?.UpdateToolTip();
	}
}
