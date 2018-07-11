using System.Windows;
using Dependinator.Utils;
using Dependinator.Utils.Net;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal interface IExistingInstanceIpcService
	{
		void Activate(string[] args);
	}


	internal class ExistingInstanceIpcService : IpcService, IExistingInstanceIpcService
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