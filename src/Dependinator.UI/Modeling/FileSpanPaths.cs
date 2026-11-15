using Dependinator.UI.Modeling.Dtos;

namespace Dependinator.UI.Modeling;

// Converts node FileSpan paths (and a project's folder) between the in-memory absolute form and
// the persisted model-relative form. They are stored relative to the model (.sln) folder, so a
// cached or cloud-synced model keeps its source locations when the same solution is opened from
// a different base path (another computer or checkout). Paths outside the model folder are
// persisted unchanged, and absolute persisted paths are applied unchanged.
static class FileSpanPaths
{
    // The model folder itself, as a relative path
    const string ModelFolder = ".";

    public static NodeDto ToRelative(NodeDto nodeDto, string modelPath)
    {
        var properties = nodeDto.Properties;
        if (properties.FileSpan is null && properties.ProjectFolder is null)
            return nodeDto;

        var rootLength = RootDirLength(modelPath);
        if (rootLength <= 0)
            return nodeDto;

        return nodeDto with
        {
            Properties = properties with
            {
                FileSpan = properties.FileSpan is { } span
                    ? span with
                    {
                        Path = ToRelativePath(span.Path, modelPath, rootLength),
                    }
                    : null,
                ProjectFolder = properties.ProjectFolder is { } folder
                    ? ToRelativePath(folder, modelPath, rootLength, isFolder: true)
                    : null,
            },
        };
    }

    public static NodeDto ToAbsolute(NodeDto nodeDto, string modelPath)
    {
        var properties = nodeDto.Properties;
        var isSpanRelative = properties.FileSpan is { } span && !IsAbsolute(span.Path);
        var isFolderRelative = properties.ProjectFolder is { } folder && !IsAbsolute(folder);
        if (!isSpanRelative && !isFolderRelative)
            return nodeDto;

        var rootLength = RootDirLength(modelPath);
        if (rootLength <= 0)
            return nodeDto;

        var root = modelPath[..rootLength];
        var separator = modelPath.Contains('\\') ? '\\' : '/';
        return nodeDto with
        {
            Properties = properties with
            {
                FileSpan = isSpanRelative
                    ? properties.FileSpan! with
                    {
                        Path = $"{root}{separator}{properties.FileSpan.Path}",
                    }
                    : properties.FileSpan,
                ProjectFolder =
                    !isFolderRelative ? properties.ProjectFolder
                    : properties.ProjectFolder == ModelFolder ? root
                    : $"{root}{separator}{properties.ProjectFolder}",
            },
        };
    }

    static string ToRelativePath(string path, string modelPath, int rootLength, bool isFolder = false)
    {
        if (!path.StartsWith(modelPath[..rootLength], StringComparison.OrdinalIgnoreCase))
            return path;
        if (isFolder && path.TrimEnd('/', '\\').Length == rootLength)
            return ModelFolder;
        if (path.Length <= rootLength + 1 || !IsSeparator(path[rootLength]))
            return path;
        return path[(rootLength + 1)..];
    }

    // Length of the model path's directory part (index of the last separator).
    static int RootDirLength(string modelPath) => modelPath.LastIndexOfAny(['/', '\\']);

    static bool IsSeparator(char c) => c is '/' or '\\';

    // Rooted unix ("/..."), UNC ("\\...") or Windows drive ("C:...") paths. Hand-rolled since
    // Path.IsPathRooted uses the runtime platform's rules, but a model saved on Windows can be
    // loaded on unix (cloud sync) and vice versa.
    public static bool IsAbsolute(string path) =>
        path.Length > 0 && (IsSeparator(path[0]) || (path.Length > 1 && path[1] == ':'));
}
