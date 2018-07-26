namespace Dependinator.ModelViewing.Private.DataHandling
{
    internal class SourceLocation
    {
        public SourceLocation(string filePath, int lineNumber)
        {
            FilePath = filePath;
            LineNumber = lineNumber;
        }


        public string FilePath { get; }
        public int LineNumber { get; }
    }
}
