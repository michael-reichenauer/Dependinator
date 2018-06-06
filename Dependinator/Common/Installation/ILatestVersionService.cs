using System;
using System.Threading.Tasks;


namespace Dependinator.Common.Installation
{
	internal interface ILatestVersionService
	{
		event EventHandler OnNewVersionAvailable;

		void StartCheckForLatestVersion();
		Task CheckLatestVersionAsync();

	}
}