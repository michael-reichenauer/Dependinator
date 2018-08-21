using System.Windows.Interop;
using IWin32Window = System.Windows.Forms.IWin32Window;


namespace System.Windows.Media
{
    public static class WpfExtensions
    {
        public static IWin32Window GetIWin32Window(this Visual visual)
        {
            var source = PresentationSource.FromVisual(visual) as HwndSource;
            IWin32Window win = new Win32Window(source.Handle);
            return win;
        }


        private class Win32Window : IWin32Window
        {
            private readonly IntPtr handle;
            public Win32Window(IntPtr handle) => this.handle = handle;

            IntPtr IWin32Window.Handle => handle;
        }
    }
}
