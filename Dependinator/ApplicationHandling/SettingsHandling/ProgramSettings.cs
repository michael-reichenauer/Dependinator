using System.Windows;

namespace Dependinator.ApplicationHandling.SettingsHandling
{
	internal class ProgramSettings
	{
		public string LastUsedWorkingFolder { get; set; } = "";
		public string LatestVersionInfoETag { get; set; } = "";
		public string LatestVersionInfo { get; set; } = "";
	}
}