using Bicep.Core.Semantics;
using Bicep.Core.Syntax;
using Bicep.Core.Text;

namespace Dependinator.Bicep.Parsing;

// Source locations (0-based lines, like the Roslyn parser's) for go-to-code navigation.
static class Locations
{
    public static FileSpan ToFileSpan(SemanticModel model, SyntaxBase syntax)
    {
        var lineStarts = model.SourceFile.LineStarts;
        var (startLine, _) = TextCoordinateConverter.GetPosition(lineStarts, syntax.Span.Position);
        var (endLine, _) = TextCoordinateConverter.GetPosition(lineStarts, syntax.Span.Position + syntax.Span.Length);
        return new FileSpan(GetFilePath(model), startLine, endLine);
    }

    public static string GetFilePath(SemanticModel model) => model.SourceFile.FileHandle.Uri.GetFilePath();

    public static int GetLine(SemanticModel model, int offset) =>
        TextCoordinateConverter.GetPosition(model.SourceFile.LineStarts, offset).line;
}
