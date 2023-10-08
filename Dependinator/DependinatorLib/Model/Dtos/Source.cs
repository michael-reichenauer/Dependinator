namespace Dependinator.Model.Dtos;

internal class Source
{
    public string Path { get; }
    public string Text { get; }
    public int LineNumber { get; }


    public Source(string path, string text, int lineNumber)
    {
        Path = path;
        Text = text;
        LineNumber = lineNumber;
    }


    public override string ToString() => LineNumber == 0 ? Path : $"{Path}({LineNumber})";
}

