using System.Collections.Generic;
using System.Windows;


namespace Dependinator.Common.SettingsHandling
{
    public class WorkFolderSettings
    {
        public Dictionary<string, string> BranchColors { get; set; } = new Dictionary<string, string>();

        public string FilePath { get; set; }

        public Rect MainWindowBounds { get; set; } = new Rect(100, 100, 1000, 700);
        public bool IsMaximized { get; set; } = false;
        public double Scale { get; set; } = 2;
        public Point Offset { get; set; } = new Point(-37, 43);
    }
}
