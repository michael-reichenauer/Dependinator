using System;
using System.Threading.Tasks;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Open
{
	internal class FileItem : ViewModel
	{
		private readonly Func<string, Task> openFileAction;

		public FileItem(string fileName, string filePath, Func<string, Task> openFileAction)
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