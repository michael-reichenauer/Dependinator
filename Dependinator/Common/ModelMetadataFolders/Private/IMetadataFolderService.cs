using System;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
    internal interface IModelMetadataService
    {
        string ModelFilePath { get; }

        string MetadataFolderPath { get; }
        bool IsDefault { get; }
        event EventHandler OnChange;

        void SetModelFilePath(string path);
        void SetDefault();
        string GetMetadataFolderPath(string modelFilePath);
    }
}
