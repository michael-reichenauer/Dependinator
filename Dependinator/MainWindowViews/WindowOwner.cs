using System;
using System.Windows;
using Dependinator.Utils;


namespace Dependinator.MainWindowViews
{
	[SingleInstance]
	internal class WindowOwner
	{
		private readonly Lazy<MainWindow> mainWindow;


		public WindowOwner(Lazy<MainWindow> mainWindow)
		{
			this.mainWindow = mainWindow;
		}

		public static implicit operator Window(WindowOwner owner) => owner.Window;


		public Window Window
		{
			get
			{
				if (Application.Current?.MainWindow is MainWindow)
				{
					return mainWindow.Value;
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