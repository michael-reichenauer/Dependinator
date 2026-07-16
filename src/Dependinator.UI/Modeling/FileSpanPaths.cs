using Dependinator.UI.Modeling.Dtos;

namespace Dependinator.UI.Modeling;

// Converts node FileSpan paths between the in-memory absolute form and the persisted
// model-relative form. Spans are stored relative to the model (.sln) folder, so a cached
// or cloud-synced model keeps its source locations when the same solution is opened from
// a different base path (another computer or checkout). Paths outside the model folder
// are persisted unchanged, and absolute persisted paths are applied unchanged.
static class FileSpanPaths
{
    public static NodeDto ToRelative(NodeDto nodeDto, string modelPath)
    {
        var span = nodeDto.Properties.FileSpan;
        if (span is null)
            return nodeDto;

        var rootLength = RootDirLength(modelPath);
        if (rootLength <= 0)
            return nodeDto;

        var path = span.Path;
        if (
            path.Length <= rootLength + 1
            || !path.StartsWith(modelPath[..rootLength], StringComparison.OrdinalIgnoreCase)
            || !IsSeparator(path[rootLength])
        )
            return nodeDto;

        var relativePath = path[(rootLength + 1)..];
        return nodeDto with { Properties = nodeDto.Properties with { FileSpan = span with { Path = relativePath } } };
    }

    public static NodeDto ToAbsolute(NodeDto nodeDto, string modelPath)
    {
        var span = nodeDto.Properties.FileSpan;
        if (span is null || IsAbsolute(span.Path))
            return nodeDto;

        var rootLength = RootDirLength(modelPath);
        if (rootLength <= 0)
            return nodeDto;

        var separator = modelPath.Contains('\\') ? '\\' : '/';
        var absolutePath = $"{modelPath[..rootLength]}{separator}{span.Path}";
        return nodeDto with { Properties = nodeDto.Properties with { FileSpan = span with { Path = absolutePath } } };
    }

    // Length of the model path's directory part (index of the last separator).
    static int RootDirLength(string modelPath) => modelPath.LastIndexOfAny(['/', '\\']);

    static bool IsSeparator(char c) => c is '/' or '\\';

    // Rooted unix ("/..."), UNC ("\\...") or Windows drive ("C:...") paths. Hand-rolled since
    // Path.IsPathRooted uses the runtime platform's rules, but a model saved on Windows can be
    // loaded on unix (cloud sync) and vice versa.
    static bool IsAbsolute(string path) =>
        path.Length > 0 && (IsSeparator(path[0]) || (path.Length > 1 && path[1] == ':'));
}
