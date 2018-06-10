using System.Diagnostics;
using System.Reflection;


namespace Dependinator.Utils.OsSystem
{
	internal static class AssemblyExtensions
	{
		public static string GetFileVersion(this Assembly assembly)
		{
			FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
			return fvi.FileVersion;
		}
	}
}