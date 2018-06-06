﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Lines
{
	/// <summary>
	/// Interaction logic for LineView.xaml
	/// </summary>
	public partial class LineView : UserControl
	{
		private MouseClicked lineClicked;
		// private MouseClicked toggleClicked;
		private readonly DragUiElement dragLine;
		private readonly DragUiElement dragLineControlPoints;

		private LineViewModel ViewModel => DataContext as LineViewModel;


		public LineView()
		{
			InitializeComponent();

			lineClicked = new MouseClicked(this, Clicked);
			//toggleClicked = new MouseClicked(ToggleLine, ClickToggleLine);

			dragLine = new DragUiElement(
				LineControl,
				(o, e) => ViewModel?.LineControl.MouseMove(false, e),
				e => ViewModel?.LineControl.MouseDown(e),
				e => ViewModel?.LineControl.MouseUp(e));

			dragLineControlPoints = new DragUiElement(
				LinePoints,
				(o, e) => ViewModel?.LineControl.MouseMove(true, e),
				e => ViewModel?.LineControl.MouseDown(e),
				e => ViewModel?.LineControl.MouseUp(e));
		}


		private void Clicked(MouseButtonEventArgs e) => ViewModel?.Clicked();
		private void ClickToggleLine(MouseButtonEventArgs e) => ViewModel?.Toggle();


		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnter();

		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeave();


		protected override void OnMouseWheel(MouseWheelEventArgs e) => ViewModel.OnMouseWheel(this, e);

		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) => ViewModel?.UpdateToolTip();
	}
}