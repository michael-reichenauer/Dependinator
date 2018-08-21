namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers
{
    public class NodeDataSource
    {
        public string Text { get; }
        public int LineNumber { get; }
        public string Path { get; }


        public NodeDataSource(string text, int lineNumber, string path)
        {
            Text = text;
            LineNumber = lineNumber;
            Path = path;
        }
    }
}
