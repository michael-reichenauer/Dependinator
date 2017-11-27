﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Nodes
{
	/// <summary>
	/// Interaction logic for TitleView.xaml
	/// </summary>
	public partial class TitleView : UserControl
	{
		private readonly DragUiElement dragUiElementHorizontal;
		private readonly DragUiElement dragUiElementVertical;
		private NodeViewModel ViewModel => DataContext as NodeViewModel;

		public TitleView()
		{
			InitializeComponent();

			dragUiElementHorizontal = new DragUiElement(
				TitleBorderHorizontal,
				(p, o) => ViewModel?.MouseMove(p, true),
				() => Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
				p => ViewModel?.MouseDown(p),
				p => ViewModel?.MouseUp(p));

			dragUiElementVertical = new DragUiElement(
				TitleBorderHorizontal,
				(p, o) => ViewModel?.MouseMove(p, true),
				() => Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
				p => ViewModel?.MouseDown(p),
				p => ViewModel?.MouseUp(p));
		}


		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) =>
			ViewModel?.UpdateToolTip();


		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnter(true);

		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeave();
	}
}
