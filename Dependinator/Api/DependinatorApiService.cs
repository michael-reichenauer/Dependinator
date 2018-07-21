using System;
using System.Windows;
using Dependinator.ModelViewing;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using DependinatorApi;
using DependinatorApi.ApiHandling;



namespace Dependinator.Api
{
	[SingleInstance]
	internal class DependinatorApiService : ApiIpcService, IDependinatorApi
	{
		private readonly Lazy<IModelViewService> modelViewService;


		public DependinatorApiService(Lazy<IModelViewService> modelViewService)
		{
			this.modelViewService = modelViewService;
		}


		public void Activate(string[] args)
		{
			MoveMainWindowToFront();
		}


		public void ShowNodeForFile(string filePath, int lineNumber)
		{
			MoveMainWindowToFront();

			Application.Current.Dispatcher.InvokeAsync(() =>
			{
				modelViewService.Value.StartMoveToNode(filePath);
			});
		}


		private static void MoveMainWindowToFront()
		{
			Application.Current.Dispatcher.InvokeAsync(() =>
			{
				WindowState state = Application.Current.MainWindow.WindowState;

				Application.Current.MainWindow.WindowState = WindowState.Minimized;
				Application.Current.MainWindow.Activate();
				Application.Current.MainWindow.WindowState = state == WindowState.Maximized
					? WindowState.Maximized
					: WindowState.Normal;

				Log.Usage("Activated");
			});
		}
	}
}