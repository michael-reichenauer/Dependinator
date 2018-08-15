using System.Collections.Generic;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;


namespace Dependinator.ModelViewing.Private.DataHandling.Private
{
    internal interface IDataFilePaths
    {
        IReadOnlyList<string> GetDataFilePaths(DataFile dataFile);

        string GetCacheFilePath(DataFile dataFile);

        string GetSaveFilePath(DataFile dataFile);
    }
}
