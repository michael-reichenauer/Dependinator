using System;
using System.IO;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Utils.Dependencies;


namespace Dependinator.Common.ModelMetadataFolders
{
	[SingleInstance]
	internal class ModelMetadata
	{
		private readonly IModelMetadataService modelMetadataService;
		
		private string FolderPath => modelMetadataService.MetadataFolderPath;


		public ModelMetadata(IModelMetadataService modelMetadataService)
		{
			this.modelMetadataService = modelMetadataService;
		}


		public event EventHandler OnChange
		{
			add => modelMetadataService.OnChange += value;
			remove => modelMetadataService.OnChange -= value;
		}


		public string ModelFilePath => modelMetadataService.ModelFilePath;

		public bool IsValid => ModelFilePath != null && File.Exists(ModelFilePath);
		public bool IsDefault => modelMetadataService.IsDefault;

		public bool HasValue => FolderPath != null;

		public string ModelName => HasValue ? Path.GetFileNameWithoutExtension(ModelFilePath) : null;

		public static implicit operator string(ModelMetadata modelMetadata) => modelMetadata.FolderPath;
		

		public override string ToString() => FolderPath;


		public void SetDefault() => modelMetadataService.SetDefault();

	}
}