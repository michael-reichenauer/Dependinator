﻿using System.Windows;
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
		private readonly DragUiElement dragUiElement;
		private NodeViewModel ViewModel => DataContext as NodeViewModel;

		public NodePointsView()
		{
			InitializeComponent();

			dragUiElement = new DragUiElement(
				MouseOverBorder,
				(p, o) => ViewModel?.MouseMove(p, false),
				() => Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
				p => ViewModel?.MouseDown(p),
				p => ViewModel?.MouseUp(p));
		}

		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseEnter(false);

		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e) =>
			ViewModel?.OnMouseLeave();


		private void ToolTip_OnOpened(object sender, RoutedEventArgs e) =>
			ViewModel?.UpdateToolTip();
	}
}
