namespace Dependiator.Utils
{
	public interface ICmd
	{
		CmdResult Run(string path, string args);
		CmdResult Start(string path, string args);
	}
}