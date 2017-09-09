using System.IO;
using System.Linq;
using Dependinator.Utils;
using Microsoft.Win32;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal class OpenFileDialogService : IOpenFileDialogService
	{
		private static readonly string OpenFileDialogTitle = "Select a .NET .dll or .exe file";

		private static readonly string[] SupportedFileExtensions = {".exe", ".dll"};
		private static readonly string DefaultFileExtension = ".exe";

		private static readonly string SupportedFileTypes =
			"Files (*.exe, *.dll)|*.exe;*.dll|.NET libs (*.dll)|*.dll|.NET Programs (*.exe)|*.exe";


		public bool TryShowOpenFileDialog(out string filePath)
		{
			while (true)
			{
				filePath = null;

				OpenFileDialog openFileDialog = new OpenFileDialog
				{
					Title = OpenFileDialogTitle,
					DefaultExt = DefaultFileExtension,
					Filter = SupportedFileTypes,
					CheckFileExists = true,
					Multiselect = false,
					InitialDirectory = GetIntialFolder()
				};

				bool? result = openFileDialog.ShowDialog();

				if (result != true)
				{
					Log.Debug("User canceled selecting a file");
					return false;
				}

				filePath = openFileDialog.FileName;

				if (IsValidPath(filePath))
				{
					Log.Debug($"User selected valid file '{filePath}'");
					return true;
				}

				Log.Debug($"User selected an unsupported file: {openFileDialog.FileName}, retrying");
			}
		}


		private static string GetIntialFolder()
		{
			return System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
		}


		private static bool IsValidPath(string path)
		{
			if (path == null || !File.Exists(path))
			{
				return false;
			}

			return SupportedFileExtensions.Any(path.EndsWith);
		}
	}
}