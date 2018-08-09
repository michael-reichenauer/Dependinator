namespace Dependinator.ModelViewing.Private.DataHandling.Dtos
{
    public class DataFile
    {
        public string FilePath { get; }

        public string WorkFolderPath { get; }


        public DataFile(string filePath, string workFolderPath)
        {
            FilePath = filePath;
            WorkFolderPath = workFolderPath;
        }


        public override string ToString() => FilePath;
    }
}
