namespace Dependiator.ApplicationHandling
{
	public interface ICommandLine
	{
		bool IsSilent { get; }
		bool IsInstall { get; }
		bool IsUninstall { get; }
		bool IsRunInstalled { get; }
		bool IsTest { get; }
		bool HasFolder { get; }
		string Folder { get; }
	}
}