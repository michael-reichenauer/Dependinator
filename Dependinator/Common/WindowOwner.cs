using System;
using System.Windows;
using System.Windows.Interop;
using Dependinator.Utils.Dependencies;
using IWin32Window = System.Windows.Forms.IWin32Window;


namespace Dependinator.Common
{
    [SingleInstance]
    internal class WindowOwner
    {
        public Window Window
        {
            get
            {
                if (Application.Current?.MainWindow is IMainWindow)
                {
                    return Application.Current?.MainWindow;
                }

                return null;
            }
        }


        public IWin32Window Win32Window
        {
            get
            {
                var source = PresentationSource.FromVisual(Window) as HwndSource;
                IWin32Window win = new Win32WindowHandle(source.Handle);
                return win;
            }
        }

        public static implicit operator Window(WindowOwner owner) => owner.Window;


        private class Win32WindowHandle : IWin32Window
        {
            private readonly IntPtr _handle;


            public Win32WindowHandle(IntPtr handle)
            {
                _handle = handle;
            }


            IntPtr IWin32Window.Handle => _handle;
        }
    }
}
