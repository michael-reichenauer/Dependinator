using System.Windows;
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
			Application.Current.Dispatcher.InvokeAsync(() =>
			{
				Application.Current.MainWindow.WindowState = WindowState.Minimized;
				Application.Current.MainWindow.Activate();
				Application.Current.MainWindow.WindowState = WindowState.Normal;

				Log.Usage("Activated");
			});
		}


		public void ShowFile(string filePath)
		{
			Log.Warn($"Show file {filePath}");
		}
	}
}