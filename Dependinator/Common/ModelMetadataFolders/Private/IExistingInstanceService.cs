namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal interface IExistingInstanceService
	{
		void RegisterPath(string metaDataFolderPath);
		bool TryActivateExistingInstance(string metaDataFolderPath, string[] args);
	}
}