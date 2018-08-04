using System.IO;


namespace Dependinator.ModelViewing.Private.DataHandling.Dtos
{
    public class DataFile
    {
        public string FilePath { get; }
        public string CachePath => GetCacheFilePath();

        public string WorkFolderPath { get; }


        public DataFile(string filePath, string workFolderPath)
        {
            FilePath = filePath;
            WorkFolderPath = workFolderPath;
        }


        public override string ToString() => FilePath;

        private string GetCacheFilePath()
        {
            string dataJson = $"{Path.GetFileName(FilePath)}.dn.json";
            return Path.Combine(WorkFolderPath, dataJson);
        }
    }
}
