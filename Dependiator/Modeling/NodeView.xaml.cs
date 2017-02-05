using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Dependiator.Modeling
{
	/// <summary>
	/// Interaction logic for NodeView.xaml
	/// </summary>
	public partial class NodeView : UserControl
	{
		//private Point lastMousePosition;
		//private bool isMoving = false;

		public NodeView()
		{
			InitializeComponent();
		}


		//private void UIElement_OnMouseMove(object sender, MouseEventArgs e)
		//{
		//	ModuleViewModel viewModel = DataContext as ModuleViewModel;
		//	if (viewModel == null)
		//	{
		//		return;
		//	}

		//	Point viewPosition = e.GetPosition(sender as IInputElement);

		//	if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
		//	 && e.LeftButton == MouseButtonState.Pressed)
		//	{
		//		CaptureMouse();
		//		bool isFirst = isMoving == false;
		//		isMoving = true;

		//		Vector viewOffset = viewPosition - lastMousePosition;
		//		viewModel.MouseMove(viewPosition, viewOffset, isFirst);

		//		//movingObject = viewModel.MoveItem(viewPosition, viewOffset, movingObject);

		//		//e.Handled = true;
		//		//e.Handled = movingObject != null;
		//	}
		//	else
		//	{
		//		//movingObject = null;
		//		isMoving = false;
		//		ReleaseMouseCapture();
		//	}

		//	lastMousePosition = viewPosition;
		//}
	}
}
