namespace Dependinator.Common.ModelMetadataFolders.Private
{
	public interface IStartInstanceService
	{
		bool StartInstance(string modelFilePath);

		bool OpenOrStartInstance(string modelFilePath);
	}
}