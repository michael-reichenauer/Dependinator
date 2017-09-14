﻿using System.Collections.Generic;
using System.Threading.Tasks;


namespace Dependinator.Common.ModelMetadataFolders
{
	public interface IOpenModelService
	{
		Task OpenModelAsync();

		Task OpenModelAsync(string modelFilePath);

		Task OpenModelAsync(IReadOnlyList<string> modelFilePaths);
	}
}