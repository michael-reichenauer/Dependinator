namespace Dependinator.ModelViewing.Private.DataHandling.Dtos
{
    internal class SourceLocation
    {
        public string FilePath { get; }
        public int LineNumber { get; }
        

        public SourceLocation(string filePath, int lineNumber)
        {
            FilePath = filePath;
            LineNumber = lineNumber;
        }
    }
}
