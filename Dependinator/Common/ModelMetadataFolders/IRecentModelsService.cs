using System;
using System.Collections.Generic;


namespace Dependinator.Common.ModelMetadataFolders
{
	internal interface IRecentModelsService
	{
		event EventHandler Changed;

		void AddModelPaths(string modelFilePath);

		IReadOnlyList<string> GetModelPaths();
		void RemoveModelPath(string modelFilePath);
	}
}