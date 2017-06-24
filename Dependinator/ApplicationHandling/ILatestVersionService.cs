using System;
using System.Threading.Tasks;


namespace Dependinator.ApplicationHandling
{
	internal interface ILatestVersionService
	{
		event EventHandler OnNewVersionAvailable;

		void StartCheckForLatestVersion();

		Task<bool> StartLatestInstalledVersionAsync();
	}
}