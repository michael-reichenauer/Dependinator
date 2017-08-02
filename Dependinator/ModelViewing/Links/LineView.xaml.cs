using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Utils;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Links
{
	/// <summary>
	/// Interaction logic for LineView.xaml
	/// </summary>
	public partial class LineView : UserControl
	{
		private readonly DragUiElement dragUiElement;

		private LineViewModel ViewModel => DataContext as LineViewModel;


		public LineView()
		{
			InitializeComponent();

			dragUiElement = new DragUiElement(
				this,
				(p, o) => ViewModel?.MoveLinePoint(p),
				() => Keyboard.Modifiers.HasFlag(ModifierKeys.Control), 
				p => ViewModel?.BeginMoveLinePoint(p), 
				p => ViewModel?.EndMoveLinePoint(p));
		}



		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnter();

		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeave();
	}
}
