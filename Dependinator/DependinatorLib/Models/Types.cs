namespace Dependinator.Models;

record Pos(double X, double Y);
record Size(double Width, double Height);
record Rect(double X, double Y, double Width, double Height);


interface IItem { }
record Source(string Path, string Text, int LineNumber);

