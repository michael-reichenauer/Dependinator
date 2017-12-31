using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Links
{
	/// <summary>
	/// Interaction logic for LineView.xaml
	/// </summary>
	public partial class LineView : UserControl
	{
		//private readonly DragUiElement dragUiElement;
		//private readonly DragUiElement dragUiElementPoints;


		private LineViewModel ViewModel => DataContext as LineViewModel;
		private MouseClicked mouseClicked;

		public LineView()
		{
			InitializeComponent();
			mouseClicked = new MouseClicked(this, Clicked);
			//dragUiElement = new DragUiElement(
			//	this,
			//	(p, o) => ViewModel?.MouseMove(p),
			//	() => Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
			//	p => ViewModel?.MouseDown(p, false),
			//	p => ViewModel?.MouseUp(p));

			//dragUiElementPoints = new DragUiElement(
			//	LinePoints,
			//	(p, o) => ViewModel?.MouseMove(p),
			//	() => Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
			//	p => ViewModel?.MouseDown(p, true),
			//	p => ViewModel?.MouseUp(p));


		}


		private void Clicked(MouseButtonEventArgs obj)
		{
			throw new System.NotImplementedException();
		}


		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnter(false);

		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeave(false);


		private void UIElement_OnMouseEnterPoint(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnter(true);

		private void UIElement_OnMouseLeavePoint(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeave(true);

		protected override void OnMouseWheel(MouseWheelEventArgs e) => ViewModel.OnMouseWheel(this, e);

		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) => ViewModel?.UpdateToolTip();
	}
}
