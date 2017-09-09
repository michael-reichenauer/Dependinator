using System.Collections.Generic;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal interface IRecentModelsService
	{
		void AddModelPaths(string modelFilePath);

		IReadOnlyList<string> GetModelPaths();
	}
}