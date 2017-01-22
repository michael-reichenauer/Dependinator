namespace Dependiator.ApplicationHandling.SettingsHandling
{
	internal class ProgramSettings
	{
		public string LastUsedWorkingFolder { get; set; } = "";
		public string LatestVersionInfoETag { get; set; } = "";
		public string LatestVersionInfo { get; set; } = "";

		public double Left { get; set; } = 100;
		public double Top { get; set; } = 100;
		public double Width { get; set; } = 800;
		public double Height { get; set; } = 695;
		public bool IsMaximized { get; set; } = false;
	}
}