namespace Dependinator.ModelViewing.Private.DataHandling.Dtos
{
    internal class Source
    {
        public string Path { get; }
        public int LineNumber { get; }
        

        public Source(string path, int lineNumber)
        {
            Path = path;
            LineNumber = lineNumber;
        }
    }
}
