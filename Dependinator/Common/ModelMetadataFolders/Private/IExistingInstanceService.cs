namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal interface IExistingInstanceService
	{
		bool TryRegisterPath(string metaDataFolderPath);
	}
}