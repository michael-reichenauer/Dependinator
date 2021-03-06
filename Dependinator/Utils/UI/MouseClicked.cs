using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;


namespace Dependinator.Utils.UI
{
    internal class MouseClicked
    {
        private readonly Stopwatch clickedStopwatch = new Stopwatch();
        private readonly Action<MouseButtonEventArgs> onMouseClicked;
        private Point mouseDownPoint;


        public MouseClicked(UIElement uiElement, Action<MouseButtonEventArgs> onMouseClicked)
        {
            this.onMouseClicked = onMouseClicked;
            uiElement.MouseDown += (s, e) => MouseDown(e);
            uiElement.MouseUp += (s, e) => MouseUp(e);
        }


        private void MouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                clickedStopwatch.Restart();
                mouseDownPoint = e.GetPosition(Application.Current.MainWindow);
            }
        }


        private void MouseUp(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 1)
                {
                    if ((mouseDownPoint - e.GetPosition(Application.Current.MainWindow)).Length < 5 &&
                        clickedStopwatch.ElapsedMilliseconds < 200)
                    {
                        onMouseClicked(e);
                    }
                }
            }
        }
    }
}
