using System;
using System.Windows;
using System.Windows.Input;


namespace Dependinator.Utils.UI
{
    internal class DragUiElement
    {
        private readonly Action<MouseEventArgs> begin;
        private readonly Action<MouseEventArgs> end;
        private readonly Action<Vector, MouseEventArgs> move;
        private readonly UIElement uiElement;

        private Point? lastMousePoint;


        public DragUiElement(
            UIElement uiElement,
            Action<Vector, MouseEventArgs> move,
            Action<MouseEventArgs> begin = null,
            Action<MouseEventArgs> end = null)
        {
            this.uiElement = uiElement;

            uiElement.MouseMove += (s, e) => MouseMove(e);
            uiElement.MouseUp += (s, e) => MouseUp(e);
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
                    begin?.Invoke(e);
                }
                else
                {
                    Vector viewOffset = viewPosition - lastMousePoint.Value;

                    move(viewOffset, e);
                }

                lastMousePoint = viewPosition;

                e.Handled = true;
            }
            else
            {
                uiElement.ReleaseMouseCapture();

                if (lastMousePoint.HasValue)
                {
                    end?.Invoke(e);
                    lastMousePoint = null;
                }
            }
        }


        private void MouseUp(MouseButtonEventArgs e)
        {
            uiElement.ReleaseMouseCapture();

            if (lastMousePoint.HasValue)
            {
                end?.Invoke(e);
                lastMousePoint = null;
            }
        }
    }
}
