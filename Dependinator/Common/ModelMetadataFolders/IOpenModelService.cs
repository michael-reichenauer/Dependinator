using System.Threading.Tasks;


namespace Dependinator.Common.ModelMetadataFolders
{
	public interface IOpenModelService
	{
		Task OpenModelAsync();

		Task OpenModelAsync(string modelFilePath);
	}
}