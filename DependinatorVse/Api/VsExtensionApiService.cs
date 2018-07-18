﻿
using DependinatorApi;
using DependinatorApi.ApiHandling;
using DependinatorVse.Commands.Private;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;


namespace DependinatorVse.Api
{
	public class VsExtensionApiService : ApiIpcService, IVsExtensionApi
	{
		private readonly AsyncPackage package;


		public VsExtensionApiService(AsyncPackage package)
		{
			this.package = package;
		}


#pragma warning disable VSTHRD100 // Api functions does not support Task type
		public async void Activate()
#pragma warning restore VSTHRD100
		{
			await package.JoinableTaskFactory.SwitchToMainThreadAsync();

			DTE2 dte = (DTE2)await package.GetServiceAsync(typeof(DTE));
			if (dte == null) return;

			ActivateMainWindow(dte);
		}


#pragma warning disable VSTHRD100 // Api functions does not support Task type
		public async void ShowFile(string filePath, int lineNumber)
#pragma warning restore VSTHRD100 
		{
			await package.JoinableTaskFactory.SwitchToMainThreadAsync();

			Log.Debug($"Show {filePath}");
			
			DTE2 dte = (DTE2)await package.GetServiceAsync(typeof(DTE));
			if (dte == null) return;
			
			dte.ExecuteCommand("File.OpenFile", $"\"{filePath}\"");
			dte.ExecuteCommand("Edit.GoTo", $"{lineNumber}");
		}


		private static void ActivateMainWindow(DTE2 dte)
		{
			Window mainWindow = dte.MainWindow;
			mainWindow.WindowState = vsWindowState.vsWindowStateMinimize;
			mainWindow.Activate();
			mainWindow.WindowState = vsWindowState.vsWindowStateNormal;
		}
	}
}