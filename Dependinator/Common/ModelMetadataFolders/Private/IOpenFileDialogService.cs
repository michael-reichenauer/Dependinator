namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal interface IOpenFileDialogService
	{
		bool TryShowOpenFileDialog(out string filePath);
	}
}