using System.Collections.Generic;
using System.Windows;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Open
{
	internal class OpenModelViewModel : ItemViewModel
	{
		public OpenModelViewModel()
		{
			ItemBounds = new Rect(30, 30, 730, 580);
			RecentFiles.Add(new FileItem("Server.dll", "c:\\Work\\Server.dll", OpenRecentFile));
			RecentFiles.Add(new FileItem("Dependiator.exe", "c:\\Work\\Dependiator.exe", OpenRecentFile));
		}


		public List<FileItem> RecentFiles { get; } = new List<FileItem>();

		public void OpenFile()
		{

		}

		public void OpenRecentFile(string filePath)
		{

		}

	}
}