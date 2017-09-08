using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dependinator.Common.WorkFolders
{
	public interface IOpenModelService
	{
		Task OpenFileAsync();

		Task OpenFileAsync(string filePath);

		IReadOnlyList<string> GetResentFilePaths();
	}
}