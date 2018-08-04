using System.Collections.Generic;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;


namespace Dependinator.ModelViewing.Private.DataHandling.Private
{
    internal interface IDataFilePaths
    {
        string GetDataFolderPath(DataFile dataFile);

        IReadOnlyList<string> GetDataFilePaths(DataFile dataFile);

        IReadOnlyList<string> GetBuildPaths(DataFile dataFile);

        string GetCacheFilePath(DataFile dataFile);

        string GetSaveFilePath(DataFile dataFile);
    }
}
