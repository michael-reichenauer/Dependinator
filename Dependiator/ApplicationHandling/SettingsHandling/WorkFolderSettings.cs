using System.Collections.Generic;


namespace Dependiator.ApplicationHandling.SettingsHandling
{
	public class WorkFolderSettings
	{
		public Dictionary<string, string> BranchColors { get; set; } = new Dictionary<string, string>();

		public string FilePath { get; set; }
	}
}