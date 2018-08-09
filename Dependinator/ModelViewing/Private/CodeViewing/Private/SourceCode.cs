namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
    internal class SourceCode
    {
        public SourceCode(string text, int lineNumber, string filePath)
        {
            Text = text;
            LineNumber = lineNumber;
            FilePath = filePath;
        }


        public string Text { get; }
        public int LineNumber { get; }
        public string FilePath { get; }
    }
}
