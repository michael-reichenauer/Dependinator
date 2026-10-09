namespace Dependinator.Core.Shared;

// Classifies model paths: a C# solution (.sln) or a Bicep deployment (.bicep entry file) refers
// to sources that can be parsed; any other path is a manually designed model that must never be
// parsed.
static class ModelPaths
{
    public static bool IsSolution(string path) => path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);

    public static bool IsBicep(string path) => path.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase);

    public static bool IsParseable(string path) => IsSolution(path) || IsBicep(path);

    public static bool IsDesignModel(string path) => !IsParseable(path);

    // The kind of model a path refers to, for titles and subtitles ("solution", "Bicep deployment",
    // "design model").
    public static string KindName(string path) =>
        IsBicep(path) ? "Bicep deployment"
        : IsSolution(path) ? "solution"
        : "design model";
}
