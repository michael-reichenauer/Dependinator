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


        public IReadOnlyList<string> GetDataFilePaths(DataFile dataFile) =>
            parserService.GetDataFilePaths(dataFile);



        public IReadOnlyList<string> GetBuildPaths(DataFile dataFile) => 
            parserService.GetMonitorChangesPaths(dataFile);


        public string GetCacheFilePath(DataFile dataFile)
        {
            var dataFileName = Path.GetFileName(dataFile.FilePath);
            string cacheFileName = $"{dataFileName}.dn.json";
            return Path.Combine(dataFile.WorkFolderPath, cacheFileName);
        }


        public string GetSaveFilePath(DataFile dataFile)
        {
            string dataFileName = $"{Path.GetFileNameWithoutExtension(dataFile.FilePath)}.dpnr";
            string folderPath = Path.GetDirectoryName(dataFile.FilePath);
            return Path.Combine(folderPath, dataFileName);

            ////  string dataFilePath = Path.Combine(folderPath, dataJson);
            //return Path.Combine(dataFile.WorkFolderPath, dataFileName);
        }
    }
}
