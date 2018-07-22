namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal interface IExistingInstanceService
	{
		bool TryActivateExistingInstance(string modelFilePath, string[] args);
		bool TryActivateExistingInstance(string[] args);

		bool WaitForOtherInstance();
	}
}