using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;


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


		//string message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.GetType().FullName);
		//string title = "ShowInDependinatorCommand";

		//// Show a message box to prove we were here
		//VsShellUtilities.ShowMessageBox(
		//		this.package,
		//		message,
		//		title,
		//		OLEMSGICON.OLEMSGICON_INFO,
		//		OLEMSGBUTTON.OLEMSGBUTTON_OK,
		//		OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
	}
}