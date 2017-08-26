using System;
using System.Windows;
using Dependinator.ApplicationHandling.Installation;
using Dependinator.Utils;


namespace Dependinator.MainWindowViews
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