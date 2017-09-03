using System;
using System.Collections.Generic;
using System.Windows;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils.UI;

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


	internal class FileItem : ViewModel
	{
		private readonly Action<string> openFileAction;

		public FileItem(string fileName, string filePath, Action<string> openFileAction)
		{
			FileName = fileName;
			FilePath = filePath;
			this.openFileAction = openFileAction;
		}


		public string FilePath { get; }
		public string FileName { get; }
		public string ToolTip => "Show model for " + FilePath;
		public Command OpenFileCommand => Command(() => openFileAction(FilePath));
	}
}