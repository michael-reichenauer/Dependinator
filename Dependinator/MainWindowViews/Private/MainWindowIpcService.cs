using System.Windows;
using Dependinator.Utils;

namespace Dependinator.MainWindowViews.Private
{
	internal class MainWindowIpcService : IpcService
	{
		public void Activate(string[] args)
		{
			Application.Current.Dispatcher.InvokeAsync(() =>
			{
				Application.Current.MainWindow.WindowState = WindowState.Minimized;
				Application.Current.MainWindow.Activate();
				Application.Current.MainWindow.WindowState = WindowState.Normal;

				Log.Usage("Activated");
			});
		}
	}
}