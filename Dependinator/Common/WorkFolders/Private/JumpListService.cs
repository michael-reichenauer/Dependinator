using System.IO;
using System.Windows;
using System.Windows.Shell;
using Dependinator.Common.SettingsHandling;

namespace Dependinator.Common.WorkFolders.Private
{
	public class JumpListService
	{
		private static readonly int MaxTitleLength = 25;


		public void Add(string path)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return;
			}

			JumpList jumpList = JumpList.GetJumpList(Application.Current) ?? new JumpList();

			string name = Path.GetFileNameWithoutExtension(path) ?? path;

			string title = name.Length < MaxTitleLength
				? name
				: name.Substring(0, MaxTitleLength) + "...";

			JumpTask jumpTask = new JumpTask();
			jumpTask.Title = title;
			jumpTask.ApplicationPath = ProgramInfo.GetInstallFilePath();
			jumpTask.Arguments = $"\"{path}\"";
			jumpTask.IconResourcePath = ProgramInfo.GetInstallFilePath();
			jumpTask.Description = path;

			jumpList.ShowRecentCategory = true;

			JumpList.AddToRecentCategory(jumpTask);
			JumpList.SetJumpList(Application.Current, jumpList);
		}
	}
}