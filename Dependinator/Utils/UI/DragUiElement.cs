using System;
using System.Windows;
using System.Windows.Input;


namespace Dependinator.Utils.UI
{
	internal class DragUiElement
	{
		private readonly UIElement uiElement;
		private readonly Action<Point> begin;
		private readonly Action<Point, Vector> move;
		private readonly Action<Point> end;

		private Point? lastMousePoint;


		public DragUiElement(
			UIElement uiElement,
			Action<Point, Vector> move,
			Action<Point> begin = null,
			Action<Point> end = null)
		{
			this.uiElement = uiElement;
		
			uiElement.MouseMove += (s, e) => MouseMove(e);
			
			this.begin = begin;
			this.move = move;
			this.end = end;
		}


		public void MouseMove(MouseEventArgs e)
		{
			Point viewPosition = e.GetPosition(Application.Current.MainWindow);

			if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				uiElement.CaptureMouse();

				if (!lastMousePoint.HasValue)
				{
					begin?.Invoke(viewPosition);
				}
				else
				{
					Vector viewOffset = viewPosition - lastMousePoint.Value;

					move(viewPosition, viewOffset);
				}

				lastMousePoint = viewPosition;

				e.Handled = true;
			}
			else
			{
				uiElement.ReleaseMouseCapture();

				if (lastMousePoint.HasValue)
				{
					end?.Invoke(viewPosition);
					lastMousePoint = null;
				}
			}
		}
	}
}
	