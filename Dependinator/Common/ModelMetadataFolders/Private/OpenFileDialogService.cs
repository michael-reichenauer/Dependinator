using System.IO;
using System.Linq;
using Dependinator.Utils;
using Microsoft.Win32;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal class OpenFileDialogService : IOpenFileDialogService
	{
		private static readonly string OpenFileDialogTitle = "Select a .NET .sln, .exe or .dll file";

		private static readonly string[] SupportedFileExtensions = { ".sln", ".exe", ".dll", };
		private static readonly string DefaultFileExtension = ".sln";

		private static readonly string SupportedFileTypes =
			".NET files (*.sln *.exe, *.dll)|*.sln; *.exe;*.dll";


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
					InitialDirectory = GetInitialFolder()
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


		private static string GetInitialFolder()
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