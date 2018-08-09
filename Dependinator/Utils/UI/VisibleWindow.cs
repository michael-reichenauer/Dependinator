using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using DrawingPoint = System.Drawing.Point;


namespace Dependinator.Utils.UI
{
    public class VisibleWindow
    {
        public static bool IsVisibleOnAnyScreen(Rectangle rect)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(rect) && screen.WorkingArea.Top < rect.Top)
                {
                    return true;
                }
            }

            return false;
        }


        public static bool IsVisible(Window window)
        {
            WindowInteropHelper nativeWindow = new WindowInteropHelper(window);

            int testCount = 4;
            int xStep = (int)(window.Width / (testCount + 1));
            int yStep = (int)(window.Height / (testCount + 1));

            for (int i = 1; i < testCount - 1; i++)
            {
                for (int j = 1; j < testCount - 1; j++)
                {
                    int x = (int)(window.Left + j * xStep);
                    int y = (int)(window.Top + i * yStep);

                    DrawingPoint testPoint = new DrawingPoint(x, y);
                    if (nativeWindow.Handle != WindowFromPoint(testPoint))
                    {
                        return false;
                    }
                }
            }

            return true;
        }


        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(DrawingPoint lpPoint);
    }
}
