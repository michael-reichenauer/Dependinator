namespace Dependinator.Core.Shared;

// Classifies model paths: paths ending in ".sln" refer to solutions that can be parsed;
// any other path is a manually designed model that must never be parsed.
static class ModelPaths
{
    public static bool IsParseable(string path) => path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);

    public static bool IsDesignModel(string path) => !IsParseable(path);
}
