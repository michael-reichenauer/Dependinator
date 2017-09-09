using System;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal interface IModelMetadataService
	{
		event EventHandler OnChange;

		string ModelFilePath { get; }

		string MetadataFolderPath { get; }
		
		void SetModelFilePath(string path);
	}
}