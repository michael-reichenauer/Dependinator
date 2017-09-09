using System.Collections.Generic;


namespace Dependinator.Common.ModelMetadataFolders
{
	internal interface IRecentModelsService
	{
		void AddModelPaths(string modelFilePath);

		IReadOnlyList<string> GetModelPaths();
	}
}