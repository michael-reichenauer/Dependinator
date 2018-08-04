using System.Collections.Generic;
using System.IO;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing;


namespace Dependinator.ModelViewing.Private.DataHandling.Private
{
    internal class DataFilePaths : IDataFilePaths
    {
        private readonly IParserService parserService;


        public DataFilePaths(IParserService parserService)
        {
            this.parserService = parserService;
        }
        public string GetDataFolderPath(DataFile dataFile) => Path.GetDirectoryName(dataFile.FilePath);


        public IReadOnlyList<string> GetDataFilePaths(DataFile dataFile) =>
            parserService.GetDataFilePaths(dataFile);



        public IReadOnlyList<string> GetBuildPaths(DataFile dataFile) => 
            parserService.GetBuildPaths(dataFile);


        public string GetCacheFilePath(DataFile dataFile)
        {
            var dataFileName = Path.GetFileName(dataFile.FilePath);
            string cacheFileName = $"{dataFileName}.dn.json";
            return Path.Combine(dataFile.WorkFolderPath, cacheFileName);
        }


        public string GetSaveFilePath(DataFile dataFile)
        {
            string dataFileName = $"{Path.GetFileName(dataFile.FilePath)}.dpnr";
            string folderPath = Path.GetDirectoryName(dataFile.FilePath);
            //  string dataFilePath = Path.Combine(folderPath, dataJson);
            return Path.Combine(dataFile.WorkFolderPath, dataFileName);
        }
    }
}
