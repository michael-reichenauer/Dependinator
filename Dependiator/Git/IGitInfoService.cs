using Dependiator.Utils;


namespace Dependiator.Git
{
	internal interface IGitInfoService
	{
		R<string> GetCurrentRootPath(string folder);

		bool IsSupportedRemoteUrl(string workingFolder);
	}
}