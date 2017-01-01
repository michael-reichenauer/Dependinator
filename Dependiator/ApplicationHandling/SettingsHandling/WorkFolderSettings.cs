using System.Collections.Generic;


namespace Dependiator.ApplicationHandling.SettingsHandling
{
	public class WorkFolderSettings
	{
		public double Left { get; set; } = 100;
		public double Top { get; set; } = 100;
		public double Width { get; set; } = 800;
		public double Height { get; set; } = 695;
		public bool IsMaximized { get; set; } = false;

		public Dictionary<string, string> BranchColors { get; set; } = new Dictionary<string, string>();
	}
}