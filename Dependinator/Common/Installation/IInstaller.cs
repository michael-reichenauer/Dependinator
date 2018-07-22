namespace Dependinator.Common.Installation
{
	public interface IInstaller
	{
		bool InstallOrUninstall();

		bool IsExtensionInstalled();

		bool InstallExtension(bool isSilent, bool isWait);
	}
}