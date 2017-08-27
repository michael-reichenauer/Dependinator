using System.Collections.Generic;
using System.Windows;

namespace Dependinator.Common.SettingsHandling
{
	public class WorkFolderSettings
	{
		public Dictionary<string, string> BranchColors { get; set; } = new Dictionary<string, string>();

		public string FilePath { get; set; }

		public Rect WindowBounds { get; set; } = new Rect(100, 100, 800, 695);
		public bool IsMaximized { get; set; } = false;
		public double Scale { get; set; } = 1;
		public Point Offset { get; set; } = new Point(0, 0);
	}
}