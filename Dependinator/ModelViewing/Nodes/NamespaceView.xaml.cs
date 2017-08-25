using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Nodes
{
	public partial class NamespaceView : UserControl
	{
		private readonly DragUiElement dragUiElement;

		private NodeViewModel ViewModel => DataContext as NodeViewModel;

		public NamespaceView()
		{
			InitializeComponent();

			dragUiElement = new DragUiElement(
				MouseOverBorder,
				(p, o) => ViewModel?.MouseMove(p),
				() => Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
				p => ViewModel?.MouseDown(p),
				p => ViewModel?.MouseUp(p));
		}


		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) =>
			ViewModel?.UpdateToolTip();

		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnter();

		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeave();
	}
}
