using System;
using System.Windows;
using Dependinator.ModelViewing;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace Dependinator.Api
{
    /// <summary>
    ///     Implements the IDependinatorApi api published by the Dependinator exe.
    ///     It is used by the Visual Studio extension to call the Dependinator exe and by different
    ///     Dependiator instances, when they are interacting.
    /// </summary>
    [SingleInstance]
    internal class DependinatorApiService : ApiIpcService, IDependinatorApi
    {
        private readonly Lazy<IModelViewService> modelViewService;


        public DependinatorApiService(Lazy<IModelViewService> modelViewService)
        {
            this.modelViewService = modelViewService;
        }


        /// <summary>
        ///     Activate the studio main window to bring ti to the front.
        /// </summary>
        public void Activate(string[] args)
        {
            MoveMainWindowToFront();
        }


        /// <summary>
        ///     Show the node that correspond to the specified file
        /// </summary>
        public void ShowNodeForFile(string filePath, int lineNumber)
        {
            MoveMainWindowToFront();

            Application.Current.Dispatcher.InvokeAsync(() => 
                { modelViewService.Value.StartMoveToNode(new Source(filePath, null, lineNumber)); });
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
