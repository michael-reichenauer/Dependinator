using System.Collections.Generic;
using System.Threading.Tasks;


namespace Dependinator.Common.ModelMetadataFolders
{
	public interface IOpenModelService
	{
		Task OpenOtherModelAsync();

		Task OpenCurrentModelAsync();

		Task TryModelAsync(string modelFilePath);

		Task OpenModelAsync(IReadOnlyList<string> modelFilePaths);
	}
}