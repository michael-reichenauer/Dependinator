using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;


namespace DependinatorVse.Utils
{
	internal class Studio
	{
		public static async Task<DTE2> GetDteAsync(AsyncPackage package) =>
			(DTE2)await package.GetServiceAsync(typeof(DTE));


		public async Task<T> GetServiceAsync<T>(AsyncPackage package) =>
			(T)(await package.GetServiceAsync(typeof(T)));

		public static bool IsDeveloperMode(DTE2 dte) =>
			dte?.MainWindow?.Caption?.Contains("- Experimental Instance") ?? false;


		public static bool ShowInfoMessageBox(
			AsyncPackage package, string message, bool isCancellable = false) =>
				1 == ShowMessageBox(package, message, OLEMSGICON.OLEMSGICON_INFO, isCancellable);

		public static void ShowWarnMessageBox(AsyncPackage package, string message) =>
			ShowMessageBox(package, message, OLEMSGICON.OLEMSGICON_WARNING, false);


		private static int ShowMessageBox(
			AsyncPackage package, 
			string message, 
			OLEMSGICON messageType, 
			bool isCancellable)
		{
			OLEMSGBUTTON buttons = isCancellable ? OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL : OLEMSGBUTTON.OLEMSGBUTTON_OK;
			return VsShellUtilities.ShowMessageBox(
				package,
				message,
				"Dependinator Extension",
				messageType,
				buttons,
				OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
		}
	}
}