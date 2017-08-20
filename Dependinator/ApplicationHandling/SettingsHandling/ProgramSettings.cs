using System.Windows;

namespace Dependinator.ApplicationHandling.SettingsHandling
{
	internal class ProgramSettings
	{
		public string LastUsedWorkingFolder { get; set; } = "";
		public string LatestVersionInfoETag { get; set; } = "";
		public string LatestVersionInfo { get; set; } = "";

		public Rect WindowBounds { get; set; } = new Rect(100, 100, 800, 695);
		public bool IsMaximized { get; set; } = false;
	}
}