using System;
using System.Windows;
using System.Windows.Input;

namespace Dependinator.Utils.UI
{
	internal class DragUiElement
	{
		private readonly UIElement uiElement;
		private readonly Func<bool> predicate;
		private readonly Action<Point> begin;
		private readonly Action<Point, Vector> move;
		private readonly Action<Point> end;

		private Point lastMousePoint;
		private bool IsMouseDown;


		public DragUiElement(
			UIElement uiElement, 
			Action<Point, Vector> move, 
			Func<bool> predicate = null, 
			Action<Point> begin = null, 
			Action<Point> end = null,
			bool isPreview = false)
		{
			this.uiElement = uiElement;
			if (isPreview)
			{
				uiElement.PreviewMouseDown += (s, e) => MouseDown(e);
				uiElement.PreviewMouseUp += (s, e) => MouseUp(e);
				uiElement.PreviewMouseMove += (s, e) => MouseMove(e);
			}
			else
			{
				uiElement.MouseDown += (s, e) => MouseDown(e);
				uiElement.MouseUp += (s, e) => MouseUp(e);
				uiElement.MouseMove += (s, e) => MouseMove(e);
			}
			

			this.predicate = predicate;
			this.begin = begin;
			this.move = move;
			this.end = end;
		}


		private void MouseDown(MouseButtonEventArgs e)
		{
			if (predicate?.Invoke() ?? true)
			{
				uiElement.CaptureMouse();

				Point viewPosition = e.GetPosition(Application.Current.MainWindow);

				lastMousePoint = viewPosition;
				begin?.Invoke(viewPosition);
				IsMouseDown = true;
				e.Handled = true;
			}

		}


		private void MouseUp(MouseButtonEventArgs e)
		{
			if (IsMouseDown)
			{
				IsMouseDown = false;
				e.Handled = true;

				Point viewPosition = e.GetPosition(Application.Current.MainWindow);

				end?.Invoke(viewPosition);
				uiElement.ReleaseMouseCapture();			
			}
		}


		private void MouseMove(MouseEventArgs e)
		{
			if (IsMouseDown)
			{
				Point viewPosition = e.GetPosition(Application.Current.MainWindow);
				Vector viewOffset = viewPosition - lastMousePoint;

				move?.Invoke(viewPosition, viewOffset);

				lastMousePoint = viewPosition;
				e.Handled = true;
			}
		}
	}
}