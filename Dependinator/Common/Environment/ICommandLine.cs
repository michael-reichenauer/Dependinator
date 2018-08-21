namespace Dependinator.Common.Environment
{
    public interface ICommandLine
    {
        bool IsSilent { get; }
        bool IsInstall { get; }
        bool IsUninstall { get; }
        bool IsCheckUpdate { get; }
        bool IsRunInstalled { get; }
        bool IsTest { get; }
        bool HasFile { get; }
        string FilePath { get; }
        bool IsInstallExtension { get; }
    }
}
