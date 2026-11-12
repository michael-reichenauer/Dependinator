using System.Security.Cryptography;

namespace Dependinator.TypeScript.Parsing;

// The parser helper's script (Helper/, bundled into one file and embedded in this assembly) as
// a file Node can run: written once per version to the user's local application data, under a
// name of its content's hash, so a file of that name that someone changed is never run.
// Several versions of the app can run at once (VS Code's release and pre-release, the browser
// host), each with its own script: one in use is never deleted, only what nobody used for long.
static class HelperScript
{
    const string ResourceName = "Dependinator.TypeScript.ts-parser.js";
    static readonly TimeSpan UnusedAge = TimeSpan.FromDays(30);
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
        {
            // Its time tells the other versions that this one is in use
            Result.Catch(() => File.SetLastWriteTimeUtc(scriptPath, DateTime.UtcNow));
            return scriptPath;
        }

        // Written beside and moved into place, so a parse in another process never runs half a file
        var tempPath = $"{scriptPath}.{Environment.ProcessId}.tmp";
        File.WriteAllBytes(tempPath, content);
        File.Move(tempPath, scriptPath, overwrite: true);
        DeleteUnusedVersions(folder, scriptPath, DateTime.UtcNow - UnusedAge);
        return scriptPath;
    }

    // The scripts of versions that have not started since the time (and what a stopped process
    // left half written).
    internal static void DeleteUnusedVersions(string folder, string scriptPath, DateTime unusedSinceUtc)
    {
        var others = Directory
            .EnumerateFiles(folder, "ts-parser-*")
            .Where(file => file != scriptPath && File.GetLastWriteTimeUtc(file) < unusedSinceUtc);
        foreach (var file in others)
            Result.Catch(() => File.Delete(file));
    }
}
