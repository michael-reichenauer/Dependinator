using System.Security.Cryptography;

namespace Dependinator.TypeScript.Parsing;

// The parser helper's script (Helper/, bundled into one file and embedded in this assembly) as
// a file Node can run: written once per version to the user's local application data, under a
// name of its content's hash, so a file of that name that someone changed is never run.
static class HelperScript
{
    const string ResourceName = "Dependinator.TypeScript.ts-parser.js";
    static readonly Lock syncRoot = new();
    static string? path;

    public static Result<string> GetPath()
    {
        lock (syncRoot)
        {
            if (path is not null && File.Exists(path))
                return path;

            var result = Result.Catch(Write);
            if (result is string written)
                path = written;
            return result;
        }
    }

    static string Write()
    {
        using var resource = typeof(HelperScript).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The TypeScript parser helper is not part of this build.");
        using var memory = new MemoryStream();
        resource.CopyTo(memory);
        var content = memory.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(content))[..16].ToLowerInvariant();

        var folder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create
            ),
            "Dependinator",
            "helpers"
        );
        Directory.CreateDirectory(folder);
        var scriptPath = Path.Combine(folder, $"ts-parser-{hash}.js");
        if (File.Exists(scriptPath) && File.ReadAllBytes(scriptPath).AsSpan().SequenceEqual(content))
            return scriptPath;

        // Written beside and moved into place, so a parse in another process never runs half a file
        var tempPath = $"{scriptPath}.{Environment.ProcessId}.tmp";
        File.WriteAllBytes(tempPath, content);
        File.Move(tempPath, scriptPath, overwrite: true);
        DeleteOtherVersions(folder, scriptPath);
        return scriptPath;
    }

    static void DeleteOtherVersions(string folder, string scriptPath)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "ts-parser-*.js").Where(file => file != scriptPath))
            Result.Catch(() => File.Delete(file));
    }
}
