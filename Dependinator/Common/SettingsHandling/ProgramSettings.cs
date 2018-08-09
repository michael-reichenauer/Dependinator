using System.Collections.Generic;


namespace Dependinator.Common.SettingsHandling
{
    internal class ProgramSettings
    {
        public string LastUsedWorkingFolder { get; set; } = "";
        public string LatestVersionInfoETag { get; set; } = "";
        public string LatestVersionInfo { get; set; } = "";
        public List<string> ResentModelPaths { get; set; } = new List<string>();
    }
}
