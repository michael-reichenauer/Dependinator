using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Nodes
{
	/// <summary>
	/// Interaction logic for NodePointsView.xaml
	/// </summary>
	public partial class NodePointsView2 : UserControl
	{
		private NodePointsView2Model ViewModel => DataContext as NodePointsView2Model;
		private readonly Dictionary<string, NodeControl> points;
		private MouseClicked mouseClicked;
		private Point lastMousePoint;


		public NodePointsView2()
		{
			InitializeComponent();

			mouseClicked = new MouseClicked(ControlCenter, Clicked);
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

			

		private void Control_OnMouseMove(object sender, MouseEventArgs e)
		{
			Point viewPosition = e.GetPosition(Application.Current.MainWindow);
			Rectangle rectangle = (Rectangle)sender;

			if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				rectangle.CaptureMouse();
				Vector viewOffset = viewPosition - lastMousePoint;
				
				NodeControl control = points[rectangle.Name];
				ViewModel?.Move(control, viewOffset);

				e.Handled = true;
			}
			else
			{
				rectangle.ReleaseMouseCapture();
			}

			lastMousePoint = viewPosition;
		}
	}
}