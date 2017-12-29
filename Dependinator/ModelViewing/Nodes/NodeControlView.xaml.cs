using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Nodes
{
	/// <summary>
	/// Interaction logic for NodeControlView.xaml
	/// </summary>
	public partial class NodeControlView : UserControl
	{
		private NodeControlViewModel ViewModel => DataContext as NodeControlViewModel;
		private readonly Dictionary<string, NodeControl> points;
		private MouseClicked mouseClicked;
		private MouseClicked mouseClickedEdit;
		private Point? lastMousePoint;


		public NodeControlView()
		{
			InitializeComponent();

			mouseClicked = new MouseClicked(ControlCenter, Clicked);
			mouseClickedEdit = new MouseClicked(EditNode, ClickedEdit);

			points = new Dictionary<string, NodeControl>
			{
				{ControlCenter.Name, NodeControl.Center},
				{ControlLeftTop.Name, NodeControl.LeftTop},
				{ControlLeftBottom.Name, NodeControl.LeftBottom},
				{ControlRightTop.Name, NodeControl.RightTop},
				{ControlRightBottom.Name, NodeControl.RightBottom},

				{ControlTop.Name, NodeControl.Top},
				{ControlLeft.Name, NodeControl.Left},
				{ControlRight.Name, NodeControl.Right},
				{ControlBottom.Name, NodeControl.Bottom},
			};
		}


		private void Clicked(MouseButtonEventArgs e) => ViewModel.Clicked(e);
		private void ClickedEdit(MouseButtonEventArgs e) => ViewModel.ClickedEditNode(e);

		protected override void OnMouseWheel(MouseWheelEventArgs e) => ViewModel.OnMouseWheel(this, e);


		private void Control_OnMouseMove(object sender, MouseEventArgs e)
		{
			Point viewPosition = e.GetPosition(Application.Current.MainWindow);
			Rectangle rectangle = (Rectangle)sender;

			if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				rectangle.CaptureMouse();

				if (lastMousePoint.HasValue)
				{
					Vector viewOffset = viewPosition - lastMousePoint.Value;

					Move(rectangle, viewOffset);
				}

				lastMousePoint = viewPosition;

				e.Handled = true;
			}
			else
			{
				rectangle.ReleaseMouseCapture();
				lastMousePoint = null;
			}
		}


		private void Move(Rectangle rectangle, Vector viewOffset)
		{
			NodeControl control = points[rectangle.Name];
			ViewModel?.Move(control, viewOffset);
		}
	}
}