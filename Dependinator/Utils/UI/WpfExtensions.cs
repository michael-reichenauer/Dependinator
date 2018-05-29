// ReSharper disable once CheckNamespace
namespace System.Windows.Media
{
	public static class WpfExtensions
	{
		public static Forms.IWin32Window GetIWin32Window(this Visual visual)
		{
			var source = PresentationSource.FromVisual(visual) as Interop.HwndSource;
			Forms.IWin32Window win = new Win32Window(source.Handle);
			return win;
		}


		private class Win32Window : Forms.IWin32Window
		{
			private readonly IntPtr handle;
			public Win32Window(IntPtr handle) => this.handle = handle;

			IntPtr Forms.IWin32Window.Handle => handle;
		}
	}
}