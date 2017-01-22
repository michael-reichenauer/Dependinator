using System.Collections.Generic;


namespace Dependiator.ApplicationHandling.SettingsHandling
{
	public class WorkFolderSettings
	{
		public Dictionary<string, string> BranchColors { get; set; } = new Dictionary<string, string>();

		public string FilePath { get; set; }

		public double Scale { get; set; } = 1;
		public double X { get; set; } = 0;
		public double Y { get; set; } = 0;
	}
}