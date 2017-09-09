using System;
using System.IO;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	[SingleInstance]
	internal class ModelMetadataService : IModelMetadataService
	{
		public event EventHandler OnChange;

		public string ModelFilePath { get; private set; }
		
		public string MetadataFolderPath { get; private set; }

		public ModelMetadataService()
		{
			string defaultPath = Path.Combine(ProgramInfo.GetProgramFolderPath(), "Default");

			SetModelFilePath(defaultPath);
		}


		public void SetModelFilePath(string modelFilePath)
		{
			string metadataPath = GetMetadataFolderPath(modelFilePath);

			EnsureFolderExists(metadataPath);

			if (!MetadataFolderPath.IsSameIgnoreCase(metadataPath))
			{
				MetadataFolderPath = metadataPath;
				ModelFilePath = modelFilePath;
				OnChange?.Invoke(this, EventArgs.Empty);
			}
		}

		
		public static string GetMetadataFolderPath(string modelFilePath)
		{
			string metadataFolderName = CreateMetadataFolderName(modelFilePath);

			string metadataFoldersRoot = ProgramInfo.GetModelMetadataFoldersRoot();

			return Path.Combine(metadataFoldersRoot, metadataFolderName);
		}


		private static string CreateMetadataFolderName(string path)
		{
			string encoded = path.Replace(" ", "  ");
			encoded = encoded.Replace(":", " ;");
			encoded = encoded.Replace("/", " (");
			encoded = encoded.Replace("\\", " )");

			return encoded;
		}


		private static void EnsureFolderExists(string folder)
		{
			try
			{
				if (!Directory.Exists(folder))
				{
					Directory.CreateDirectory(folder);
				}
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Failed to create meta data folder {folder}");
				throw;
			}
		}
	}
}