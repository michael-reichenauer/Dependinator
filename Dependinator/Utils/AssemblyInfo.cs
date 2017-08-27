using System.Diagnostics;
using System.Reflection;

namespace Dependinator.Utils
{
	internal static class AssemblyInfo
	{
		public static string GetProgramVersion()
		{
			Assembly assembly = Assembly.GetEntryAssembly();
			FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
			return fvi.FileVersion;
		}
	}
}