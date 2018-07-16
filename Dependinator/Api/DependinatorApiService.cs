using System.Windows;
using System.Windows.Threading;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using DependinatorApi;
using DependinatorApi.ApiHandling;



namespace Dependinator.Api
{
	[SingleInstance]
	internal class DependinatorApiService : ApiIpcService, IDependinatorApi
	{
		public void Activate(string[] args)
		{
			MoveMainWindowToFront();
		}


		public void ShowFile(string filePath)
		{
			MoveMainWindowToFront();

			Log.Warn($"Show file {filePath}");
		}


		private static void MoveMainWindowToFront()
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