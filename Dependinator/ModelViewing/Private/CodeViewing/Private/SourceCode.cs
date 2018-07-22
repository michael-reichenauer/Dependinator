namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
	internal class SourceCode
	{
		public string Text { get; }
		public int LineNumber { get; }
		public string FilePath { get; }


		public SourceCode(string text, int lineNumber, string filePath)
		{
			Text = text;
			LineNumber = lineNumber;
			FilePath = filePath;
		}
	}
}