namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
	internal class SourceCode
	{
		public string Text { get; }
		public int LineNumber { get; }


		public SourceCode(string text, int lineNumber)
		{
			Text = text;
			LineNumber = lineNumber;
		}
	}
}