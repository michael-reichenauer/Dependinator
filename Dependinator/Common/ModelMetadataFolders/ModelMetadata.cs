using System;
using System.IO;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.Dependencies;


namespace Dependinator.Common.ModelMetadataFolders
{
    [SingleInstance]
    internal class ModelMetadata
    {
        private readonly IModelMetadataService modelMetadataService;


        public ModelMetadata(IModelMetadataService modelMetadataService)
        {
            this.modelMetadataService = modelMetadataService;
        }

        public DataFile DataFile => new DataFile(ModelFilePath, FolderPath);

        public string FolderPath => modelMetadataService.MetadataFolderPath;


        public string ModelFilePath => modelMetadataService.ModelFilePath;

        public bool IsValid => ModelFilePath != null && File.Exists(ModelFilePath);
        public bool IsDefault => modelMetadataService.IsDefault;

        public bool HasValue => FolderPath != null;

        public string ModelName => HasValue ? Path.GetFileNameWithoutExtension(ModelFilePath) : null;


        public event EventHandler OnChange
        {
            add => modelMetadataService.OnChange += value;
            remove => modelMetadataService.OnChange -= value;
        }

        public static implicit operator string(ModelMetadata modelMetadata) => modelMetadata.FolderPath;


        public override string ToString() => FolderPath;


        public void SetDefault() => modelMetadataService.SetDefault();
    }
}
