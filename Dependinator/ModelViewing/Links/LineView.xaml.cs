﻿using System.Windows;
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
		private MouseClicked lineClicked;
		private readonly DragUiElement dragLine;
		private readonly DragUiElement dragLineControlPoints;

		private LineViewModel ViewModel => DataContext as LineViewModel;


		public LineView()
		{
			InitializeComponent();

			lineClicked = new MouseClicked(this, Clicked);

			dragLine = new DragUiElement(
				LineControl,
				(p, o) => ViewModel?.MouseMove(p),
				p => ViewModel?.MouseDown(p, false),
				p => ViewModel?.MouseUp(p));

			dragLineControlPoints = new DragUiElement(
				LinePoints,
				(p, o) => ViewModel?.MouseMove(p),
				p => ViewModel?.MouseDown(p, true),
				p => ViewModel?.MouseUp(p));
		}


		private void Clicked(MouseButtonEventArgs e) => ViewModel?.Clicked();


		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnter();

		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeave();


		protected override void OnMouseWheel(MouseWheelEventArgs e) => ViewModel.OnMouseWheel(this, e);

		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) => ViewModel?.UpdateToolTip();
	}
}
