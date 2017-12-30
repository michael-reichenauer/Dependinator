using System;
using System.Windows;
using System.Windows.Input;


namespace Dependinator.Utils.UI
{
	internal static class MouseHelper
	{
		private static Point? lastMousePoint;

		public static void OnLeftButtonMove(
			UIElement element, 
			MouseEventArgs e, 
			Action<UIElement, Vector> moveAction)
		{
			Point viewPosition = e.GetPosition(Application.Current.MainWindow);

			if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				element.CaptureMouse();

				if (lastMousePoint.HasValue)
				{
					Vector viewOffset = viewPosition - lastMousePoint.Value;

					moveAction(element, viewOffset);
				}

				lastMousePoint = viewPosition;

				e.Handled = true;
			}
			else
			{
				element.ReleaseMouseCapture();
				lastMousePoint = null;
			}
		}
	}
}