using System;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal interface IModelMetadataService
	{
		event EventHandler OnChange;

		string ModelFilePath { get; }

		string MetadataFolderPath { get; }
		bool IsDefault { get; }

		void SetModelFilePath(string path);
		void SetDefault();
	}
}