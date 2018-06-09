using System;
using System.Windows;
using Dependinator.Utils.Dependencies;


namespace Dependinator.Common
{
	[SingleInstance]
	internal class WindowOwner
	{
		public static implicit operator Window(WindowOwner owner) => owner.Window;


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


		public System.Windows.Forms.IWin32Window Win32Window
		{
			get
			{
				var source = PresentationSource.FromVisual(Window) as System.Windows.Interop.HwndSource;
				System.Windows.Forms.IWin32Window win = new Win32WindowHandle(source.Handle);
				return win;
			}
		}

		private class Win32WindowHandle : System.Windows.Forms.IWin32Window
		{
			private readonly IntPtr _handle;
			public Win32WindowHandle(IntPtr handle)
			{
				_handle = handle;
			}

			IntPtr System.Windows.Forms.IWin32Window.Handle => _handle;
		}
	}
}